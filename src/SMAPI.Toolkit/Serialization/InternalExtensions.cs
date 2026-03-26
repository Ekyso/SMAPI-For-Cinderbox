using System;
using Newtonsoft.Json.Linq;

namespace StardewModdingAPI.Toolkit.Serialization;

/// <summary>Provides extension methods for parsing JSON.</summary>
public static class JsonExtensions
{
    /// <param name="obj">The JSON object to search.</param>
    extension(JObject obj)
    {
        /// <summary>Get a JSON field value from a case-insensitive field name. This will check for an exact match first, then search without case sensitivity.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="fieldName">The field name.</param>
        public T? ValueIgnoreCase<T>(string fieldName)
        {
            JToken? token = obj.GetValue(fieldName, StringComparison.OrdinalIgnoreCase);
            return token != null
                ? token.Value<T>()
                : default;
        }
    }
}
