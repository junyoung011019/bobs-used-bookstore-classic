using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Bookstore.Data
{
    public sealed class BookstoreConfiguration
    {
        private static BookstoreConfiguration? _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        public static void Initialize(IConfiguration configuration)
        {
            lock (_lock)
            {
                _instance = new BookstoreConfiguration();

                foreach (var kvp in configuration.AsEnumerable())
                {
                    if (kvp.Value != null)
                        _instance._appSettings[kvp.Key] = kvp.Value;
                }

                var connStrings = configuration.GetSection("ConnectionStrings");
                foreach (var child in connStrings.GetChildren())
                {
                    if (child.Value != null)
                        _instance._connectionStrings[child.Key] = child.Value;
                }
            }
        }

        private static BookstoreConfiguration Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("BookstoreConfiguration has not been initialized. Call Initialize() first.");
                return _instance;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            Instance._appSettings.TryGetValue(key, out var value);
            return value ?? string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            Instance._connectionStrings.TryGetValue(key, out var value);
            return value ?? string.Empty;
        }
    }
}
