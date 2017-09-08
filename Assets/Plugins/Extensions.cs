using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using UnityEngine;

public static class Extensions {

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
