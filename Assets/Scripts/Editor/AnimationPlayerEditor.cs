using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor : Editor
{
    private AnimationPlayer animationPlayer;

    private enum EditMode
    {
        States,
        Transitions
    }

    private PersistedInt selectedLayer;
    private PersistedInt selectedEditMode;

    void OnEnable()
    {
        animationPlayer = (AnimationPlayer) target;
        animationPlayer.editTimeUpdateCallback -= Repaint;
        animationPlayer.editTimeUpdateCallback += Repaint;

        selectedLayer = new PersistedInt(persistedLayer, animationPlayer.GetInstanceID());
        selectedEditMode = new PersistedInt(persistedEditMode, animationPlayer.GetInstanceID());
    }

    public override void OnInspectorGUI()
    {
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
        
        EditorUtilities.Splitter();

        selectedLayer.SetTo(DrawLayerSelection(numLayers));

        GUILayout.Space(10f);

        DrawSelectedLayer();
        
        GUILayout.Space(20f);
        EditorUtilities.Splitter();
        
        EditorGUILayout.LabelField("Default transition");
        
        Undo.RecordObject(animationPlayer, "Change default transition");
        animationPlayer.defaultTransition = DrawTransitionData(animationPlayer.defaultTransition);
        
        EditorUtilities.Splitter();

        DrawRuntimeDebugData();
    }

    private void DrawRuntimeDebugData()
    {
        if (!Application.isPlaying)
            return;

        for (int i = animationPlayer.GetStateCount(selectedLayer) - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            string stateName = animationPlayer.layers[selectedLayer].states[i].name;
            
            if (GUILayout.Button($"Blend to {stateName} using default transition"))
                animationPlayer.Play(i, selectedLayer);
            
            if (GUILayout.Button($"Blend to {stateName} over .5 secs"))
                animationPlayer.Play(i, TransitionData.Linear(.5f), selectedLayer);
            
            if (GUILayout.Button($"Snap to {stateName}"))
                animationPlayer.SnapTo(i, selectedLayer);
            
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetCurrentPlayingClip(selectedLayer));
        for (int i = animationPlayer.GetStateCount() - 1; i >= 0; i--)
        {
            EditorGUILayout.LabelField("weigh for " + i + ": " + animationPlayer.GetClipWeight(i, selectedLayer));
        }
    }

    private int DrawLayerSelection(int numLayers)
    {
        selectedLayer.SetTo(Mathf.Clamp(selectedLayer, 0, numLayers));

        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        selectedLayer.SetTo(DrawLeftButton(selectedLayer));
        EditorGUILayout.LabelField("Selected layer: " + selectedLayer.Get(), GUILayout.Width(selectedLayerWidth));
        selectedLayer.SetTo(DrawRightButton(numLayers, selectedLayer));

        GUILayout.Space(10f);

        if (GUILayout.Button("Add layer", GUILayout.MinWidth(100f)))
        {
            Undo.RecordObject(animationPlayer, "Add layer to animation player");
            EditorUtilities.ExpandArrayByOne(ref animationPlayer.layers, AnimationLayer.CreateLayer);
            selectedLayer.SetTo(animationPlayer.layers.Length - 1);
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
        layer.mask = EditorUtilities.ObjectField("Mask", layer.mask);

        GUILayout.Space(10f);

        selectedEditMode.SetTo(EditorGUILayout.Popup(selectedEditMode, editModeChoices, GUILayout.Width(200f)));

        GUILayout.Space(10f);

        if (selectedEditMode == (int) EditMode.States)
            DrawStates(layer);
        else
            DrawTransitions(layer);
    }

    private void DrawStates(AnimationLayer layer)
    {
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
            Undo.RecordObject(animationPlayer, "Add state to animation player");
            layer.states.Add(new AnimationState());
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
        state.clip = EditorUtilities.ObjectField(state.clip);
        if (state.clip != null && string.IsNullOrEmpty(state.name))
            state.name = state.clip.name;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTransitions(AnimationLayer layer)
    {
        EditorGUILayout.LabelField("Transitions:");

        EditorGUI.indentLevel++;
        foreach (var transition in layer.transitions)
        {
            GUILayout.Space(10f);
            Undo.RecordObject(animationPlayer, "Edit transition in animation player");
            DrawTransition(transition, layer);
        }
        EditorGUI.indentLevel--;

        if (GUILayout.Button("Add Transition"))
        {
            Undo.RecordObject(animationPlayer, "Add transition to animation player");
            layer.transitions.Add(new StateTransition {fromState = 0, toState = 0, transitionData = TransitionData.Instant()});
        }
    }

    private void DrawTransition(StateTransition stateTransition, AnimationLayer layer)
    {
        string[] choices = layer.states.Select(state => state.name).ToArray(); //@TODO: obviously

        stateTransition.fromState = EditorGUILayout.Popup("Transition from", stateTransition.fromState, choices);
        stateTransition.toState = EditorGUILayout.Popup("Transition to", stateTransition.toState, choices);
        stateTransition.transitionData = DrawTransitionData(stateTransition.transitionData);
    }

    public TransitionData DrawTransitionData(TransitionData transitionData)
    {
        transitionData.type = (TransitionType) EditorGUILayout.EnumPopup("Type", transitionData.type);
        transitionData.duration = EditorGUILayout.FloatField("Duration", transitionData.duration);

        if (transitionData.type == TransitionType.Curve)
            transitionData.curve = EditorGUILayout.CurveField(transitionData.curve);

        return transitionData;
        
    }

    private const string rightArrow = "\u2192";
    private const string leftArrow = "\u2190";
    private const float arrowButtonWidth = 24f;
    private const float selectedLayerWidth = 108f;

    private const string persistedLayer = "APE_SelectedLayer_";
    private const string persistedEditMode = "APE_EditMode_";

    private static readonly string[] editModeChoices = {"Edit states", "Edit transitions"};
}