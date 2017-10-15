using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Animation_Player
{
    public static class Extensions
    {

        public static IEnumerable<T> FilterByType<T>(this IEnumerable collection) where T : class
        {
            foreach (var element in collection)
            {
                var asT = element as T;
                if(asT != null)
                    yield return asT;
            }
        }
        
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
    public static string PrettyPrint<T>(this IEnumerable<T> array, Func<T, string> printFunc, string elementDivider, bool surroundWithBrackets) {
        if (array == null)
            return "null";

        StringBuilder builder = new StringBuilder();

        if (surroundWithBrackets)
            builder.Append("[");

        bool addedAny = false;
        foreach (T t in array) {
            addedAny = true;
            if (t == null)
                builder.Append("null");
            else if (t is IEnumerable<T>)
                builder.Append(((IEnumerable<T>) t).PrettyPrint(printFunc, elementDivider, surroundWithBrackets));
            else {
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

    public static string PrettyPrint<T>(this IEnumerable<T> array, Func<T, string> printFunc) {
        return PrettyPrint(array, false, printFunc);
    }

    public static string PrettyPrint<T>(this IEnumerable<T> array, bool newLines = false,
                                        Func<T, string> printFunc = null, bool surroundWithBrackets = true) {
        var elementDivider = newLines ? "\n " : ", ";
        return PrettyPrint(array, printFunc, elementDivider, false);
    }
        
    }
}