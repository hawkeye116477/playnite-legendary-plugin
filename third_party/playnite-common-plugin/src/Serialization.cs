using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommonPlugin
{
    public static class Serialization
    {
        private static readonly JsonSerializerOptions JsonSerializerSettings = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        public static T? FromJson<T>(string json) where T : class
        {
            return JsonSerializer.Deserialize<T>(json, JsonSerializerSettings);
        }
        
        public static bool TryFromJson<T>(string json, out T? deserialized) where T : class
        {
            try
            {
                deserialized = JsonSerializer.Deserialize<T>(json, JsonSerializerSettings);
                return true;
            }
            catch (Exception e)
            {
                deserialized = null;
                return false;
            }
        }

        public static string ToJson(object obj, bool formatted = false, JsonSerializerOptions? options = null)
        {
            options ??= new JsonSerializerOptions
            {
                WriteIndented = formatted,
                IncludeFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(obj, options);
        }

        /// <summary>
        /// Perform a deep copy of the object, using Json as a serialisation method.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T GetClone<T>(this T source)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, JsonSerializerSettings)) ??
                   throw new Exception("Failed to clone object via serialization");
            ;
        }
    }
}