using System;
using System.Linq;
using NUnit.Framework.Constraints;
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
    private PersistedInt selectedState; //for now in transition view
    private PersistedInt selectedToState;
    private PersistedEditMode selectedEditMode;

    private string[][] allStateNames;

    private static bool stylesCreated = false;
    private static GUIStyle editLayerStyle;
    private static GUIStyle editLayerButton_Background;
    private static GUIStyle editLayerButton_NotSelected;
    private static GUIStyle editLayerButton_Selected;

    void OnEnable()
    {
        animationPlayer = (AnimationPlayer) target;
        animationPlayer.editTimeUpdateCallback -= Repaint;
        animationPlayer.editTimeUpdateCallback += Repaint;

        var instanceId = animationPlayer.GetInstanceID();
        
        selectedLayer = new PersistedInt(persistedLayer, instanceId);
        selectedEditMode = new PersistedEditMode(persistedEditMode, instanceId);
        selectedState = new PersistedInt(persistedState, instanceId);
        selectedToState = new PersistedInt(persistedToState, instanceId);
        
        allStateNames = new string[animationPlayer.layers.Length][];
        for (int i = 0; i < animationPlayer.layers.Length; i++)
        {
            var states = animationPlayer.layers[i].states;
            allStateNames[i] = new string[states.Count];
            for (int j = 0; j < states.Count; j++)
                allStateNames[i][j] = states[j].name;
        }
    }

    
    public override void OnInspectorGUI()
    {
        if (!stylesCreated)
        {
            var backgroundTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, .1f));
            editLayerStyle = new GUIStyle {normal = {background = backgroundTex}};

            var buttonBackgroundTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.05f));
            var buttonSelectedTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.05f));
            var buttonNotSelectedText = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.2f));
            
            editLayerButton_Background = new GUIStyle {normal = {background = buttonBackgroundTex}};

            editLayerButton_NotSelected = new GUIStyle(GUI.skin.label)
            {
                normal = {background = buttonNotSelectedText}
            };

            editLayerButton_Selected = new GUIStyle(GUI.skin.label)
            {
                normal = {background = buttonSelectedTex}
            };

            stylesCreated = true;
        }
        
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

        EditorUtilities.Splitter();


        EditorGUILayout.BeginHorizontal(editLayerButton_Background);
        
        //HACK: Reseve the rects for later use
        GUILayout.Label("");
        var editStatesRect = GUILayoutUtility.GetLastRect();
        GUILayout.Label("");
        var editTransitionsRect = GUILayoutUtility.GetLastRect();
        GUILayout.Label("");
        var fooBarRect = GUILayoutUtility.GetLastRect();
        
        EditorGUILayout.EndHorizontal();
        
        //Hack part 2: expand the rects so they hit the next control
        Action<Rect, string, int> Draw = (rect, label, index) =>
        {
            rect.yMax += 2;
            rect.yMin -= 2;
            if (index == 0)
            {
                rect.x -= 4;
                rect.xMax += 4;
            }
            else if (index == 2)
            {
                rect.x += 4;
                rect.xMin -= 4;
            }

            var isSelected = index == selectedEditMode;
            var style = isSelected ? editLayerButton_Selected : editLayerButton_NotSelected;
            if(GUI.Button(rect, label, style))
                selectedEditMode.SetTo((EditMode) index);
        };

        Draw(editStatesRect, "Edit states", 0);
        Draw(editTransitionsRect, "Edit Transitions", 1);
        Draw(fooBarRect, "Foo/Bar: FooBar", 2);
        
        EditorGUILayout.BeginVertical(editLayerStyle);

        GUILayout.Space(10f);

        if (selectedEditMode == (int) EditMode.States)
            DrawStates();
        else if(selectedEditMode == (int) EditMode.Transitions)
            DrawTransitions();

        GUILayout.Space(20f);
        EditorGUILayout.EndVertical();
    }

    private void DrawStates()
    {
        var layer = animationPlayer.layers[selectedLayer]; 
            
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

    private void DrawTransitions()
    {
        var layer = animationPlayer.layers[selectedLayer]; 
        selectedState.SetTo(EditorGUILayout.Popup("Transitions from state", selectedState, allStateNames[selectedLayer]));
        
        EditorGUI.indentLevel++;
        selectedToState.SetTo(EditorGUILayout.Popup("Transtion to state state", selectedToState, allStateNames[selectedLayer]));
        
        EditorGUILayout.Space();
        
        var transition = layer.transitions.Find(state => state.fromState == selectedState && state.toState == selectedToState);
        if (transition == null)
        {
            EditorGUILayout.LabelField("No transition defined!");
            if (GUILayout.Button("Create transition"))
            {
                Undo.RecordObject(animationPlayer, $"Add transition from {layer.states[selectedState].name} to {layer.states[selectedToState].name}");
                layer.transitions.Add(new StateTransition {fromState = selectedState, toState = selectedToState, transitionData = TransitionData.Linear(1f)});
            }
        }
        else
        {
            Undo.RecordObject(animationPlayer, "change transition from  {layer.states[selectedState].name} to {layer.states[selectedToState].name}");
            transition.transitionData = DrawTransitionData(transition.transitionData);
        }
        
        EditorGUI.indentLevel--;
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
    private const string persistedState = "APE_SelectedState_";
    private const string persistedToState = "APE_SelectedToState_";
    private const string persistedEditMode = "APE_EditMode_";
    
    private class PersistedEditMode : PersistedVal<EditMode>
    {

        public PersistedEditMode(string key, int instanceID) : base(key, instanceID)
        { }

        protected override int ToInt(EditMode val)
        {
            return (int) val;
        }

        protected override EditMode ToType(int i)
        {
            return (EditMode) i;
        }
    }

}