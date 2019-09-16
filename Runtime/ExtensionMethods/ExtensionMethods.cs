using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    internal static class ExtensionMethods
    {
        ///  <summary>
        ///  Returns a pretty string representation of an Array. Or anything else that's IEnumerable. Like a list or whatever.
        ///  Does basic [element,element] formatting, and also does recursive calls to inner lists. You can also give it a functon to
        ///  do even prettier printing, usefull to get IE. a GameObject's name instead of "name (UnityEngine.GameObject)". If the function
        ///  isn't supplied, toString is used.
        ///  Also turns null into "null" instead of ""
        ///  Will cause a stack overflow if you put list A in list B and list B in list A, but you wouldn't do that, would you?
        ///  </summary>
        ///  <param name="array">Some array</param>
        ///  <param name="printFunc">An optional function that you can use in place of ToString</param>
        /// <param name="elementDivider">The thing that should be put between the array elements. Defaults to a comma.</param>
        /// <param name="surroundWithBrackets">Should the array be surrounded with [] or not?</param>
        /// <typeparam name="T">The type of the array</typeparam>
        /// <returns>A string representation of the array that's easy to read</returns>
        public static string PrettyPrint<T>(this IEnumerable<T> array, Func<T, string> printFunc, string elementDivider, bool surroundWithBrackets)
        {
            if (array == null)
                return "null";

            StringBuilder builder = new StringBuilder();

            if (surroundWithBrackets)
                builder.Append("[");

            bool addedAny = false;
            foreach (T t in array)
            {
                addedAny = true;
                if (t == null)
                    builder.Append("null");
                else if (t is IEnumerable<T>)
                    builder.Append(((IEnumerable<T>) t).PrettyPrint(printFunc, elementDivider, surroundWithBrackets));
                else
                {
                    if (printFunc == null)
                        builder.Append(t.ToString());
                    else
                        builder.Append(printFunc(t));
                }

                builder.Append(elementDivider);
            }

            if (addedAny) //removes the trailing ", "
                builder.Remove(builder.Length - 2, 2);
            if (surroundWithBrackets)
                builder.Append("]");

            return builder.ToString();
        }

        public static string PrettyPrint<T>(this IEnumerable<T> array, Func<T, string> printFunc)
        {
            return PrettyPrint(array, false, printFunc);
        }

        public static string PrettyPrint<T>(this IEnumerable<T> array,            bool newLines             = false,
                                            Func<T, string>     printFunc = null, bool surroundWithBrackets = true)
        {
            var elementDivider = newLines ? "\n " : ", ";
            return PrettyPrint(array, printFunc, elementDivider, false);
        }

        public static void Swap<T>(this IList<T> list, int idx1, int idx2)
        {
            var temp = list[idx1];
            list[idx1] = list[idx2];
            list[idx2] = temp;
        }

        public static int GetRandomIdx<T>(this IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list), "Can't get random idx from null list!");
            if (list.Count == 0)
                throw new ArgumentException("Can't get random idx from empty list!", nameof(list));
            return UnityEngine.Random.Range(0, list.Count);
        }

        public static T GetRandom<T>(this IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list), "Can't get random from null list!");
            if (list.Count == 0)
                throw new ArgumentException("Can't get random from empty list!", nameof(list));
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T EnsureComponent<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponent<T>();
            if (t == null)
                t = obj.AddComponent<T>();
            return t;
        }

        public static T GetIfInBounds<T>(this IList<T> arr, int index)
        {
            if (!arr.IsInBounds(index))
                return default;
            return arr[index];
        }

        public static bool IsInBounds<T>(this IList<T> arr, int index)
        {
            if (arr == null)
                return false;
            if (arr.Count == 0)
                return false;
            return index >= 0 && index < arr.Count;
        }

        public static V GetOrAdd<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            if (dict.TryGetValue(key, out var value))
                return value;

            return dict[key] = new V();
        }

        public static float Duration(this AnimationCurve curve)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }

            if (curve.keys.Length == 0)
            {
                return 0;
            }

            return curve[curve.length - 1].time - curve[0].time;
        }

        public static void EnsureContains<T>(this List<T> list, T element)
        {
            if (!list.Contains(element))
                list.Add(element);
        }
    }
}