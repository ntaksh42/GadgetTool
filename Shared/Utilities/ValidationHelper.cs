using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace GadgetTools.Shared.Utilities
{
    /// <summary>
    /// Validation helper utilities
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validate object using data annotations
        /// </summary>
        public static ValidationResult ValidateObject(object obj)
        {
            if (obj == null) return new ValidationResult(false, "Object is null");

            var context = new ValidationContext(obj);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            
            var isValid = Validator.TryValidateObject(obj, context, results, true);
            
            return new ValidationResult(isValid, results.Select(r => r.ErrorMessage ?? "Unknown error"));
        }

        /// <summary>
        /// Validate email address
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, pattern);
        }

        /// <summary>
        /// Validate URL
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var result) 
                && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Check if string contains only alphanumeric characters
        /// </summary>
        public static bool IsAlphanumeric(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.All(char.IsLetterOrDigit);
        }

        /// <summary>
        /// Check if string is a valid identifier (alphanumeric + underscore, starts with letter)
        /// </summary>
        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            var pattern = @"^[a-zA-Z][a-zA-Z0-9_]*$";
            return Regex.IsMatch(value, pattern);
        }

        /// <summary>
        /// Validation result
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; }
            public IEnumerable<string> Errors { get; }

            public ValidationResult(bool isValid, IEnumerable<string> errors)
            {
                IsValid = isValid;
                Errors = errors ?? Enumerable.Empty<string>();
            }

            public ValidationResult(bool isValid, string error = "")
                : this(isValid, string.IsNullOrEmpty(error) ? Enumerable.Empty<string>() : new[] { error })
            {
            }

            public string GetErrorMessage()
            {
                return string.Join(Environment.NewLine, Errors);
            }
        }
    }
}