using System.Collections.Generic;

namespace ESPNScrape.Services
{
    public class EspnTeamMappingService
    {
        private static readonly Dictionary<string, (string Abbreviation, string DisplayName)> TeamMappings = new()
        {
            // AFC East
            { "2", ("BUF", "Buffalo Bills") },
            { "15", ("MIA", "Miami Dolphins") },
            { "17", ("NE", "New England Patriots") },
            { "20", ("NYJ", "New York Jets") },
            
            // AFC North
            { "33", ("BAL", "Baltimore Ravens") },
            { "4", ("CIN", "Cincinnati Bengals") },
            { "5", ("CLE", "Cleveland Browns") },
            { "23", ("PIT", "Pittsburgh Steelers") },
            
            // AFC South
            { "34", ("HOU", "Houston Texans") },
            { "11", ("IND", "Indianapolis Colts") },
            { "30", ("JAX", "Jacksonville Jaguars") },
            { "10", ("TEN", "Tennessee Titans") },
            
            // AFC West
            { "7", ("DEN", "Denver Broncos") },
            { "12", ("KC", "Kansas City Chiefs") },
            { "13", ("LV", "Las Vegas Raiders") },
            { "24", ("LAC", "Los Angeles Chargers") },
            
            // NFC East
            { "6", ("DAL", "Dallas Cowboys") },
            { "19", ("NYG", "New York Giants") },
            { "21", ("PHI", "Philadelphia Eagles") },
            { "28", ("WAS", "Washington Commanders") },
            
            // NFC North
            { "3", ("CHI", "Chicago Bears") },
            { "8", ("DET", "Detroit Lions") },
            { "9", ("GB", "Green Bay Packers") },
            { "16", ("MIN", "Minnesota Vikings") },
            
            // NFC South
            { "1", ("ATL", "Atlanta Falcons") },
            { "29", ("CAR", "Carolina Panthers") },
            { "18", ("NO", "New Orleans Saints") },
            { "27", ("TB", "Tampa Bay Buccaneers") },
            
            // NFC West
            { "22", ("ARI", "Arizona Cardinals") },
            { "14", ("LAR", "Los Angeles Rams") },
            { "25", ("SF", "San Francisco 49ers") },
            { "26", ("SEA", "Seattle Seahawks") }
        };

        public (string Abbreviation, string DisplayName)? GetTeamInfo(string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
                return null;

            return TeamMappings.TryGetValue(teamId, out var teamInfo) ? teamInfo : null;
        }

        public string? GetTeamAbbreviation(string teamId)
        {
            var teamInfo = GetTeamInfo(teamId);
            return teamInfo?.Abbreviation;
        }

        public string? GetTeamDisplayName(string teamId)
        {
            var teamInfo = GetTeamInfo(teamId);
            return teamInfo?.DisplayName;
        }
    }
}