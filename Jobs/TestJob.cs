using Quartz;

namespace ESPNScrape.Jobs;

[DisallowConcurrentExecution]
public class TestJob : IJob
{
    private readonly ILogger<TestJob> _logger;

    public TestJob(ILogger<TestJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogError("=== TEST JOB EXECUTED SUCCESSFULLY ===");
        await Task.CompletedTask;
    }
}