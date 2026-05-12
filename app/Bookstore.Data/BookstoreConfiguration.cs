using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>();

        public static void Initialize(IConfiguration configuration)
        {
            foreach (var item in configuration.AsEnumerable())
            {
                if (item.Value != null)
                {
                    // Map IConfiguration's ":" separator to "/" for backward compatibility
                    var key = item.Key.Replace(":", "/");
                    _appSettings[key] = item.Value;
                }
            }

            var connectionStrings = configuration.GetSection("ConnectionStrings");
            foreach (var cs in connectionStrings.GetChildren())
            {
                _connectionStrings[cs.Key] = cs.Value ?? string.Empty;
            }

            // Allow environment variable overrides (env vars use "__" as separator)
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                var key = entry.Key?.ToString();
                var value = entry.Value?.ToString();
                if (key != null && value != null)
                {
                    _appSettings[key.Replace("__", "/")] = value;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            _appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            return _appSettings.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            _connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            return _connectionStrings.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
