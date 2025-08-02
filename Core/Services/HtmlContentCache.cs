using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GadgetTools.Core.Services
{
    public class HtmlContentCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
        private readonly int _maxCacheSize = 100;

        private static readonly Lazy<HtmlContentCache> _instance = new(() => new HtmlContentCache());
        public static HtmlContentCache Instance => _instance.Value;

        private HtmlContentCache() { }

        public bool TryGetCachedContent(string key, out string? htmlContent)
        {
            htmlContent = null;

            if (string.IsNullOrEmpty(key))
                return false;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow - entry.CreatedAt <= _cacheExpiration)
                {
                    entry.LastAccessed = DateTime.UtcNow;
                    htmlContent = entry.Content;
                    return true;
                }
                else
                {
                    // Remove expired entry
                    _cache.TryRemove(key, out _);
                }
            }

            return false;
        }

        public void CacheContent(string key, string htmlContent)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(htmlContent))
                return;

            // Enforce cache size limit
            if (_cache.Count >= _maxCacheSize)
            {
                CleanupOldEntries();
            }

            var entry = new CacheEntry
            {
                Content = htmlContent,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow
            };

            _cache.AddOrUpdate(key, entry, (k, v) => entry);
        }

        public void ClearCache()
        {
            _cache.Clear();
            System.Diagnostics.Debug.WriteLine("HTML content cache cleared");
        }

        public int GetCacheSize()
        {
            return _cache.Count;
        }

        public void RemoveExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => now - kvp.Value.CreatedAt > _cacheExpiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Removed {expiredKeys.Count} expired cache entries");
            }
        }

        private void CleanupOldEntries()
        {
            // Remove oldest entries to make room
            var entriesToRemove = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(_maxCacheSize / 4) // Remove 25% of cache
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in entriesToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            System.Diagnostics.Debug.WriteLine($"Cleaned up {entriesToRemove.Count} old cache entries");
        }

        public static string GenerateCacheKey(int workItemId, DateTime? lastModified = null)
        {
            var baseKey = $"workitem_{workItemId}";
            if (lastModified.HasValue)
            {
                baseKey += $"_{lastModified.Value:yyyyMMddHHmmss}";
            }
            return baseKey;
        }

        public static string GenerateListCacheKey(string organization, string project, int count, DateTime? lastQuery = null)
        {
            var baseKey = $"list_{organization}_{project}_{count}";
            if (lastQuery.HasValue)
            {
                baseKey += $"_{lastQuery.Value:yyyyMMddHHmmss}";
            }
            return baseKey;
        }

        private class CacheEntry
        {
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessed { get; set; }
        }
    }
}