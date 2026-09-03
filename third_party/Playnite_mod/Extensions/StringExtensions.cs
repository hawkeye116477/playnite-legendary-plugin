using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace System
{
    public class CaseInsensitiveCharComparer : EqualityComparer<char>
    {
        public override bool Equals(char x, char y)
        {
            return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
        }

        public override int GetHashCode(char obj)
        {
            return char.ToUpperInvariant(obj).GetHashCode();
        }
    }

    public static partial class StringExtensions
    {
        private static readonly CultureInfo enUSCultInfo = new("en-US", false);
        private const double defaultWinklerWeightThreshold = 0.7; //Winkler's paper used a default value of 0.7
        private const int winklerNumChars = 4; //Size of the prefix to be considered by the Winkler modification.
        private static readonly EqualityComparer<char> charCaseInsensitiveComparer = new CaseInsensitiveCharComparer();

        [GeneratedRegex(@"[™©®]")]
        private static partial Regex RemoveMarksRegex();

        public static string GetMD5(this string s)
        {
            return Convert.ToHexStringLower(s.GetMD5Bytes());
        }

        public static byte[] GetMD5Bytes(this string s)
        {
            return MD5.HashData(Encoding.UTF8.GetBytes(s));
        }

        public static string GetSHA256(this string input)
        {
            return Convert.ToHexStringLower(input.GetSHA256Bytes());
        }

        public static byte[] GetSHA256Bytes(this string input)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(input));
        }

        public static string RemoveMarks(this string str, string remplacement = "")
        {
            if (str.IsNullOrEmpty())
            {
                return str;
            }

            return RemoveMarksRegex().Replace(str, remplacement);
        }

        public static string Format(this string source, params object[] args)
        {
            return string.Format(source, args);
        }

        public static bool IsStartOfStringAcronym(this string acronymStart, string input)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(acronymStart)
                                            || acronymStart.Length < 2 || acronymStart.Length > input.Length)
            {
                return false;
            }

            foreach (var t in acronymStart)
            {
                if (!char.IsLetterOrDigit(t))
                {
                    return false;
                }
            }

            var acronymIndex = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetterOrDigit(input[i]) && (i == 0 || input[i - 1] == ' '))
                {
                    if (char.ToUpperInvariant(input[i]) != char.ToUpperInvariant(acronymStart[acronymIndex]))
                    {
                        return false;
                    }

                    acronymIndex++;
                    // If the acronym index and acronym start length is the same
                    // it means all the characters have been matched
                    if (acronymIndex == acronymStart.Length)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string RemoveUnlessThatEmptiesTheString(string input, string pattern)
        {
            string output = Regex.Replace(input, pattern, string.Empty);

            if (string.IsNullOrWhiteSpace(output))
            {
                return input;
            }

            return output;
        }

        public static string NormalizeGameName(this string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var newName = name;
            newName = newName.RemoveMarks();
            newName = newName.Replace('_', ' ');
            newName = newName.Replace('.', ' ');
            newName = newName.Replace('’', '\'');
            newName = RemoveUnlessThatEmptiesTheString(newName, @"\[.*?\]");
            newName = RemoveUnlessThatEmptiesTheString(newName, @"\(.*?\)");
            newName = Regex.Replace(newName, @"\s*:\s*", ": ");
            newName = Regex.Replace(newName, @"\s+", " ");
            if (Regex.IsMatch(newName, @",\s*The$"))
            {
                newName = "The " + Regex.Replace(newName, @",\s*The$", "", RegexOptions.IgnoreCase);
            }

            return newName.Trim();
        }

        public static bool IsHttpUrl([NotNullWhen(true)] this string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return false;
            }

            return str.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   str.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUri([NotNullWhen(true)] this string? str, UriKind kind)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return false;
            }

            return Uri.IsWellFormedUriString(str, kind);
        }

        public static string UriCombine(this string baseUri, params string[] segments)
        {
            if (baseUri.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            if (segments.Length == 0)
            {
                return baseUri;
            }

            return segments.Aggregate(baseUri, (c, s) => $"{c.TrimEnd('/')}/{s.TrimStart('/')}");
        }

        public static string UriAppendQuery(this string baseUri, string parameter, string value)
        {
            if (baseUri.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            if (baseUri.Contains('?', StringComparison.Ordinal))
            {
                return baseUri.TrimEnd('&') + $"&{parameter}={value}";
            }

            return baseUri.TrimEnd('&') + $"?{parameter}={value}";
        }

        public static int GetLineCount(this string? str)
        {
            if (str is null)
            {
                return 0;
            }

            return str.Count('\n') + 1;
        }

        public static string EndWithDirSeparator(this string source)
        {
            if (source.IsNullOrWhiteSpace())
            {
                return source;
            }

            return source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public static string TrimDirSeparator(this string source)
        {
            if (source.IsNullOrWhiteSpace())
                return source;

            return source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string EndWithUriSeparator(this string source)
        {
            if (source.IsNullOrEmpty())
            {
                return source;
            }

            return source.TrimEnd('/') + '/';
        }

        public static bool ContainsInvariantCulture(this string source, string value, CompareOptions compareOptions)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(source, value, compareOptions) >= 0;
        }

        public static bool ContainsCurrentCulture(this string source, string value, CompareOptions compareOptions)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, value, compareOptions) >= 0;
        }

        public static string Multiply(this string source, int multiplier)
        {
            if (multiplier < 0)
            {
                throw new Exception("String multiplier has to have positive value.");
            }

            if (multiplier == 0 || source.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(multiplier * source.Length);
            for (int i = 0; i < multiplier; i++)
            {
                sb.Append(source);
            }

            return sb.ToString();
        }

        public static int GetLevenshteinDistanceIgnoreCase(this string source, string value)
        {
            return source.GetLevenshteinDistance(value, charCaseInsensitiveComparer);
        }

        public static int GetLevenshteinDistance(this string source, string value)
        {
            return source.GetLevenshteinDistance(value, EqualityComparer<char>.Default);
        }

        //From https://github.com/DanHarltey/Fastenshtein
        /// <summary>
        /// Compares the two values to find the minimum Levenshtein distance.
        /// Thread safe.
        /// </summary>
        /// <returns>Difference. 0 complete match.</returns>
        public static int GetLevenshteinDistance(this string value1, string value2, IEqualityComparer<char> comparer)
        {
            if (value2.Length == 0)
            {
                return value1.Length;
            }

            int[] costs = new int[value2.Length];

            // Add indexing for insertion to first row
            for (int i = 0; i < costs.Length;)
            {
                costs[i] = ++i;
            }

            for (int i = 0; i < value1.Length; i++)
            {
                // cost of the first index
                int cost = i;
                int previousCost = i;

                // cache value for inner loop to avoid index lookup and bonds checking, profiled this is quicker
                char value1Char = value1[i];

                for (int j = 0; j < value2.Length; j++)
                {
                    int currentCost = cost;
                    cost = costs[j];

                    if (!comparer.Equals(value1Char, value2[j]))
                    {
                        if (previousCost < currentCost)
                        {
                            currentCost = previousCost;
                        }

                        if (cost < currentCost)
                        {
                            currentCost = cost;
                        }

                        ++currentCost;
                    }

                    costs[j] = currentCost;
                    previousCost = currentCost;
                }
            }

            return costs[costs.Length - 1];
        }

        //Based on https://gist.github.com/ronnieoverby/2aa19724199df4ec8af6
        public static double GetJaroWinklerSimilarityIgnoreCase(
            this string str, string str2, double winklerWeightThreshold = defaultWinklerWeightThreshold)
        {
            return str.GetJaroWinklerSimilarity(str2, charCaseInsensitiveComparer, winklerWeightThreshold);
        }

        public static double GetJaroWinklerSimilarity(
            this string str, string str2, double winklerWeightThreshold = defaultWinklerWeightThreshold)
        {
            return str.GetJaroWinklerSimilarity(str2, EqualityComparer<char>.Default, winklerWeightThreshold);
        }

        /// <summary>
        /// Returns the Jaro-Winkler similarity between the specified
        /// strings. The distance is symmetric and will fall in the
        /// range 0 (no match) to 1 (perfect match).
        /// </summary>
        /// <param name="str">First String</param>
        /// <param name="str2">Second String</param>
        /// <param name="comparer">Comparer used to determine character equality.</param>
        /// <param name="winklerWeightThreshold">The weight threshold is used to determine whether the similarity score is high enough to consider two strings as a match. Winkler's paper used a default value of 0.7.</param>
        /// <returns>Similarity between the specified strings.</returns>
        public static double GetJaroWinklerSimilarity(
            this string str, string str2, IEqualityComparer<char> comparer, double winklerWeightThreshold = defaultWinklerWeightThreshold)
        {
            var lLen1 = str.Length;
            var lLen2 = str2.Length;
            if (lLen1 == 0)
            {
                return lLen2 == 0 ? 1.0 : 0.0;
            }

            var lSearchRange = Math.Max(0, Math.Max(lLen1, lLen2) / 2 - 1);

            var lMatched1 = new bool[lLen1];
            var lMatched2 = new bool[lLen2];

            var lNumCommon = 0;
            for (var i = 0; i < lLen1; ++i)
            {
                var lStart = Math.Max(0, i - lSearchRange);
                var lEnd = Math.Min(i + lSearchRange + 1, lLen2);
                for (var j = lStart; j < lEnd; ++j)
                {
                    if (lMatched2[j])
                    {
                        continue;
                    }

                    if (!comparer.Equals(str[i], str2[j]))
                    {
                        continue;
                    }

                    lMatched1[i] = true;
                    lMatched2[j] = true;
                    ++lNumCommon;
                    break;
                }
            }

            if (lNumCommon == 0)
            {
                return 0.0;
            }

            var lNumHalfTransposed = 0;
            var k = 0;
            for (var i = 0; i < lLen1; ++i)
            {
                if (!lMatched1[i])
                {
                    continue;
                }

                while (!lMatched2[k])
                {
                    ++k;
                }

                if (!comparer.Equals(str[i], str2[k]))
                {
                    ++lNumHalfTransposed;
                }

                ++k;
            }

            var lNumTransposed = lNumHalfTransposed / 2;
            double lNumCommonD = lNumCommon;
            var lWeight = (lNumCommonD / lLen1
                           + lNumCommonD / lLen2
                           + (lNumCommon - lNumTransposed) / lNumCommonD) / 3.0;

            if (lWeight <= winklerWeightThreshold)
            {
                return lWeight;
            }

            var lMax = Math.Min(winklerNumChars, Math.Min(str.Length, str2.Length));
            var lPos = 0;
            while (lPos < lMax && comparer.Equals(str[lPos], str2[lPos]))
            {
                ++lPos;
            }

            if (lPos == 0)
            {
                return lWeight;
            }

            return lWeight + 0.1 * lPos * (1.0 - lWeight);
        }

        public static char ToUpper(this char source)
        {
            return char.ToUpper(source);
        }

        public static char ToLower(this char source)
        {
            return char.ToLower(source);
        }

        public static bool TryCreateStringFromUtf32(this int code, out string result)
        {
            try
            {
                result = char.ConvertFromUtf32(code);
                return true;
            }
            catch
            {
                result = string.Empty;
                return false;
            }
        }

        public static string TrimEndString(this string source, string value, StringComparison comp = StringComparison.Ordinal)
        {
            if (!source.EndsWith(value, comp))
            {
                return source;
            }

            return source.Remove(source.LastIndexOf(value, comp));
        }
    }
}