using ESPNScrape.Controllers;
using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace ESPNScrape.Tests.Controllers;

/// <summary>
/// Unit tests for EspnIntegrationController
/// </summary>
public class EspnIntegrationControllerTests
{
    private readonly Mock<ILogger<EspnIntegrationController>> _mockLogger;
    private readonly Mock<IEspnDataSyncService> _mockDataSyncService;
    private readonly Mock<ISchedulerFactory> _mockSchedulerFactory;
    private readonly Mock<IScheduler> _mockScheduler;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly EspnIntegrationController _controller;

    public EspnIntegrationControllerTests()
    {
        _mockLogger = new Mock<ILogger<EspnIntegrationController>>();
        _mockDataSyncService = new Mock<IEspnDataSyncService>();
        _mockSchedulerFactory = new Mock<ISchedulerFactory>();
        _mockScheduler = new Mock<IScheduler>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockSchedulerFactory.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(_mockScheduler.Object);

        _controller = new EspnIntegrationController(
            _mockLogger.Object,
            _mockDataSyncService.Object,
            _mockSchedulerFactory.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task TriggerPlayerSync_WhenSuccessful_ShouldReturnOkWithResult()
    {
        // Arrange
        var syncOptions = new SyncOptions { BatchSize = 50 };
        var expectedResult = new SyncResult
        {
            PlayersProcessed = 100,
            PlayersUpdated = 50,
            NewPlayersAdded = 10,
            DataErrors = 0
        };

        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(syncOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.TriggerPlayerSync(syncOptions);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var syncResult = Assert.IsType<SyncResult>(okResult.Value);
        Assert.Equal(expectedResult.PlayersProcessed, syncResult.PlayersProcessed);
        Assert.Equal(expectedResult.PlayersUpdated, syncResult.PlayersUpdated);
        Assert.Equal(expectedResult.NewPlayersAdded, syncResult.NewPlayersAdded);
    }

    [Fact]
    public async Task TriggerPlayerSync_WhenSyncAlreadyRunning_ShouldReturnConflict()
    {
        // Arrange
        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _controller.TriggerPlayerSync();

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task TriggerPlayerSync_WhenExceptionOccurs_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockDataSyncService.Setup(x => x.IsSyncRunningAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockDataSyncService.Setup(x => x.SyncPlayersAsync(It.IsAny<SyncOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.TriggerPlayerSync();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}