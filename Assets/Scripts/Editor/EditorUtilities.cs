using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Animation_Player
{
    public static class EditorUtilities
    {
        public static void SetDirty(Component c)
        {
            EditorUtility.SetDirty(c);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }

        public static void DrawIndented(Action drawAction)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical();
            drawAction();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawHorizontal(Action drawAction, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginHorizontal(options);
            drawAction();
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawVertical(Action drawAction, params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(options);
            drawAction();
            EditorGUILayout.EndVertical();
        }

        public static void ExpandArrayByOne<T>(ref T[] array, Func<T> CreateNew)
        {
            Array.Resize(ref array, array.Length + 1);
            array[array.Length - 1] = CreateNew();
        }

        public static void DeleteIndexFromArray<T>(ref T[] array, int index)
        {
            var old = array;
            Array.Resize(ref array, array.Length - 1);

            for (int i = 0; i < old.Length; i++)
            {
                if (i != index)
                    array[i > index ? i - 1 : i] = old[i];
            }
        }

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

        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        private static readonly Dictionary<string, float> buttonClickTime = new Dictionary<string, float>();
        private static GUIStyle areYouSureStyle;

        public static bool AreYouSureButton(string text, string areYouSureText, string uniqueID, float timeout, params GUILayoutOption[] options)
        {
            if (areYouSureStyle == null)
            {
                var buttonTexture = GetReadableCopyOf(GUI.skin.button.normal.background);
                var pixels = buttonTexture.GetPixels();
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] *= new Color(1f, 0f, 0f, 1f);
                var areYouSureTexture = new Texture2D(buttonTexture.width, buttonTexture.height);
                areYouSureTexture.SetPixels(pixels);

                areYouSureStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = {background = areYouSureTexture}
                };
            }

            float timeSinceLastClick = Single.PositiveInfinity;
            float timeFromDict;
            if (buttonClickTime.TryGetValue(uniqueID, out timeFromDict))
                timeSinceLastClick = Time.realtimeSinceStartup - timeFromDict;

            var inAreYouSureMode = timeSinceLastClick < timeout;

            if (inAreYouSureMode)
            {
                var clicked = GUILayout.Button(areYouSureText, areYouSureStyle, options);
                if (clicked)
                    buttonClickTime.Remove(uniqueID);
                return clicked;
            }

            if (GUILayout.Button(text, options))
                buttonClickTime[uniqueID] = Time.realtimeSinceStartup;

            return false;
        }

        // Why is this so hard? It's insane that there's a flag on Texture2D that we can't touch that means
        // "you can't do anything with this texture".
        public static Texture2D GetReadableCopyOf(Texture2D texture)
        {
            // Create a temporary RenderTexture of the same size as the texture
            RenderTexture tmp = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmp);
            // Backup the currently set RenderTexture
            RenderTexture previous = RenderTexture.active;
            // Set the current RenderTexture to the temporary one we created
            RenderTexture.active = tmp;
            // Create a new readable Texture2D to copy the pixels to it
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);
            // Copy the pixels from the RenderTexture to the new Texture
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            // Reset the active RenderTexture
            RenderTexture.active = previous;
            // Release the temporary RenderTexture
            RenderTexture.ReleaseTemporary(tmp);

            // "myTexture2D" now has the same pixels from "texture" and it's readable.
            return myTexture2D;
        }

        public static string TextField(string label, string text, float width)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(width));
            text = EditorGUILayout.TextField(text);
            EditorGUILayout.EndHorizontal();
            return text;
        }

        public static double DoubleField(string label, double value, float width)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(width));
            value = EditorGUILayout.DoubleField(value);
            EditorGUILayout.EndHorizontal();
            return value;
        }

        public static T ObjectField<T>(T obj, params GUILayoutOption[] options) where T : Object
        {
            return (T) EditorGUILayout.ObjectField(obj, typeof(T), false, options);
        }

        public static T ObjectField<T>(string label, T obj, params GUILayoutOption[] options) where T : Object
        {
            return (T) EditorGUILayout.ObjectField(label, obj, typeof(T), false, options);
        }

        public static T ObjectField<T>(string label, T obj, float labelWidth) where T : Object
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            obj = ObjectField(obj);
            EditorGUILayout.EndHorizontal();
            return obj;
        }

        public static T ObjectField<T>(string label, T obj, float labelWidth, float objectSelectWidth) where T : Object
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            obj = ObjectField(obj, GUILayout.Width(objectSelectWidth));
            EditorGUILayout.EndHorizontal();
            return obj;
        }

        public static float FloatField(string label, float value, float width)
        {
            EditorGUILayout.BeginHorizontal();
            LabelWithNoGap(label, width);
            value = EditorGUILayout.FloatField(value);
            EditorGUILayout.EndHorizontal();
            return value;
        }

        public static float FloatField(string label, float value, float width, float floatSelectWidth)
        {
            EditorGUILayout.BeginHorizontal();
            LabelWithNoGap(label, width);
            value = EditorGUILayout.FloatField(value, GUILayout.Width(floatSelectWidth));
            EditorGUILayout.EndHorizontal();
            return value;
        }

        private static void LabelWithNoGap(string label, float width)
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.label, GUILayout.Width(width));
            rect.xMax += 35f; //Unity steals 35 pixels of space between horizontal elements for no fucking reason
            EditorGUI.LabelField(rect, label);
        }

        public static int DrawRightButton(int currentValue, int maxValue)
        {
            var disabled = maxValue == 1 || currentValue == maxValue - 1;

            EditorGUI.BeginDisabledGroup(disabled);
            if (GUILayout.Button("\u2192", GUILayout.Width(24f)))
                currentValue++;
            EditorGUI.EndDisabledGroup();
            return currentValue;
        }

        public static int DrawLeftButton(int currentValue)
        {
            var disabled = currentValue == 0;
            EditorGUI.BeginDisabledGroup(disabled);
            if (GUILayout.Button("\u2190", GUILayout.Width(24f)))
                currentValue--;
            EditorGUI.EndDisabledGroup();
            return currentValue;
        }

        public static Rect ReserveRect(params GUILayoutOption[] options)
        {
            //Note: Using EditorGUILayout.LabelField makes the label extend way past it's width. Probably due to GUILayoutUtility not interacting with it very well?
            GUILayout.Label(string.Empty, options);
            return GUILayoutUtility.GetLastRect();
        }

        /// <summary>
        /// EditorUtilities.RecordUndo and Undo.RegisterCompleteObjectUndo both fails to properly store prefab modifications if the modification is an addition
        /// of an element in a list. They both store the change to Array.size, but fail to store data about the array element, causing it to be a blank item.
        /// (bug reported, accepted)
        /// 
        /// This means that SetDirty is neccessary to actually change the scene. But in order to ensure that Undo works, RegisterCompleteObjectUndo is 
        /// neccessary. Using EditorUtilities.RecordUndo and then SetDirty causes Undo to undo the increase to Array.size, but not to undo the added element itself.
        /// </summary>
        public static void RecordUndo(Component comp, string message)
        {
            if (Application.isPlaying)
                return;
            Undo.RegisterCompleteObjectUndo(comp, message);
            SetDirty(comp);
        }
    }
}