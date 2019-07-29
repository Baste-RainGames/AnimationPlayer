using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class SerializedPropertyHelper
{
    internal static object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');
        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj, element);
            }
        }

        return obj;
    }

    private static object GetValue_Imp(object source, string name)
    {
        if (source == null)
            return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
                return f.GetValue(source);

            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(source, null);

            type = type.BaseType;
        }

        return null;
    }

    private static object GetValue_Imp(object source, string name, int index)
    {
        var enumerable = GetValue_Imp(source, name) as IEnumerable;
        if (enumerable == null)
            return null;
        var enm = enumerable.GetEnumerator();

        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext())
                return null;
        }

        return enm.Current;
    }

    internal static void SetValue<T>(SerializedProperty target, T data, bool applyModifiedProperties = true)
    {
        if (!ValueContainerRegistry.TryGetContainerType(typeof(T), out var containerType))
        {
            var typeName = typeof(T).Name;
            Debug.LogError(
                $"Trying to set a value of type {typeName}, but it's not got a registered container type!\n" +
                $"Please ensure that this type exists: internal class {typeName}Container : ValueContainer<{typeName}> {{}}");
            return;
        }

        ValueContainer<T> dataValueContainer = (ValueContainer<T>) ScriptableObject.CreateInstance(containerType);
        dataValueContainer.t = data;
        var dataSp = new SerializedObject(dataValueContainer).FindProperty("t");

        var dataIter = dataSp.Copy();
        var targetIter = target.Copy();

        bool next;
        do
        {
            CopySerializedProp(dataIter, targetIter);
            next = dataIter.NextVisible(true);
            targetIter.NextVisible(true);
        } while (next);

        if (applyModifiedProperties)
            target.serializedObject.ApplyModifiedProperties();
    }

    //not recursive!
    private static void CopySerializedProp(SerializedProperty from, SerializedProperty to)
    {
        switch (from.propertyType)
        {
          case SerializedPropertyType.Integer:
            to.intValue = from.intValue;
            break;
          case SerializedPropertyType.Boolean:
            to.boolValue = from.boolValue;
            break;
          case SerializedPropertyType.Float:
            to.floatValue = from.floatValue;
            break;
          case SerializedPropertyType.String:
            to.stringValue = from.stringValue;
            break;
          case SerializedPropertyType.Color:
            to.colorValue = from.colorValue;
            break;
          case SerializedPropertyType.ObjectReference:
            to.objectReferenceValue = from.objectReferenceValue;
            break;
          case SerializedPropertyType.LayerMask:
            to.intValue = from.intValue;
            break;
          case SerializedPropertyType.Enum:
            to.enumValueIndex = from.enumValueIndex;
            break;
          case SerializedPropertyType.Vector2:
            to.vector2Value = from.vector2Value;
            break;
          case SerializedPropertyType.Vector3:
            to.vector3Value = from.vector3Value;
            break;
          case SerializedPropertyType.Vector4:
            to.vector4Value = from.vector4Value;
            break;
          case SerializedPropertyType.Rect:
            to.rectValue = from.rectValue;
            break;
          case SerializedPropertyType.ArraySize:
            to.intValue = from.intValue;
            break;
          case SerializedPropertyType.Character:
            to.intValue = from.intValue;
            break;
          case SerializedPropertyType.AnimationCurve:
            to.animationCurveValue = from.animationCurveValue;
            break;
          case SerializedPropertyType.Bounds:
            to.boundsValue = from.boundsValue;
            break;
          case SerializedPropertyType.ExposedReference:
            to.exposedReferenceValue = from.exposedReferenceValue;
            break;
          case SerializedPropertyType.Vector2Int:
            to.vector2IntValue = from.vector2IntValue;
            break;
          case SerializedPropertyType.Vector3Int:
            to.vector3IntValue = from.vector3IntValue;
            break;
          case SerializedPropertyType.RectInt:
            to.rectIntValue = from.rectIntValue;
            break;
          case SerializedPropertyType.BoundsInt:
            to.boundsIntValue = from.boundsIntValue;
            break;
        }
    }
}

internal static class ValueContainerRegistry
{
    private static Dictionary<Type, Type> typeToContainer;

    internal static bool TryGetContainerType(Type type, out Type containerType)
    {
        if (typeToContainer == null)
        {
            typeToContainer = new Dictionary<Type, Type>();

            var allTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(ass => ass.GetTypes());
            var allValueContainers = new List<Type>(
                allTypes
                    .Where(t => t.BaseType != null && t.BaseType.IsGenericType)
                    .Where(t => typeof(ValueContainer<>).IsAssignableFrom(t.BaseType.GetGenericTypeDefinition())));

            foreach (var t in allValueContainers)
            {
                var baseType = t.BaseType; // the base type for eg. IntContainer would be DataContainer<int>
                typeToContainer[baseType.GetGenericArguments()[0]] = t;
            }
        }

        return typeToContainer.TryGetValue(type, out containerType);
    }
}

internal class ValueContainer<T> : ScriptableObject
{
    internal T t;
}

internal class IntContainer : ValueContainer<int> { }

internal class FloatContainer : ValueContainer<float> { }

internal class StringContainer : ValueContainer<string> { }

internal class DoubleContainer : ValueContainer<double> { }

internal class ByteContainer : ValueContainer<byte> { }