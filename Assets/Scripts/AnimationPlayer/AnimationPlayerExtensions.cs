using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Animation_Player
{
    public static class AnimationPlayerExtensions
    {

        public static IEnumerable<T> FilterByType<T>(this IEnumerable collection) where T : class
        {
            foreach (var element in collection)
            {
                var asT = element as T;
                if (asT != null)
                    yield return asT;
            }
        }

        public static T EnsureComponent<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponent<T>();
            if (t == null)
                t = obj.AddComponent<T>();
            return t;
        }

        public static bool IsInBounds<T>(this T[] arr, int index)
        {
            if (arr == null)
                return false;
            if (arr.Length == 0)
                return false;
            return index >= 0 && index < arr.Length;
        }

        public static bool IsInBounds<T>(this List<T> arr, int index)
        {
            if (arr == null)
                return false;
            if (arr.Count == 0)
                return false;
            return index >= 0 && index < arr.Count;
        }

        public static V GetOrAdd<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            V value;
            if (dict.TryGetValue(key, out value))
                return value;

            return dict[key] = new V();
        }
    }
}