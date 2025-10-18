using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service responsible for bulk operations and performance-optimized data collection
    /// </summary>
    public class EspnBulkOperationsService : IEspnBulkOperationsService
    {
        private readonly IEspnApiService _apiService;
        private readonly IEspnPlayerStatsService _playerStatsService;
        private readonly IEspnScoreboardService _scoreboardService;
        private readonly IEspnRateLimitService _rateLimitService;
        private readonly ILogger<EspnBulkOperationsService> _logger;

        public EspnBulkOperationsService(
            IEspnApiService apiService,
            IEspnPlayerStatsService playerStatsService,
            IEspnScoreboardService scoreboardService,
            IEspnRateLimitService rateLimitService,
            ILogger<EspnBulkOperationsService> logger)
        {
            _apiService = apiService;
            _playerStatsService = playerStatsService;
            _scoreboardService = scoreboardService;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        public async Task<IEnumerable<PlayerStats>> GetBulkWeekPlayerStatsAsync(
            BulkWeekRequest request,
            IProgress<BulkOperationProgress>? progressCallback = null)
        {
            var validationResult = ValidateBulkOperationOptions(request.Options);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid bulk operation options: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var allResults = new ConcurrentBag<PlayerStats>();
            var totalWeeks = request.WeekNumbers.Count;
            var completedWeeks = 0;
            var failedWeeks = 0;
            var errorMessages = new ConcurrentBag<string>();

            _logger.LogInformation("Starting bulk week player stats operation {OperationId} for {WeekCount} weeks in year {Year}",
                operationId, totalWeeks, request.Year);

            var progress = new BulkOperationProgress
            {
                OperationId = operationId,
                OperationType = "BulkWeekPlayerStats",
                TotalItems = totalWeeks,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow
            };

            progressCallback?.Report(progress);

            using var semaphore = CreateOptimizedSemaphore(request.Options.MaxConcurrency);
            var tasks = request.WeekNumbers.Select(async weekNumber =>
            {
                await semaphore.WaitAsync(request.Options.CancellationToken);
                try
                {
                    var currentWeek = $"Year {request.Year}, Week {weekNumber}";
                    _logger.LogDebug("Processing {CurrentWeek}", currentWeek);

                    var weekStats = await _apiService.GetWeekPlayerStatsAsync(
                        request.Year, weekNumber, request.SeasonType, request.Options.CancellationToken);

                    foreach (var stat in weekStats)
                    {
                        allResults.Add(stat);
                    }

                    var completed = Interlocked.Increment(ref completedWeeks);
                    _logger.LogDebug("Completed {CurrentWeek} - {Completed}/{Total}", currentWeek, completed, totalWeeks);

                    // Update progress
                    if (request.Options.EnableProgressReporting)
                    {
                        progress.CompletedItems = completed;
                        progress.FailedItems = failedWeeks;
                        progress.CurrentItem = currentWeek;
                        progress.LastUpdateTime = DateTime.UtcNow;
                        progress.EstimatedTimeRemaining = EstimateTimeRemaining(totalWeeks, completed, stopwatch.Elapsed);

                        if (request.Options.EnableMetrics)
                        {
                            progress.Metrics = new BulkOperationMetrics
                            {
                                AverageItemProcessingTime = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds / Math.Max(1, completed)),
                                ItemsPerSecond = completed / Math.Max(1, stopwatch.Elapsed.TotalSeconds),
                                PeakMemoryUsage = GetCurrentMemoryUsage(),
                                TotalDataProcessed = allResults.Count
                            };
                        }

                        progressCallback?.Report(progress);
                    }

                    // Memory optimization check
                    if (GetCurrentMemoryUsage() > request.Options.MaxMemoryThreshold)
                    {
                        _logger.LogWarning("Memory threshold exceeded, optimizing memory usage");
                        OptimizeMemoryUsage();
                    }
                }
                catch (Exception ex)
                {
                    var failed = Interlocked.Increment(ref failedWeeks);
                    var errorMessage = $"Failed to process week {weekNumber}: {ex.Message}";
                    errorMessages.Add(errorMessage);
                    _logger.LogError(ex, "Failed to process week {WeekNumber} for year {Year}", weekNumber, request.Year);

                    if (!request.Options.ContinueOnError)
                    {
                        throw;
                    }

                    // Update progress with error
                    if (request.Options.EnableProgressReporting)
                    {
                        progress.FailedItems = failed;
                        progress.ErrorMessages.Add(errorMessage);
                        progressCallback?.Report(progress);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            progress.IsCompleted = true;
            progress.CompletedItems = completedWeeks;
            progress.FailedItems = failedWeeks;
            progress.LastUpdateTime = DateTime.UtcNow;
            progressCallback?.Report(progress);

            stopwatch.Stop();
            _logger.LogInformation("Completed bulk week player stats operation {OperationId}. " +
                "Processed {CompletedWeeks}/{TotalWeeks} weeks in {ElapsedTime}. " +
                "Total player stats: {TotalStats}",
                operationId, completedWeeks, totalWeeks, stopwatch.Elapsed, allResults.Count);

            return allResults.ToList();
        }

        public async Task<IEnumerable<PlayerStats>> GetBulkSeasonPlayerStatsAsync(
            BulkSeasonRequest request,
            IProgress<BulkOperationProgress>? progressCallback = null)
        {
            var validationResult = ValidateBulkOperationOptions(request.Options);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid bulk operation options: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var allResults = new ConcurrentBag<PlayerStats>();

            // Calculate total items (years * season types)
            var totalSeasons = request.Years.Count * request.SeasonTypes.Count;
            var completedSeasons = 0;
            var failedSeasons = 0;
            var errorMessages = new ConcurrentBag<string>();

            _logger.LogInformation("Starting bulk season player stats operation {OperationId} for {SeasonCount} seasons",
                operationId, totalSeasons);

            var progress = new BulkOperationProgress
            {
                OperationId = operationId,
                OperationType = "BulkSeasonPlayerStats",
                TotalItems = totalSeasons,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow
            };

            progressCallback?.Report(progress);

            using var semaphore = CreateOptimizedSemaphore(request.Options.MaxConcurrency);
            var tasks = new List<Task>();

            foreach (var year in request.Years)
            {
                foreach (var seasonType in request.SeasonTypes)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(request.Options.CancellationToken);
                        try
                        {
                            var currentSeason = $"Year {year}, Season Type {seasonType}";
                            _logger.LogDebug("Processing {CurrentSeason}", currentSeason);

                            var seasonStats = await _apiService.GetSeasonPlayerStatsAsync(
                                year, seasonType, request.Options.CancellationToken);

                            foreach (var stat in seasonStats)
                            {
                                allResults.Add(stat);
                            }

                            var completed = Interlocked.Increment(ref completedSeasons);
                            _logger.LogDebug("Completed {CurrentSeason} - {Completed}/{Total}", currentSeason, completed, totalSeasons);

                            // Update progress
                            if (request.Options.EnableProgressReporting)
                            {
                                progress.CompletedItems = completed;
                                progress.FailedItems = failedSeasons;
                                progress.CurrentItem = currentSeason;
                                progress.LastUpdateTime = DateTime.UtcNow;
                                progress.EstimatedTimeRemaining = EstimateTimeRemaining(totalSeasons, completed, stopwatch.Elapsed);

                                if (request.Options.EnableMetrics)
                                {
                                    progress.Metrics = new BulkOperationMetrics
                                    {
                                        AverageItemProcessingTime = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds / Math.Max(1, completed)),
                                        ItemsPerSecond = completed / Math.Max(1, stopwatch.Elapsed.TotalSeconds),
                                        PeakMemoryUsage = GetCurrentMemoryUsage(),
                                        TotalDataProcessed = allResults.Count
                                    };
                                }

                                progressCallback?.Report(progress);
                            }

                            // Memory optimization check
                            if (GetCurrentMemoryUsage() > request.Options.MaxMemoryThreshold)
                            {
                                _logger.LogWarning("Memory threshold exceeded, optimizing memory usage");
                                OptimizeMemoryUsage();
                            }
                        }
                        catch (Exception ex)
                        {
                            var failed = Interlocked.Increment(ref failedSeasons);
                            var errorMessage = $"Failed to process year {year}, season type {seasonType}: {ex.Message}";
                            errorMessages.Add(errorMessage);
                            _logger.LogError(ex, "Failed to process year {Year}, season type {SeasonType}", year, seasonType);

                            if (!request.Options.ContinueOnError)
                            {
                                throw;
                            }

                            // Update progress with error
                            if (request.Options.EnableProgressReporting)
                            {
                                progress.FailedItems = failed;
                                progress.ErrorMessages.Add(errorMessage);
                                progressCallback?.Report(progress);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, request.Options.CancellationToken));
                }
            }

            await Task.WhenAll(tasks);

            progress.IsCompleted = true;
            progress.CompletedItems = completedSeasons;
            progress.FailedItems = failedSeasons;
            progress.LastUpdateTime = DateTime.UtcNow;
            progressCallback?.Report(progress);

            stopwatch.Stop();
            _logger.LogInformation("Completed bulk season player stats operation {OperationId}. " +
                "Processed {CompletedSeasons}/{TotalSeasons} seasons in {ElapsedTime}. " +
                "Total player stats: {TotalStats}",
                operationId, completedSeasons, totalSeasons, stopwatch.Elapsed, allResults.Count);

            return allResults.ToList();
        }

        public async Task<IEnumerable<PlayerStats>> GetBulkGamePlayerStatsAsync(
            IEnumerable<string> eventIds,
            BulkOperationOptions options,
            IProgress<BulkOperationProgress>? progressCallback = null)
        {
            var eventIdList = eventIds.ToList();
            var validationResult = ValidateBulkOperationOptions(options);
            if (!validationResult.IsValid)
            {
                throw new ArgumentException($"Invalid bulk operation options: {string.Join(", ", validationResult.ErrorMessages)}");
            }

            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var allResults = new ConcurrentBag<PlayerStats>();
            var totalGames = eventIdList.Count;

            _logger.LogInformation("Starting bulk game player stats operation {OperationId} for {GameCount} games",
                operationId, totalGames);

            var progress = new BulkOperationProgress
            {
                OperationId = operationId,
                OperationType = "BulkGamePlayerStats",
                TotalItems = totalGames,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow
            };

            progressCallback?.Report(progress);

            // Use enhanced player stats service for bulk processing
            var gameStats = await _playerStatsService.ExtractBulkGamePlayerStatsAsync(
                eventIdList, options.MaxConcurrency, options.CancellationToken);

            foreach (var stat in gameStats)
            {
                allResults.Add(stat);
            }

            progress.IsCompleted = true;
            progress.CompletedItems = totalGames;
            progress.LastUpdateTime = DateTime.UtcNow;
            progressCallback?.Report(progress);

            stopwatch.Stop();
            _logger.LogInformation("Completed bulk game player stats operation {OperationId}. " +
                "Processed {TotalGames} games in {ElapsedTime}. " +
                "Total player stats: {TotalStats}",
                operationId, totalGames, stopwatch.Elapsed, allResults.Count);

            return allResults.ToList();
        }

        public IAsyncEnumerable<T> StreamParseJsonAsync<T>(
            string jsonContent,
            CancellationToken cancellationToken = default) where T : class
        {
            return StreamParseJsonAsyncInternal<T>(jsonContent, cancellationToken);
        }

        private async IAsyncEnumerable<T> StreamParseJsonAsyncInternal<T>(
            string jsonContent,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
        {
            _logger.LogDebug("Starting streaming JSON parse for type {Type}", typeof(T).Name);

            await Task.Yield(); // Make it properly async

            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = JsonSerializer.Deserialize<T>(element.GetRawText());
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Try to find array properties to stream
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in property.Value.EnumerateArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var item = JsonSerializer.Deserialize<T>(element.GetRawText());
                            if (item != null)
                            {
                                yield return item;
                            }
                        }
                    }
                }
            }

            _logger.LogDebug("Completed streaming JSON parse for type {Type}", typeof(T).Name);
        }

        public async Task<IEnumerable<TResult>> ProcessInBatchesAsync<TItem, TResult>(
            IEnumerable<TItem> items,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<TResult>>> processor,
            BulkOperationOptions options,
            IProgress<BulkOperationProgress>? progressCallback = null)
        {
            var itemList = items.ToList();
            var totalBatches = (int)Math.Ceiling((double)itemList.Count / options.BatchSize);
            var allResults = new ConcurrentBag<TResult>();
            var completedBatches = 0;
            var operationId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Processing {ItemCount} items in {BatchCount} batches of size {BatchSize}",
                itemList.Count, totalBatches, options.BatchSize);

            var progress = new BulkOperationProgress
            {
                OperationId = operationId,
                OperationType = "BatchProcessing",
                TotalItems = totalBatches,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow
            };

            progressCallback?.Report(progress);

            using var semaphore = CreateOptimizedSemaphore(options.MaxConcurrency);
            var tasks = new List<Task>();

            for (int i = 0; i < totalBatches; i++)
            {
                var batchIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(options.CancellationToken);
                    try
                    {
                        var startIndex = batchIndex * options.BatchSize;
                        var batchItems = itemList.Skip(startIndex).Take(options.BatchSize);

                        _logger.LogDebug("Processing batch {BatchIndex}/{TotalBatches}", batchIndex + 1, totalBatches);

                        var results = await processor(batchItems, options.CancellationToken);

                        foreach (var result in results)
                        {
                            allResults.Add(result);
                        }

                        var completed = Interlocked.Increment(ref completedBatches);

                        // Update progress
                        if (options.EnableProgressReporting)
                        {
                            progress.CompletedItems = completed;
                            progress.CurrentItem = $"Batch {completed}/{totalBatches}";
                            progress.LastUpdateTime = DateTime.UtcNow;
                            progress.EstimatedTimeRemaining = EstimateTimeRemaining(totalBatches, completed, stopwatch.Elapsed);
                            progressCallback?.Report(progress);
                        }

                        // Memory optimization check
                        if (GetCurrentMemoryUsage() > options.MaxMemoryThreshold)
                        {
                            OptimizeMemoryUsage();
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, options.CancellationToken));
            }

            await Task.WhenAll(tasks);

            progress.IsCompleted = true;
            progress.CompletedItems = completedBatches;
            progress.LastUpdateTime = DateTime.UtcNow;
            progressCallback?.Report(progress);

            stopwatch.Stop();
            _logger.LogInformation("Completed batch processing operation {OperationId}. " +
                "Processed {CompletedBatches}/{TotalBatches} batches in {ElapsedTime}. " +
                "Total results: {TotalResults}",
                operationId, completedBatches, totalBatches, stopwatch.Elapsed, allResults.Count);

            return allResults.ToList();
        }

        public long GetCurrentMemoryUsage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(false);
        }

        public void OptimizeMemoryUsage()
        {
            _logger.LogDebug("Optimizing memory usage");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            _logger.LogDebug("Memory optimization completed. Current usage: {MemoryUsage} bytes", GetCurrentMemoryUsage());
        }

        public ValidationResult ValidateBulkOperationOptions(BulkOperationOptions options)
        {
            var result = new ValidationResult { IsValid = true };

            if (options.MaxConcurrency <= 0)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("MaxConcurrency must be greater than 0");
            }

            if (options.MaxConcurrency > 20)
            {
                result.WarningMessages.Add("MaxConcurrency is very high and may cause rate limiting issues");
            }

            if (options.BatchSize <= 0)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("BatchSize must be greater than 0");
            }

            if (options.MaxRetries < 0)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("MaxRetries cannot be negative");
            }

            if (options.RetryDelay < TimeSpan.Zero)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("RetryDelay cannot be negative");
            }

            if (options.MaxMemoryThreshold <= 0)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("MaxMemoryThreshold must be greater than 0");
            }

            if (options.ProgressUpdateInterval < TimeSpan.Zero)
            {
                result.IsValid = false;
                result.ErrorMessages.Add("ProgressUpdateInterval cannot be negative");
            }

            return result;
        }

        public SemaphoreSlim CreateOptimizedSemaphore(int maxConcurrency)
        {
            // Ensure we don't exceed reasonable limits to prevent rate limiting
            var optimizedConcurrency = Math.Min(maxConcurrency, 10);
            _logger.LogDebug("Creating semaphore with concurrency limit: {Concurrency}", optimizedConcurrency);
            return new SemaphoreSlim(optimizedConcurrency, optimizedConcurrency);
        }

        public TimeSpan EstimateTimeRemaining(int totalItems, int completedItems, TimeSpan elapsedTime)
        {
            if (completedItems == 0 || totalItems <= completedItems)
            {
                return TimeSpan.Zero;
            }

            var avgTimePerItem = elapsedTime.TotalMilliseconds / completedItems;
            var remainingItems = totalItems - completedItems;
            var estimatedRemainingMs = avgTimePerItem * remainingItems;

            return TimeSpan.FromMilliseconds(estimatedRemainingMs);
        }
    }
}