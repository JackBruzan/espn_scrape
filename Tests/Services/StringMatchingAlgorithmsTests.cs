using Xunit;
using ESPNScrape.Services;

namespace ESPNScrape.Tests.Services
{
    /// <summary>
    /// Unit tests for string matching algorithms used in player matching
    /// </summary>
    public class StringMatchingAlgorithmsTests
    {
        [Theory]
        [InlineData("hello", "hello", 0)]
        [InlineData("hello", "world", 4)]
        [InlineData("", "", 0)]
        [InlineData("", "test", 4)]
        [InlineData("test", "", 4)]
        [InlineData("kitten", "sitting", 3)]
        [InlineData("saturday", "sunday", 3)]
        public void LevenshteinDistance_CalculatesCorrectEditDistance(string s1, string s2, int expectedDistance)
        {
            // Act
            var distance = StringMatchingAlgorithms.LevenshteinDistance(s1, s2);

            // Assert
            Assert.Equal(expectedDistance, distance);
        }

        [Theory]
        [InlineData("hello", "hello", 1.0)]
        [InlineData("hello", "hallo", 0.8)]
        [InlineData("", "", 1.0)]
        [InlineData("", "test", 0.0)]
        [InlineData("test", "", 0.0)]
        public void CalculateSimilarity_ReturnsCorrectSimilarityScore(string s1, string s2, double expectedSimilarity)
        {
            // Act
            var similarity = StringMatchingAlgorithms.CalculateSimilarity(s1, s2);

            // Assert
            Assert.Equal(expectedSimilarity, similarity, 1);
        }

        [Theory]
        [InlineData("Smith", "S530")]
        [InlineData("Johnson", "J525")]
        [InlineData("Williams", "W452")]
        [InlineData("Brown", "B650")]
        [InlineData("Jones", "J520")]
        [InlineData("Garcia", "G620")]
        [InlineData("Miller", "M460")]
        [InlineData("Davis", "D120")]
        [InlineData("Rodriguez", "R362")]
        [InlineData("Wilson", "W425")]
        public void Soundex_GeneratesCorrectSoundexCodes(string name, string expectedSoundex)
        {
            // Act
            var soundex = StringMatchingAlgorithms.Soundex(name);

            // Assert
            Assert.Equal(expectedSoundex, soundex);
        }

        [Theory]
        [InlineData("Smith", "Smyth", true)]
        [InlineData("Johnson", "Jonson", true)]
        [InlineData("Brown", "Braun", true)]
        [InlineData("Smith", "Jones", false)]
        [InlineData("", "", true)]
        [InlineData("", "Smith", false)]
        public void ArePhoneticallySimilar_IdentifiesPhoneticMatches(string name1, string name2, bool expectedSimilar)
        {
            // Act
            var areSimilar = StringMatchingAlgorithms.ArePhoneticallySimilar(name1, name2);

            // Assert
            Assert.Equal(expectedSimilar, areSimilar);
        }

        [Theory]
        [InlineData("John Smith Jr.", "john smith")]
        [InlineData("Mary O'Connor", "mary oconnor")]
        [InlineData("Jean-Pierre", "jean pierre")]
        [InlineData("  Multiple   Spaces  ", "multiple spaces")]
        [InlineData("Robert III", "robert")]
        public void NormalizeName_NormalizesNamesProperly(string input, string expectedOutput)
        {
            // Act
            var normalized = StringMatchingAlgorithms.NormalizeName(input);

            // Assert
            Assert.Equal(expectedOutput, normalized);
        }

        [Theory]
        [InlineData("John Smith", "JS")]
        [InlineData("Mary Jane Watson", "MJW")]
        [InlineData("", "")]
        [InlineData("Prince", "P")]
        [InlineData("Jean-Claude Van Damme", "JCVD")]
        public void GetInitials_ExtractsCorrectInitials(string name, string expectedInitials)
        {
            // Act
            var initials = StringMatchingAlgorithms.GetInitials(name);

            // Assert
            Assert.Equal(expectedInitials, initials);
        }

        [Theory]
        [InlineData("James", "Jim", true)]
        [InlineData("Robert", "Bob", true)]
        [InlineData("William", "Bill", true)]
        [InlineData("Christopher", "Chris", true)]
        [InlineData("Michael", "Mike", true)]
        [InlineData("John", "Johnny", false)] // Not in our mapping
        [InlineData("Smith", "Jones", false)]
        [InlineData("", "", false)]
        public void AreNameVariations_IdentifiesCommonNicknames(string fullName, string nickname, bool expectedVariation)
        {
            // Act
            var areVariations = StringMatchingAlgorithms.AreNameVariations(fullName, nickname);

            // Assert
            Assert.Equal(expectedVariation, areVariations);
        }

        [Theory]
        [InlineData("MARTHA", "MARHTA", 0.96)] // Should be high due to common prefix
        [InlineData("CRATE", "TRACE", 0.73)]
        [InlineData("DwaYne", "DuANE", 0.84)]
        [InlineData("", "", 1.0)]
        [InlineData("", "test", 0.0)]
        public void JaroWinklerSimilarity_CalculatesCorrectSimilarity(string s1, string s2, double expectedSimilarity)
        {
            // Act
            var similarity = StringMatchingAlgorithms.JaroWinklerSimilarity(s1, s2);

            // Assert
            Assert.Equal(expectedSimilarity, similarity, 2);
        }

        [Fact]
        public void NormalizeName_HandlesNullAndEmpty()
        {
            // Act & Assert
            Assert.Equal("", StringMatchingAlgorithms.NormalizeName(null!));
            Assert.Equal("", StringMatchingAlgorithms.NormalizeName(""));
            Assert.Equal("", StringMatchingAlgorithms.NormalizeName("   "));
        }

        [Fact]
        public void Soundex_HandlesEdgeCases()
        {
            // Act & Assert
            Assert.Equal("", StringMatchingAlgorithms.Soundex(null!));
            Assert.Equal("", StringMatchingAlgorithms.Soundex(""));
            Assert.Equal("A000", StringMatchingAlgorithms.Soundex("A"));
            Assert.Equal("A100", StringMatchingAlgorithms.Soundex("Ab"));
        }

        [Theory]
        [InlineData("Tom Brady", "Thomas Brady", 0.8)] // Should be relatively high
        [InlineData("A.J. Green", "AJ Green", 0.9)] // Should handle punctuation
        [InlineData("DeAndre Hopkins", "Deandre Hopkins", 0.95)] // Case variations
        [InlineData("Julio Jones", "Julio Jones Jr.", 0.85)] // Jr. suffix
        public void IntegrationTest_RealPlayerNameMatching(string name1, string name2, double minExpectedSimilarity)
        {
            // Act
            var levenshteinSim = StringMatchingAlgorithms.CalculateSimilarity(name1, name2);
            var jaroWinklerSim = StringMatchingAlgorithms.JaroWinklerSimilarity(name1, name2);

            // Assert - At least one algorithm should achieve the minimum similarity
            Assert.True(levenshteinSim >= minExpectedSimilarity || jaroWinklerSim >= minExpectedSimilarity,
                $"Expected at least one similarity score >= {minExpectedSimilarity}. " +
                $"Levenshtein: {levenshteinSim:F2}, Jaro-Winkler: {jaroWinklerSim:F2}");
        }
    }
}