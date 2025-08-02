using System.Collections.ObjectModel;

namespace GadgetTools.Shared.Extensions
{
    /// <summary>
    /// Collection extension methods
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Add range of items to ObservableCollection
        /// </summary>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) throw new ArgumentNullException(nameof(items));

            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// Replace all items in ObservableCollection
        /// </summary>
        public static void ReplaceAll<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) throw new ArgumentNullException(nameof(items));

            collection.Clear();
            collection.AddRange(items);
        }

        /// <summary>
        /// Check if collection is null or empty
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
        {
            return collection == null || !collection.Any();
        }

        /// <summary>
        /// Safe foreach that handles null collections
        /// </summary>
        public static void SafeForEach<T>(this IEnumerable<T>? collection, Action<T> action)
        {
            if (collection == null || action == null) return;

            foreach (var item in collection)
            {
                action(item);
            }
        }

        /// <summary>
        /// Get items in chunks
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (chunkSize <= 0) throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));

            var list = source.ToList();
            for (int i = 0; i < list.Count; i += chunkSize)
            {
                yield return list.Skip(i).Take(chunkSize);
            }
        }
    }
}