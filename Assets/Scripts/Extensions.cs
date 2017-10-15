using System.Collections;
using System.Collections.Generic;

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
        
    }
}