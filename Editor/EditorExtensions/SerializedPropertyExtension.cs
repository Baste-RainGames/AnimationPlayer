using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class SerializedPropertyExtension
{
    /// <summary>
    /// Use to set the underlying object of a serialized property dirty, if you have
    /// edited the object directly through SerializedPropertyHelper.GetTargetObjectOfProperty
    ///
    /// Using property.serializedObject.ApplyModifiedProperties() will apply the changes done through
    /// the property, and discard any changes done directly to the object
    /// </summary>
    internal static void SetUnderlyingObjectDirty(this SerializedProperty property)
    {
        var targetObject = property.serializedObject.targetObject;
        EditorUtility.SetDirty(targetObject);
        var asComp = targetObject as Component;
        if (asComp != null)
        {
            EditorSceneManager.MarkSceneDirty(asComp.gameObject.scene);
        }
    }

    internal static SerializedProperty FindSiblingAttribute(this SerializedProperty prop, string siblingName) {
        var oldPath = prop.propertyPath;

        var pathSplit = oldPath.Split('.');

        StringBuilder siblingBuilder = new StringBuilder();
        for (int i = 0; i < pathSplit.Length; i++) {
            if (i < pathSplit.Length - 1) {
                siblingBuilder.Append(pathSplit[i]);
                siblingBuilder.Append('.');
            }
            else {
                siblingBuilder.Append(siblingName);
            }

        }

        prop = prop.serializedObject.FindProperty(siblingBuilder.ToString());
        return prop;
    }

    internal static SerializedProperty AppendToArray(this SerializedProperty prop) {
        if (!prop.isArray) {
            Debug.LogError($"Trying to expand the array size of the property {prop.displayName}, but it's not an array");
            return null;
        }

        prop.arraySize++;
        return prop.GetArrayElementAtIndex(prop.arraySize - 1);
    }

    internal static SerializedProperty InsertIntoArray(this SerializedProperty prop, int atIndex) {
        if (!prop.isArray) {
            Debug.LogError($"Trying to expand the array size of the property {prop.displayName}, but it's not an array");
            return null;
        }

        prop.InsertArrayElementAtIndex(atIndex);
        return prop.GetArrayElementAtIndex(atIndex);
    }

    internal static IEnumerable<SerializedProperty> IterateArray(this SerializedProperty prop) {
        if (!prop.isArray) {
            Debug.LogError($"Trying to iterate the property {prop.displayName}, but it's not an array");
            yield break;
        }

        for (int i = 0; i < prop.arraySize; i++) {
            yield return prop.GetArrayElementAtIndex(i);
        }
    }

    internal static IEnumerable<(int, SerializedProperty)> IterateArrayWithIndex(this SerializedProperty prop) {
        if (!prop.isArray) {
            Debug.LogError($"Trying to iterate the property {prop.displayName}, but it's not an array");
            yield break;
        }

        for (int i = 0; i < prop.arraySize; i++) {
            yield return (i, prop.GetArrayElementAtIndex(i));
        }
    }

    internal static IEnumerable<(int, SerializedProperty)> IterateArrayWithIndex_Reverse(this SerializedProperty prop) {
        if (!prop.isArray) {
            Debug.LogError($"Trying to iterate the property {prop.displayName}, but it's not an array");
            yield break;
        }

        for (int i = prop.arraySize - 1; i >= 0; i--) {
            yield return (i, prop.GetArrayElementAtIndex(i));
        }
    }

    internal static string ListProperties(this SerializedProperty property, bool includeInvisible = false) {
        StringBuilder sb = new StringBuilder();

        var start = property.Copy();
        var end   = property.Copy();

        end.Next(false);

        ListProperties(start, end, includeInvisible, sb, 0);

        return sb.ToString();
    }

    private static void ListProperties(SerializedProperty startProp, SerializedProperty endProp, bool includeInvisible, StringBuilder sb, int indent) {
        var currentProp = startProp;

        bool cont = true;
        do {
            for (int i = 0; i < indent; i++) {
                sb.Append(' ');
                sb.Append(' ');
            }

            sb.Append(currentProp.displayName);
            sb.Append(": ");
            sb.Append(PrintValue(currentProp));
            sb.Append('\n');

            var hasChildren = includeInvisible ? currentProp.hasChildren : currentProp.hasVisibleChildren;

            if (hasChildren) {
                var childIterator = currentProp.Copy();
                var childEnd = currentProp.Copy();

                if (includeInvisible)
                    childIterator.Next(true);
                else
                    childIterator.NextVisible(true);

                childEnd.Next(false);

                ListProperties(childIterator, childEnd, includeInvisible, sb, indent + 1);
            }

            cont = currentProp.Next(false);
        } while (cont && currentProp.propertyPath != endProp.propertyPath);
    }

    internal static string PrintValue(this SerializedProperty prop) {
        switch (prop.propertyType) {
            case SerializedPropertyType.Generic:
                return prop.type ?? "Generic?";
            case SerializedPropertyType.Integer:
                return prop.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return prop.boolValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Float:
                return prop.floatValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return prop.stringValue;
            case SerializedPropertyType.Color:
                return prop.colorValue.ToString();
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue == null ? "null" : prop.objectReferenceValue.ToString();
            case SerializedPropertyType.LayerMask:
                return ((LayerMask) prop.intValue).ToString();
            case SerializedPropertyType.Enum:
                return prop.enumValueIndex.ToString();
            case SerializedPropertyType.Vector2:
                return prop.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return prop.vector3Value.ToString();
            case SerializedPropertyType.Vector4:
                return prop.vector4Value.ToString();
            case SerializedPropertyType.Rect:
                return prop.rectValue.ToString();
            case SerializedPropertyType.ArraySize:
                return "Array size = " + prop.intValue;
            case SerializedPropertyType.Character:
                return $"{(char) prop.intValue}";
            case SerializedPropertyType.AnimationCurve:
                return "Animation Curve";
            case SerializedPropertyType.Bounds:
                return prop.boundsValue.ToString();
            case SerializedPropertyType.Gradient:
                return "Where's gradient stored?";
            case SerializedPropertyType.Quaternion:
                return prop.quaternionValue.ToString();
            case SerializedPropertyType.ExposedReference:
                return prop.exposedReferenceValue.ToString();
            case SerializedPropertyType.FixedBufferSize:
                return prop.fixedBufferSize.ToString();
            case SerializedPropertyType.Vector2Int:
                return prop.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int:
                return prop.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt:
                return prop.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt:
                return prop.boundsIntValue.ToString();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}