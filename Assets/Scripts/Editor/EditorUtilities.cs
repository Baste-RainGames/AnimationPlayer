using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class EditorUtilities
{
    public static void SetDirty(Component c)
    {
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }

    public static void ExpandArrayByOne<T>(ref T[] array, Func<T> CreateNew)
    {
        Array.Resize(ref array, array.Length + 1);
        array[array.Length - 1] = CreateNew();
    }

    public static T ObjectField<T>(string label, T obj) where T : Object
    {
        return (T) EditorGUILayout.ObjectField(label, obj, typeof(T), false);
    }

    public static T ObjectField<T>(T obj) where T : Object
    {
        return (T) EditorGUILayout.ObjectField(obj, typeof(T), false);
    }

    #region splitter

    //Editor splitter, taken from http://answers.unity3d.com/questions/216584/horizontal-line.html
    static EditorUtilities()
    {
        splitter = new GUIStyle
        {
            normal =
            {
                background = EditorGUIUtility.whiteTexture
            },
            stretchWidth = true,
            margin = new RectOffset(0, 0, 7, 7)
        };
    }

    private static readonly GUIStyle splitter;
    private static readonly Color splitterColor = EditorGUIUtility.isProSkin ? new Color(0.157f, 0.157f, 0.157f) : new Color(0.5f, 0.5f, 0.5f);

    // GUILayout Style
    public static void Splitter(Color rgb, float thickness = 1)
    {
        Rect position = GUILayoutUtility.GetRect(GUIContent.none, splitter, GUILayout.Height(thickness));

        if (Event.current.type == EventType.Repaint)
        {
            Color restoreColor = GUI.color;
            GUI.color = rgb;
            splitter.Draw(position, false, false, false, false);
            GUI.color = restoreColor;
        }
    }

    public static void Splitter(float thickness, GUIStyle splitterStyle)
    {
        Rect position = GUILayoutUtility.GetRect(GUIContent.none, splitterStyle, GUILayout.Height(thickness));

        if (Event.current.type == EventType.Repaint)
        {
            Color restoreColor = GUI.color;
            GUI.color = splitterColor;
            splitterStyle.Draw(position, false, false, false, false);
            GUI.color = restoreColor;
        }
    }

    public static void Splitter(float thickness = 1)
    {
        Splitter(thickness, splitter);
    }

    // GUI Style
    public static void Splitter(Rect position)
    {
        if (Event.current.type == EventType.Repaint)
        {
            Color restoreColor = GUI.color;
            GUI.color = splitterColor;
            splitter.Draw(position, false, false, false, false);
            GUI.color = restoreColor;
        }
    }

    #endregion

    public static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width*height];
 
        for(int i = 0; i < pix.Length; i++)
            pix[i] = col;
 
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
 
        return result;
    }
}