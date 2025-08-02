using System.Text;
using System.Text.RegularExpressions;

namespace GadgetTools.Shared.Extensions
{
    /// <summary>
    /// String extension methods
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Check if string is null, empty, or whitespace
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Check if string has value (not null, empty, or whitespace)
        /// </summary>
        public static bool HasValue(this string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Get string or default value if null/empty
        /// </summary>
        public static string OrDefault(this string? value, string defaultValue = "")
        {
            return value.HasValue() ? value! : defaultValue;
        }

        /// <summary>
        /// Truncate string to specified length
        /// </summary>
        public static string Truncate(this string value, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= maxLength) return value;

            return value.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// Remove HTML tags from string
        /// </summary>
        public static string RemoveHtmlTags(this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            return Regex.Replace(value, "<.*?>", string.Empty);
        }

        /// <summary>
        /// Convert to title case
        /// </summary>
        public static string ToTitleCase(this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (result.Length > 0) result.Append(' ');
                
                if (word.Length > 0)
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                    {
                        result.Append(word.Substring(1).ToLower());
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Contains ignore case
        /// </summary>
        public static bool ContainsIgnoreCase(this string source, string value)
        {
            if (source == null || value == null) return false;
            return source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Equals ignore case
        /// </summary>
        public static bool EqualsIgnoreCase(this string source, string value)
        {
            return string.Equals(source, value, StringComparison.OrdinalIgnoreCase);
        }
    }
}