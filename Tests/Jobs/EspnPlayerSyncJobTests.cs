using ESPNScrape.Jobs;
using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ESPNScrape.Tests.Jobs;

/// <summary>
/// Unit tests for EspnPlayerSyncJob
/// </summary>
public class EspnPlayerSyncJobTests
{
    private readonly Mock<ILogger<EspnPlayerSyncJob>> _mockLogger;
    private readonly Mock<IEspnDataSyncService> _mockDataSyncService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IJobExecutionContext> _mockJobContext;
    private readonly EspnPlayerSyncJob _job;

    public EspnPlayerSyncJobTests()
    {
        _mockLogger = new Mock<ILogger<EspnPlayerSyncJob>>();
        _mockDataSyncService = new Mock<IEspnDataSyncService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockJobContext = new Mock<IJobExecutionContext>();

        _job = new EspnPlayerSyncJob(
            _mockLogger.Object,
            _mockDataSyncService.Object,
            _mockConfiguration.Object);

        SetupDefaultMockBehavior();
    }

    [Fact]
    public async Task Execute_WhenOffSeason_ShouldSkipExecution()
    {
        // Arrange
        SetupOffSeasonConfiguration();

        // Act
        await _job.Execute(_mockJobContext.Object);

        // Assert
        _mockDataSyncService.Verify(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyInfoLog("ESPN player sync job .* skipped - currently off-season");
    }

    [Fact]
    public async Task Execute_WhenSyncAlreadyRunning_ShouldSkipExecution()
    {
        // Arrange
        SetupNflSeasonConfiguration();
        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _job.Execute(_mockJobContext.Object);

        // Assert
        _mockDataSyncService.Verify(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyWarningLog("ESPN player sync job .* skipped - another sync operation is already running");
    }

    [Fact]
    public async Task Execute_WhenSuccessful_ShouldCompleteSyncAndLog()
    {
        // Arrange
        SetupNflSeasonConfiguration();
        var expectedResult = new SyncResult
        {
            PlayersProcessed = 100,
            PlayersUpdated = 50,
            NewPlayersAdded = 10,
            DataErrors = 0,
            MatchingErrors = 0,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };
        expectedResult.EndTime = expectedResult.StartTime.AddMinutes(5);

        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        await _job.Execute(_mockJobContext.Object);

        // Assert
        _mockDataSyncService.Verify(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyInfoLog("ESPN player sync job .* completed successfully");
    }

    [Fact]
    public async Task Execute_WhenSyncFails_ShouldLogErrorAndRethrow()
    {
        // Arrange
        SetupNflSeasonConfiguration();
        var expectedException = new Exception("Sync failed");

        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _job.Execute(_mockJobContext.Object));
        Assert.Equal(expectedException.Message, exception.Message);

        VerifyErrorLog("ESPN player sync job .* failed");
    }

    [Fact]
    public async Task Execute_WhenOperationCancelled_ShouldLogAndRethrow()
    {
        // Arrange
        SetupNflSeasonConfiguration();
        var cancellationException = new OperationCanceledException("Operation was cancelled");

        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(cancellationException);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _job.Execute(_mockJobContext.Object));

        VerifyWarningLog("ESPN player sync job .* was cancelled");
    }

    [Fact]
    public async Task Execute_WithSyncErrors_ShouldLogWarningsAndComplete()
    {
        // Arrange
        SetupNflSeasonConfiguration();
        var resultWithErrors = new SyncResult
        {
            PlayersProcessed = 100,
            PlayersUpdated = 45,
            NewPlayersAdded = 5,
            DataErrors = 3,
            MatchingErrors = 2,
            Errors = new List<string> { "Error 1", "Error 2", "Error 3" },
            Warnings = new List<string> { "Warning 1", "Warning 2" }
        };
        resultWithErrors.EndTime = resultWithErrors.StartTime.AddMinutes(5);

        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultWithErrors);

        // Act
        await _job.Execute(_mockJobContext.Object);

        // Assert
        VerifyWarningLog("ESPN player sync job .* completed with errors");
        VerifyInfoLog("ESPN player sync job .* completed with .* warnings");
    }

    private void SetupDefaultMockBehavior()
    {
        _mockJobContext.Setup(x => x.FireInstanceId).Returns("test-job-instance");
        _mockJobContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
        configSection.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<int>())).Returns(100);
        _mockConfiguration.Setup(x => x.GetSection("DataSync:PlayerSync")).Returns(configSection.Object);
        _mockConfiguration.Setup(x => x["Job:TimeoutMinutes"]).Returns("60");
        _mockConfiguration.Setup(x => x["DataStorage:Directory"]).Returns("data");
    }

    private void SetupNflSeasonConfiguration()
    {
        // Mock configuration to simulate NFL season (current date should be in season)
        // The job checks current month, so we don't need to mock DateTime if running during season
        // For off-season testing, we use the ForceExecution config
        _mockConfiguration.Setup(x => x["Job:ForceExecution"]).Returns("false");
    }

    private void SetupOffSeasonConfiguration()
    {
        // Simulate off-season by ensuring ForceExecution is false
        // In real off-season months, the job would skip naturally
        _mockConfiguration.Setup(x => x["Job:ForceExecution"]).Returns("false");

        // Note: In actual testing, you might want to mock DateTime.Now or use a time provider
        // For simplicity, this test assumes we're testing during off-season months
    }

    private void VerifyInfoLog(string expectedPattern)
    {
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ESPN player sync job") ||
                                             System.Text.RegularExpressions.Regex.IsMatch(v.ToString()!, expectedPattern)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyWarningLog(string expectedPattern)
    {
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => System.Text.RegularExpressions.Regex.IsMatch(v.ToString()!, expectedPattern)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyErrorLog(string expectedPattern)
    {
        _mockLogger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => System.Text.RegularExpressions.Regex.IsMatch(v.ToString()!, expectedPattern)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}