using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor : Editor
{
    private AnimationPlayer animationPlayer;
    private int _selectedLayer;
    private int selectedLayer
    {
        get { return _selectedLayer; }
        set
        {
            _selectedLayer = value;
            SetPersistedSelectedLayer(animationPlayer.GetInstanceID(), value);
        }
    }

    void OnEnable()
    {
        animationPlayer = (AnimationPlayer) target;
        animationPlayer.editTimeUpdateCallback -= Repaint;
        animationPlayer.editTimeUpdateCallback += Repaint;

        _selectedLayer = GetPersistedSelectedLayer(animationPlayer.GetInstanceID());
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(30f);

        var numLayers = animationPlayer.layers.Length;
        if (animationPlayer.layers == null || numLayers == 0)
        {
            EditorGUILayout.LabelField("No layers in the animation player!");
            if (GUILayout.Button("Fix that!"))
            {
                animationPlayer.layers = new AnimationLayer[1];
                animationPlayer.layers[0] = AnimationLayer.CreateLayer();
            }
            return;
        }

        selectedLayer = DrawLayerSelection(numLayers);
        
        GUILayout.Space(20f);
        
        DrawSelectedLayer();

        if (!Application.isPlaying)
            return;

        for (int i = animationPlayer.GetClipCount() - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Snap to " + i))
            {
                animationPlayer.SnapTo(i, selectedLayer);
            }
            if (GUILayout.Button("Blend to " + i + " over .5 secs"))
            {
                animationPlayer.Play(i, AnimTransition.Linear(.5f), selectedLayer);
            }
            if (GUILayout.Button("Blend to " + i + " using default transition"))
            {
                animationPlayer.Play(i, selectedLayer);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetCurrentPlayingClip(selectedLayer));
        for (int i = animationPlayer.GetClipCount() - 1; i >= 0; i--)
        {
            EditorGUILayout.LabelField("weigh for " + i + ": " + animationPlayer.GetClipWeight(i, selectedLayer));
        }
    }

    private int DrawLayerSelection(int numLayers)
    {
        selectedLayer = Mathf.Clamp(selectedLayer, 0, numLayers);

        EditorGUILayout.BeginHorizontal();
        
        GUILayout.FlexibleSpace();
        
        selectedLayer = DrawLeftButton(selectedLayer);
        EditorGUILayout.LabelField("Selected layer: " + selectedLayer, GUILayout.Width(selectedLayerWidth));
        selectedLayer = DrawRightButton(numLayers, selectedLayer);
        
        GUILayout.Space(10f);
        
        if (GUILayout.Button("Add layer", GUILayout.MinWidth(100f)))
        {
            ExpandArrayByOne(ref animationPlayer.layers, AnimationLayer.CreateLayer);
            selectedLayer = animationPlayer.layers.Length - 1;
            SetDirty();
        }
        
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
        
        return selectedLayer;
    }

    private int DrawRightButton(int numLayers, int selectedLayer)
    {
        var disabled = numLayers == 1 || selectedLayer == numLayers - 1;

        EditorGUI.BeginDisabledGroup(disabled);
        if (GUILayout.Button(rightArrow, GUILayout.Width(arrowButtonWidth)))
            selectedLayer++;
        EditorGUI.EndDisabledGroup();
        return selectedLayer;
    }

    private int DrawLeftButton(int selectedLayer)
    {
        var disabled = selectedLayer == 0;
        EditorGUI.BeginDisabledGroup(disabled);
        if (GUILayout.Button(leftArrow, GUILayout.Width(arrowButtonWidth)))
            selectedLayer--;
        EditorGUI.EndDisabledGroup();
        return selectedLayer;
    }
    
    private void DrawSelectedLayer()
    {
        var layer = animationPlayer.layers[selectedLayer];

        layer.startWeight = EditorGUILayout.Slider("Layer Weight", layer.startWeight, 0f, 1f);
        layer.mask = ObjectField("Mask", layer.mask);
        
        GUILayout.Space(10f);
        EditorGUILayout.LabelField("States:");

        EditorGUI.indentLevel++;
        foreach (var state in layer.states)
        {
            GUILayout.Space(10f);
            DrawState(state);
        }
        EditorGUI.indentLevel--;
        

        if (GUILayout.Button("Add State"))
        {
            layer.states.Add(new AnimationState());
            SetDirty();
        }
    }

    private void DrawState(AnimationState state)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name", GUILayout.Width(55f));
        state.name = EditorGUILayout.TextField(state.name);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Clip", GUILayout.Width(55f));
        state.clip = ObjectField(state.clip);
        if (state.clip != null && string.IsNullOrEmpty(state.name))
            state.name = state.clip.name;
        EditorGUILayout.EndHorizontal();
    }

    private new void SetDirty()
    {
        EditorUtility.SetDirty(animationPlayer);
        EditorSceneManager.MarkSceneDirty(animationPlayer.gameObject.scene);
    }
    
    private void ExpandArrayByOne<T>(ref T[] array, Func<T> CreateNew)
    {
        Array.Resize(ref array, array.Length + 1);
        array[array.Length - 1] = CreateNew();
    }
    
    private T ObjectField<T>(string label, T obj) where T : UnityEngine.Object
    {
        return (T) EditorGUILayout.ObjectField(label, obj, typeof(T), false);
    }
    
    private T ObjectField<T>(T obj) where T : UnityEngine.Object
    {
        return (T) EditorGUILayout.ObjectField(obj, typeof(T), false);
    }

    private const string rightArrow = "\u2192";
    private const string leftArrow = "\u2190";
    private const float arrowButtonWidth = 24f;
    private const float selectedLayerWidth = 108f;

    private const string persistedLayer = "APE_SelectedLayer_";
    private int GetPersistedSelectedLayer(int instanceId)
    {
        return EditorPrefs.GetInt(persistedLayer + instanceId, 0);
    }

    private void SetPersistedSelectedLayer(int instanceId, int value)
    {
        EditorPrefs.SetInt(persistedLayer + instanceId, value);
    }
}