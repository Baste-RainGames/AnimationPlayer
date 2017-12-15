using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SerializedPropertyExtension
{
    /// <summary>
    /// Use to set the underlying object of a serialized property dirty, if you have
    /// edited the object directly through SerializedPropertyHelper.GetTargetObjectOfProperty
    ///
    /// Using property.serializedObject.ApplyModifiedProperties() will apply the changes done through
    /// the property, and discard any changes done directly to the object
    /// </summary>
    public static void SetDirty(this SerializedProperty property)
    {
        var targetObject = property.serializedObject.targetObject;
        EditorUtility.SetDirty(targetObject);
        var asComp = targetObject as Component;
        if (asComp != null)
        {
            EditorSceneManager.MarkSceneDirty(asComp.gameObject.scene);
        }
    }
}