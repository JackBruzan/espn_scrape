using System.Text;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Helper service for string matching algorithms used in player matching
    /// </summary>
    public static class StringMatchingAlgorithms
    {
        /// <summary>
        /// Calculate Levenshtein distance between two strings
        /// </summary>
        /// <param name="s1">First string</param>
        /// <param name="s2">Second string</param>
        /// <returns>Edit distance between the strings</returns>
        public static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            // Normalize strings for comparison
            s1 = NormalizeName(s1);
            s2 = NormalizeName(s2);

            var matrix = new int[s1.Length + 1, s2.Length + 1];

            // Initialize first row and column
            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            // Fill matrix
            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }

        /// <summary>
        /// Calculate similarity score based on Levenshtein distance (0.0 to 1.0)
        /// </summary>
        /// <param name="s1">First string</param>
        /// <param name="s2">Second string</param>
        /// <returns>Similarity score where 1.0 is exact match</returns>
        public static double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int maxLength = Math.Max(s1.Length, s2.Length);
            int distance = LevenshteinDistance(s1, s2);

            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// Calculate Jaro-Winkler similarity score
        /// </summary>
        /// <param name="s1">First string</param>
        /// <param name="s2">Second string</param>
        /// <returns>Jaro-Winkler similarity score (0.0 to 1.0)</returns>
        public static double JaroWinklerSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            s1 = NormalizeName(s1);
            s2 = NormalizeName(s2);

            if (s1 == s2) return 1.0;

            double jaro = CalculateJaro(s1, s2);

            // Jaro-Winkler adds prefix bonus
            int prefixLength = GetCommonPrefixLength(s1, s2, Math.Min(4, Math.Min(s1.Length, s2.Length)));
            return jaro + (0.1 * prefixLength * (1 - jaro));
        }

        /// <summary>
        /// Generate Soundex code for phonetic matching
        /// </summary>
        /// <param name="name">Name to encode</param>
        /// <returns>Soundex code</returns>
        public static string Soundex(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            name = NormalizeName(name).ToUpper();
            if (name.Length == 0) return string.Empty;

            var soundex = new StringBuilder();
            soundex.Append(name[0]);

            // Soundex mapping
            var soundexMap = new Dictionary<char, char>
            {
                {'B', '1'}, {'F', '1'}, {'P', '1'}, {'V', '1'},
                {'C', '2'}, {'G', '2'}, {'J', '2'}, {'K', '2'}, {'Q', '2'}, {'S', '2'}, {'X', '2'}, {'Z', '2'},
                {'D', '3'}, {'T', '3'},
                {'L', '4'},
                {'M', '5'}, {'N', '5'},
                {'R', '6'}
            };

            char? lastCode = null;
            for (int i = 1; i < name.Length && soundex.Length < 4; i++)
            {
                if (soundexMap.TryGetValue(name[i], out char code))
                {
                    if (code != lastCode)
                    {
                        soundex.Append(code);
                        lastCode = code;
                    }
                }
                else
                {
                    lastCode = null;
                }
            }

            // Pad with zeros
            while (soundex.Length < 4)
                soundex.Append('0');

            return soundex.ToString();
        }

        /// <summary>
        /// Check if two names are phonetically similar using Soundex
        /// </summary>
        /// <param name="name1">First name</param>
        /// <param name="name2">Second name</param>
        /// <returns>True if phonetically similar</returns>
        public static bool ArePhoneticallySimilar(string name1, string name2)
        {
            return Soundex(name1) == Soundex(name2);
        }

        /// <summary>
        /// Normalize a name for comparison (remove punctuation, extra spaces, etc.)
        /// </summary>
        /// <param name="name">Name to normalize</param>
        /// <returns>Normalized name</returns>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            // Remove common suffixes and prefixes
            name = name.Trim();

            // Handle common name variations
            var replacements = new Dictionary<string, string>
            {
                { "Jr.", "" }, { "Sr.", "" }, { "III", "" }, { "II", "" },
                { ".", "" }, { "'", "" }, { "-", " " }
            };

            foreach (var replacement in replacements)
            {
                name = name.Replace(replacement.Key, replacement.Value);
            }

            // Normalize multiple spaces to single space
            while (name.Contains("  "))
            {
                name = name.Replace("  ", " ");
            }

            return name.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Extract initials from a name
        /// </summary>
        /// <param name="name">Full name</param>
        /// <returns>Initials (e.g., "John Smith" -> "JS")</returns>
        public static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            // Don't fully normalize, but handle hyphens and basic cleanup
            name = name.Replace("-", " ").Replace(".", "");
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var initials = new StringBuilder();

            foreach (var part in parts)
            {
                if (part.Length > 0 && char.IsLetter(part[0]))
                {
                    initials.Append(char.ToUpper(part[0]));
                }
            }

            return initials.ToString();
        }

        /// <summary>
        /// Check if names could be variations of each other (nickname matching)
        /// </summary>
        /// <param name="name1">First name</param>
        /// <param name="name2">Second name</param>
        /// <returns>True if they could be name variations</returns>
        public static bool AreNameVariations(string name1, string name2)
        {
            // Common nickname mappings
            var nicknames = new Dictionary<string, string[]>
            {
                { "anthony", new[] { "tony" } },
                { "christopher", new[] { "chris", "kit" } },
                { "daniel", new[] { "dan", "danny" } },
                { "david", new[] { "dave", "davey" } },
                { "edward", new[] { "ed", "eddie", "ted" } },
                { "eugene", new[] { "gene" } },
                { "frederick", new[] { "fred", "freddy" } },
                { "gregory", new[] { "greg" } },
                { "james", new[] { "jim", "jimmy", "jamie" } },
                { "jeffrey", new[] { "jeff" } },
                { "joseph", new[] { "joe", "joey" } },
                { "joshua", new[] { "josh" } },
                { "kenneth", new[] { "ken", "kenny" } },
                { "matthew", new[] { "matt" } },
                { "michael", new[] { "mike", "mickey" } },
                { "nicholas", new[] { "nick", "nicky" } },
                { "patrick", new[] { "pat", "paddy" } },
                { "richard", new[] { "rick", "ricky", "dick" } },
                { "robert", new[] { "rob", "bob", "bobby" } },
                { "stephen", new[] { "steve", "stevie" } },
                { "theodore", new[] { "ted", "teddy" } },
                { "thomas", new[] { "tom", "tommy" } },
                { "william", new[] { "will", "bill", "billy" } },
                { "zachary", new[] { "zach" } }
            };

            name1 = NormalizeName(name1);
            name2 = NormalizeName(name2);

            // Check direct mappings both ways
            foreach (var nickname in nicknames)
            {
                if ((nickname.Key == name1 && nickname.Value.Contains(name2)) ||
                    (nickname.Key == name2 && nickname.Value.Contains(name1)))
                {
                    return true;
                }
            }

            return false;
        }

        private static double CalculateJaro(string s1, string s2)
        {
            int matchWindow = Math.Max(s1.Length, s2.Length) / 2 - 1;
            if (matchWindow < 0) matchWindow = 0;

            bool[] s1Matches = new bool[s1.Length];
            bool[] s2Matches = new bool[s2.Length];

            int matches = 0;
            int transpositions = 0;

            // Identify matches
            for (int i = 0; i < s1.Length; i++)
            {
                int start = Math.Max(0, i - matchWindow);
                int end = Math.Min(i + matchWindow + 1, s2.Length);

                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j] || s1[i] != s2[j]) continue;
                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0;

            // Count transpositions
            int k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (s1[i] != s2[k]) transpositions++;
                k++;
            }

            return ((double)matches / s1.Length + (double)matches / s2.Length +
                   (matches - transpositions / 2.0) / matches) / 3.0;
        }

        private static int GetCommonPrefixLength(string s1, string s2, int maxLength)
        {
            int prefixLength = 0;
            for (int i = 0; i < maxLength && i < s1.Length && i < s2.Length; i++)
            {
                if (s1[i] == s2[i])
                    prefixLength++;
                else
                    break;
            }
            return prefixLength;
        }
    }
}