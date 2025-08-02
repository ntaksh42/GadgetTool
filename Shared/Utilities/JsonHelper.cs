using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GadgetTools.Shared.Utilities
{
    /// <summary>
    /// JSON serialization helper
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings DefaultSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// Serialize object to JSON string
        /// </summary>
        public static string Serialize<T>(T obj, JsonSerializerSettings? settings = null)
        {
            if (obj == null) return "null";
            
            return JsonConvert.SerializeObject(obj, settings ?? DefaultSettings);
        }

        /// <summary>
        /// Deserialize JSON string to object
        /// </summary>
        public static T? Deserialize<T>(string json, JsonSerializerSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;

            try
            {
                return JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        /// <summary>
        /// Try deserialize JSON string to object
        /// </summary>
        public static bool TryDeserialize<T>(string json, out T? result, JsonSerializerSettings? settings = null)
        {
            result = default;
            
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                result = JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Check if string is valid JSON
        /// </summary>
        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                JsonConvert.DeserializeObject(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Deep clone object using JSON serialization
        /// </summary>
        public static T? DeepClone<T>(T obj)
        {
            if (obj == null) return default;

            var json = Serialize(obj);
            return Deserialize<T>(json);
        }
    }
}