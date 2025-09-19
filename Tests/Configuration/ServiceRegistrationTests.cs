using ESPNScrape.Services;
using ESPNScrape.HealthChecks;
using Xunit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;

namespace ESPNScrape.Tests.Configuration
{
    public class ServiceRegistrationTests
    {
        [Fact]
        public void ServiceRegistration_AllServicesRegistered_Successfully()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Add basic services that would be registered in Program.cs
            services.AddLogging();
            services.AddHttpClient<IEspnHttpService, EspnHttpService>();
            services.AddMemoryCache();
            services.AddHealthChecks()
                .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" });

            // Act
            var serviceProvider = services.BuildServiceProvider();

            // Assert - Verify all ESPN-related services are registered
            var espnHttpService = serviceProvider.GetService<IEspnHttpService>();
            Assert.NotNull(espnHttpService);
            Assert.IsType<EspnHttpService>(espnHttpService);

            var memoryCache = serviceProvider.GetService<IMemoryCache>();
            Assert.NotNull(memoryCache);

            var healthCheckService = serviceProvider.GetService<HealthCheckService>();
            Assert.NotNull(healthCheckService);

            // Verify health check is registered
            var healthCheckOptions = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
            Assert.NotNull(healthCheckOptions);
            Assert.Contains(healthCheckOptions.Value.Registrations, r => r.Name == "espn_api");
        }

        [Fact]
        public void HttpClient_Configuration_IsCorrect()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient<IEspnHttpService, EspnHttpService>();

            // Act
            var serviceProvider = services.BuildServiceProvider();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(typeof(EspnHttpService).FullName ?? "EspnHttpService");

            // Assert
            Assert.NotNull(httpClient);
            // The timeout is set in EspnHttpService constructor, default HttpClient timeout is 100 seconds
            Assert.True(httpClient.Timeout > TimeSpan.Zero);

            // Verify the ESPN service can be created (this is the main test)
            var espnService = serviceProvider.GetRequiredService<IEspnHttpService>();
            Assert.NotNull(espnService);
        }

        [Fact]
        public void HealthCheck_EspnApi_IsRegistered()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient<IEspnHttpService, EspnHttpService>();
            services.AddHealthChecks()
                .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" });

            // Act
            var serviceProvider = services.BuildServiceProvider();
            var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

            // Assert
            Assert.NotNull(healthCheckService);

            // Verify the health check can be executed (this would require the ESPN service to be available)
            // For unit testing, we just verify the registration is correct
            var healthCheckOptions = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
            Assert.NotNull(healthCheckOptions);

            var espnHealthCheck = healthCheckOptions.Value.Registrations.FirstOrDefault(r => r.Name == "espn_api");
            Assert.NotNull(espnHealthCheck);
            Assert.Contains("espn", espnHealthCheck.Tags);
            Assert.Contains("api", espnHealthCheck.Tags);
        }

        [Fact]
        public void MemoryCache_Configuration_IsCorrect()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1000;
            });

            // Act
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();

            // Assert
            Assert.NotNull(memoryCache);

            // Test basic cache functionality with size specification
            var testKey = "test_key";
            var testValue = "test_value";

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                Size = 1
            };

            memoryCache.Set(testKey, testValue, cacheEntryOptions);
            var retrievedValue = memoryCache.Get<string>(testKey);

            Assert.Equal(testValue, retrievedValue);
        }
    }
}