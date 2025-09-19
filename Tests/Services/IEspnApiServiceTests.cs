using ESPNScrape.Services.Interfaces;
using System.Reflection;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class IEspnApiServiceTests
    {
        [Fact]
        public void Interface_AllMethods_HaveProperSignatures()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var methods = interfaceType.GetMethods();

            // Act & Assert
            Assert.True(methods.Length >= 14, "Interface should have at least 14 methods");

            // Verify key methods exist
            var seasonMethod = methods.FirstOrDefault(m => m.Name == "GetSeasonAsync");
            Assert.NotNull(seasonMethod);
            Assert.True(seasonMethod.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), seasonMethod.ReturnType.GetGenericTypeDefinition());

            var gamesMethod = methods.FirstOrDefault(m => m.Name == "GetGamesAsync");
            Assert.NotNull(gamesMethod);
            Assert.True(gamesMethod.GetParameters().Any(p => p.Name == "year"));
            Assert.True(gamesMethod.GetParameters().Any(p => p.Name == "weekNumber"));
        }

        [Fact]
        public void Interface_AllAsyncMethods_HaveCancellationTokenParameter()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var asyncMethods = interfaceType.GetMethods()
                .Where(m => m.Name.EndsWith("Async"));

            // Act & Assert
            foreach (var method in asyncMethods)
            {
                var hasToken = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken));

                Assert.True(hasToken, $"Method {method.Name} should have CancellationToken parameter");
            }
        }

        [Fact]
        public void Interface_BulkMethods_ExistAndReturnCollections()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var seasonStatsMethod = interfaceType.GetMethod("GetSeasonPlayerStatsAsync");
            var weekStatsMethod = interfaceType.GetMethod("GetAllPlayersWeekStatsAsync");

            // Assert
            Assert.NotNull(seasonStatsMethod);
            Assert.NotNull(weekStatsMethod);

            // Verify return types are collections
            Assert.True(seasonStatsMethod.ReturnType.IsGenericType);
            Assert.True(weekStatsMethod.ReturnType.IsGenericType);
        }

        [Fact]
        public void Interface_OverloadMethods_HaveDefaultParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var weekMethod = interfaceType.GetMethod("GetWeekAsync");
            var gamesMethod = interfaceType.GetMethod("GetGamesAsync");

            // Assert
            Assert.NotNull(weekMethod);
            Assert.NotNull(gamesMethod);

            // Verify seasonType has default value
            var weekSeasonTypeParam = weekMethod.GetParameters()
                .FirstOrDefault(p => p.Name == "seasonType");
            Assert.NotNull(weekSeasonTypeParam);
            Assert.True(weekSeasonTypeParam.HasDefaultValue);
            Assert.Equal(2, weekSeasonTypeParam.DefaultValue);
        }

        [Fact]
        public void Interface_Documentation_RequiredMethodsExist()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var requiredMethods = new[]
            {
                "GetSeasonAsync",
                "GetWeeksAsync",
                "GetCurrentWeekAsync",
                "GetWeekAsync",
                "GetGamesAsync",
                "GetGameAsync",
                "GetGamesForDateAsync",
                "GetBoxScoreAsync",
                "GetGamePlayerStatsAsync",
                "GetWeekPlayerStatsAsync",
                "GetSeasonPlayerStatsAsync",
                "GetAllPlayersWeekStatsAsync",
                "GetTeamsAsync",
                "GetTeamAsync"
            };

            // Act & Assert
            foreach (var methodName in requiredMethods)
            {
                var method = interfaceType.GetMethod(methodName);
                Assert.NotNull(method);
                Assert.True(method.Name.EndsWith("Async"), $"Method {methodName} should be async");
            }
        }

        [Fact]
        public void Interface_SeasonMethods_HaveCorrectParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var getSeasonMethod = interfaceType.GetMethod("GetSeasonAsync");
            var getWeeksMethod = interfaceType.GetMethod("GetWeeksAsync");

            // Assert
            Assert.NotNull(getSeasonMethod);
            Assert.NotNull(getWeeksMethod);

            // Verify GetSeasonAsync parameters
            var seasonParams = getSeasonMethod.GetParameters();
            Assert.Contains(seasonParams, p => p.Name == "year" && p.ParameterType == typeof(int));
            Assert.Contains(seasonParams, p => p.Name == "cancellationToken" && p.ParameterType == typeof(CancellationToken));

            // Verify GetWeeksAsync parameters
            var weeksParams = getWeeksMethod.GetParameters();
            Assert.Contains(weeksParams, p => p.Name == "year" && p.ParameterType == typeof(int));
            Assert.Contains(weeksParams, p => p.Name == "seasonType" && p.ParameterType == typeof(int));
            Assert.Contains(weeksParams, p => p.Name == "cancellationToken" && p.ParameterType == typeof(CancellationToken));
        }

        [Fact]
        public void Interface_GameMethods_HaveCorrectParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var getGamesMethod = interfaceType.GetMethod("GetGamesAsync");
            var getGameMethod = interfaceType.GetMethod("GetGameAsync");
            var getGamesForDateMethod = interfaceType.GetMethod("GetGamesForDateAsync");

            // Assert
            Assert.NotNull(getGamesMethod);
            Assert.NotNull(getGameMethod);
            Assert.NotNull(getGamesForDateMethod);

            // Verify GetGamesAsync parameters
            var gamesParams = getGamesMethod.GetParameters();
            Assert.Contains(gamesParams, p => p.Name == "year" && p.ParameterType == typeof(int));
            Assert.Contains(gamesParams, p => p.Name == "weekNumber" && p.ParameterType == typeof(int));
            Assert.Contains(gamesParams, p => p.Name == "seasonType" && p.ParameterType == typeof(int));

            // Verify GetGameAsync parameters
            var gameParams = getGameMethod.GetParameters();
            Assert.Contains(gameParams, p => p.Name == "eventId" && p.ParameterType == typeof(string));

            // Verify GetGamesForDateAsync parameters
            var dateParams = getGamesForDateMethod.GetParameters();
            Assert.Contains(dateParams, p => p.Name == "date" && p.ParameterType == typeof(DateTime));
        }

        [Fact]
        public void Interface_PlayerStatsMethods_HaveCorrectParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var getGameStatsMethod = interfaceType.GetMethod("GetGamePlayerStatsAsync");
            var getWeekStatsMethod = interfaceType.GetMethod("GetWeekPlayerStatsAsync");
            var getSeasonStatsMethod = interfaceType.GetMethod("GetSeasonPlayerStatsAsync");
            var getAllStatsMethod = interfaceType.GetMethod("GetAllPlayersWeekStatsAsync");

            // Assert
            Assert.NotNull(getGameStatsMethod);
            Assert.NotNull(getWeekStatsMethod);
            Assert.NotNull(getSeasonStatsMethod);
            Assert.NotNull(getAllStatsMethod);

            // Verify parameter types for game stats
            var gameStatsParams = getGameStatsMethod.GetParameters();
            Assert.Contains(gameStatsParams, p => p.Name == "eventId" && p.ParameterType == typeof(string));

            // Verify parameter types for week stats
            var weekStatsParams = getWeekStatsMethod.GetParameters();
            Assert.Contains(weekStatsParams, p => p.Name == "year" && p.ParameterType == typeof(int));
            Assert.Contains(weekStatsParams, p => p.Name == "weekNumber" && p.ParameterType == typeof(int));
            Assert.Contains(weekStatsParams, p => p.Name == "seasonType" && p.ParameterType == typeof(int));

            // Verify parameter types for season stats
            var seasonStatsParams = getSeasonStatsMethod.GetParameters();
            Assert.Contains(seasonStatsParams, p => p.Name == "year" && p.ParameterType == typeof(int));
            Assert.Contains(seasonStatsParams, p => p.Name == "seasonType" && p.ParameterType == typeof(int));
        }

        [Fact]
        public void Interface_TeamMethods_HaveCorrectParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var getTeamsMethod = interfaceType.GetMethod("GetTeamsAsync");
            var getTeamMethod = interfaceType.GetMethod("GetTeamAsync");

            // Assert
            Assert.NotNull(getTeamsMethod);
            Assert.NotNull(getTeamMethod);

            // Verify GetTeamsAsync parameters (should only have cancellation token)
            var teamsParams = getTeamsMethod.GetParameters();
            Assert.Single(teamsParams);
            Assert.Contains(teamsParams, p => p.Name == "cancellationToken" && p.ParameterType == typeof(CancellationToken));

            // Verify GetTeamAsync parameters
            var teamParams = getTeamMethod.GetParameters();
            Assert.Contains(teamParams, p => p.Name == "teamId" && p.ParameterType == typeof(string));
            Assert.Contains(teamParams, p => p.Name == "cancellationToken" && p.ParameterType == typeof(CancellationToken));
        }

        [Fact]
        public void Interface_BoxScoreMethod_HasCorrectParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var getBoxScoreMethod = interfaceType.GetMethod("GetBoxScoreAsync");

            // Assert
            Assert.NotNull(getBoxScoreMethod);

            var boxScoreParams = getBoxScoreMethod.GetParameters();
            Assert.Contains(boxScoreParams, p => p.Name == "eventId" && p.ParameterType == typeof(string));
            Assert.Contains(boxScoreParams, p => p.Name == "cancellationToken" && p.ParameterType == typeof(CancellationToken));

            // Verify return type
            Assert.True(getBoxScoreMethod.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), getBoxScoreMethod.ReturnType.GetGenericTypeDefinition());
        }

        [Fact]
        public void Interface_ReturnTypes_AreCorrect()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act & Assert for individual item methods
            var seasonMethod = interfaceType.GetMethod("GetSeasonAsync");
            Assert.NotNull(seasonMethod);
            Assert.Equal("Season", seasonMethod.ReturnType.GetGenericArguments()[0].Name);

            var weekMethod = interfaceType.GetMethod("GetWeekAsync");
            Assert.NotNull(weekMethod);
            Assert.Equal("Week", weekMethod.ReturnType.GetGenericArguments()[0].Name);

            var gameMethod = interfaceType.GetMethod("GetGameAsync");
            Assert.NotNull(gameMethod);
            Assert.Equal("GameEvent", gameMethod.ReturnType.GetGenericArguments()[0].Name);

            var teamMethod = interfaceType.GetMethod("GetTeamAsync");
            Assert.NotNull(teamMethod);
            Assert.Equal("Team", teamMethod.ReturnType.GetGenericArguments()[0].Name);

            var boxScoreMethod = interfaceType.GetMethod("GetBoxScoreAsync");
            Assert.NotNull(boxScoreMethod);
            Assert.Equal("BoxScore", boxScoreMethod.ReturnType.GetGenericArguments()[0].Name);

            // Act & Assert for collection methods
            var weeksMethod = interfaceType.GetMethod("GetWeeksAsync");
            Assert.NotNull(weeksMethod);
            var weeksReturnType = weeksMethod.ReturnType.GetGenericArguments()[0];
            Assert.True(weeksReturnType.IsGenericType);
            Assert.Equal(typeof(IEnumerable<>), weeksReturnType.GetGenericTypeDefinition());

            var gamesMethod = interfaceType.GetMethod("GetGamesAsync");
            Assert.NotNull(gamesMethod);
            var gamesReturnType = gamesMethod.ReturnType.GetGenericArguments()[0];
            Assert.True(gamesReturnType.IsGenericType);
            Assert.Equal(typeof(IEnumerable<>), gamesReturnType.GetGenericTypeDefinition());
        }

        [Fact]
        public void Interface_CancellationTokenParameters_HaveDefaultValues()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var methods = interfaceType.GetMethods();

            // Act & Assert
            foreach (var method in methods)
            {
                var tokenParam = method.GetParameters()
                    .FirstOrDefault(p => p.ParameterType == typeof(CancellationToken));

                if (tokenParam != null)
                {
                    Assert.True(tokenParam.HasDefaultValue,
                        $"Method {method.Name} should have default value for CancellationToken parameter");
                }
            }
        }

        [Fact]
        public void Interface_SeasonTypeParameters_HaveDefaultValue()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var methodsWithSeasonType = new[]
            {
                "GetWeeksAsync",
                "GetWeekAsync",
                "GetGamesAsync",
                "GetWeekPlayerStatsAsync",
                "GetSeasonPlayerStatsAsync",
                "GetAllPlayersWeekStatsAsync"
            };

            // Act & Assert
            foreach (var methodName in methodsWithSeasonType)
            {
                var method = interfaceType.GetMethod(methodName);
                Assert.NotNull(method);

                var seasonTypeParam = method.GetParameters()
                    .FirstOrDefault(p => p.Name == "seasonType");

                if (seasonTypeParam != null)
                {
                    Assert.True(seasonTypeParam.HasDefaultValue,
                        $"Method {methodName} should have default value for seasonType parameter");
                    Assert.Equal(2, seasonTypeParam.DefaultValue);
                }
            }
        }
    }
}