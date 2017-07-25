using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
    private PersistedInt selectedState;
    private PersistedInt selectedToState;
    private PersistedEditMode selectedEditMode;

    private string[][] allStateNames;
    private bool shouldUpdateStateNames;

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

        shouldUpdateStateNames = true;
    }

    public override void OnInspectorGUI()
    {
        HandleInitialization();

        if (animationPlayer.layers.Length == 0)
            return;

        EditorUtilities.Splitter();

        DrawLayerSelection();

        if (animationPlayer.layers.Length == 0)
            return; //Deleted last layer in DrawLayerSelection

        GUILayout.Space(10f);

        DrawSelectedLayer();

        EditorUtilities.Splitter();

        EditorGUILayout.LabelField("Default transition");

        Undo.RecordObject(animationPlayer, "Change default transition");
        animationPlayer.defaultTransition = DrawTransitionData(animationPlayer.defaultTransition);

        EditorUtilities.Splitter();

        DrawRuntimeDebugData();
    }

    private void HandleInitialization()
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

        var numLayers = animationPlayer.layers.Length;
        if (animationPlayer.layers == null || numLayers == 0)
        {
            GUILayout.Space(30f);

            EditorGUILayout.LabelField("No layers in the animation player!");
            if (GUILayout.Button("Fix that!"))
            {
                animationPlayer.layers = new AnimationLayer[1];
                animationPlayer.layers[0] = AnimationLayer.CreateLayer();
            }
            return;
        }

        if (shouldUpdateStateNames)
        {
            shouldUpdateStateNames = false;
            allStateNames = new string[animationPlayer.layers.Length][];
            for (int i = 0; i < animationPlayer.layers.Length; i++)
            {
                var states = animationPlayer.layers[i].states;
                allStateNames[i] = new string[states.Count];
                for (int j = 0; j < states.Count; j++)
                    allStateNames[i][j] = states[j].name;
            }
        }
    }

    private void DrawLayerSelection()
    {
        var numLayers = animationPlayer.layers.Length;
        
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
            shouldUpdateStateNames = true;
        }
        if (EditorUtilities.AreYouSureButton("Delete layer", "Are you sure?", "DeleteLayer" + selectedLayer.Get(), 1f, GUILayout.Width(100f)))
        {
            Undo.RecordObject(animationPlayer, "Delete layer from animation player");
            EditorUtilities.DeleteIndexFromArray(ref animationPlayer.layers, selectedLayer);
            selectedLayer.SetTo(Mathf.Max(0, selectedLayer - 1));
            shouldUpdateStateNames = true;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
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
        var metaDataRect = GUILayoutUtility.GetLastRect();

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
            if (GUI.Button(rect, label, style))
                selectedEditMode.SetTo((EditMode) index);
        };

        Draw(editStatesRect, "Edit states", 0);
        Draw(editTransitionsRect, "Edit Transitions", 1);
        Draw(metaDataRect, "Metadata", 2);

        EditorGUILayout.BeginVertical(editLayerStyle);

        GUILayout.Space(10f);

        if (selectedEditMode == (int) EditMode.States)
            DrawStates();
        else if (selectedEditMode == (int) EditMode.Transitions)
            DrawTransitions();
        else
            EditorGUILayout.LabelField("Metadata should go here (clips used, etc.)");

        GUILayout.Space(20f);
        EditorGUILayout.EndVertical();
    }

    private void DrawStates()
    {
        var layer = animationPlayer.layers[selectedLayer];

        EditorGUILayout.LabelField("States:");

        EditorGUI.indentLevel++;
        int deleteIndex = -1;
        for (var i = 0; i < layer.states.Count; i++)
        {
            if (DrawState(layer.states[i], i))
                deleteIndex = i;

            if (i != layer.states.Count - 1)
                GUILayout.Space(20f);
        }
        EditorGUI.indentLevel--;

        if (deleteIndex != -1)
        {
            Undo.RecordObject(animationPlayer, "Deleting state " + layer.states[deleteIndex].name);
            layer.states.RemoveAt(deleteIndex);
            layer.transitions.RemoveAll(transition => transition.fromState == deleteIndex || transition.toState == deleteIndex);
            foreach (var transition in layer.transitions)
            {
                //This would be so much better if transitions were placed on the state!
                if (transition.toState > deleteIndex)
                    transition.toState--;
                if (transition.fromState > deleteIndex)
                    transition.fromState--;
            }
            shouldUpdateStateNames = true;
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Normal State"))
        {
            Undo.RecordObject(animationPlayer, "Add state to animation player");
            layer.states.Add(AnimationState.Normal());
            shouldUpdateStateNames = true;
        }

        if (GUILayout.Button("Add blend tree"))
        {
            Undo.RecordObject(animationPlayer, "Add blend tree to animation player");
            layer.states.Add(AnimationState.BlendTree1D());
        }

        EditorGUILayout.EndHorizontal();

    }

    private bool DrawState(AnimationState state, int stateIndex)
    {
        const float labelWidth = 55f;

        var old = state.name;
        state.name = TextField("Name", state.name, labelWidth);
        if (old != state.name)
            shouldUpdateStateNames = true;
        
        state.speed = DoubleField("Speed", state.speed, labelWidth);

        if (state.type == AnimationStateType.SingleClip)
        {
            state.clip = ObjectField("Clip", state.clip, labelWidth);
            if (state.clip != null && (string.IsNullOrEmpty(state.name) || state.name == AnimationState.DefaultName))
                state.name = state.clip.name;
        }

        else
        {
            state.blendVariable = TextField("Blend with variable", state.blendVariable, 100f);
            EditorGUI.indentLevel++;
            foreach (var blendTreeEntry in state.blendTree)
                DrawBlendTreeEntry(blendTreeEntry, state.blendVariable);

            EditorGUI.indentLevel--;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(137f);
            if (GUILayout.Button("Add blend tree entry"))
                state.blendTree.Add(new BlendTreeEntry());
            EditorGUILayout.EndHorizontal();

        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(77f);
        bool delete = EditorUtilities.AreYouSureButton("delete state", "are you sure", "DeleteState_" + stateIndex + "_" + selectedLayer.Get(), 1f);
        EditorGUILayout.EndHorizontal();

        return delete;
    }

    private void DrawBlendTreeEntry(BlendTreeEntry blendTreeEntry, string blendVarName)
    {
        blendTreeEntry.clip = ObjectField("Clip", blendTreeEntry.clip, 100f);
        blendTreeEntry.threshold = FloatField($"When '{blendVarName}' =", blendTreeEntry.threshold, 100f);
    }

    private void DrawTransitions()
    {
        var layer = animationPlayer.layers[selectedLayer];
        selectedState.SetTo(EditorGUILayout.Popup("Transitions from state", selectedState, allStateNames[selectedLayer]));
        selectedState.SetTo(Mathf.Clamp(selectedState, 0, layer.states.Count - 1));

        EditorGUILayout.Space();

        EditorUtilities.DrawIndented(() =>
        {
            selectedToState.SetTo(EditorGUILayout.Popup("Transtion to state", selectedToState, allStateNames[selectedLayer]));
            selectedToState.SetTo(Mathf.Clamp(selectedToState, 0, layer.states.Count - 1));

            EditorGUILayout.Space();

            var transition = layer.transitions.Find(state => state.fromState == selectedState && state.toState == selectedToState);
            var fromStateName = layer.states[selectedState].name;
            var toStateName = layer.states[selectedToState].name;

            if (transition == null)
            {
                EditorGUILayout.LabelField("No transition defined!");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"Create transition from {fromStateName} to {toStateName}"))
                {
                    Undo.RecordObject(animationPlayer, $"Add transition from {fromStateName} to {toStateName}");
                    layer.transitions.Add(
                        new StateTransition {fromState = selectedState, toState = selectedToState, transitionData = TransitionData.Linear(1f)});
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                Undo.RecordObject(animationPlayer, $"Edit of transition from  {fromStateName} to {toStateName}");
                transition.transitionData = DrawTransitionData(transition.transitionData);

                GUILayout.Space(20f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (EditorUtilities.AreYouSureButton("Clear transition", "Are you sure?", 
                                                     "Clear_Transition_" + fromStateName + "_" +toStateName,
                                                     1f, GUILayout.Width(150f)))
                {
                    Undo.RecordObject(animationPlayer, $"Clear transition from  {fromStateName} to {toStateName}");
                    layer.transitions.Remove(transition);
                }
                EditorGUILayout.EndHorizontal();
            }
        });
    }

    public TransitionData DrawTransitionData(TransitionData transitionData)
    {
        transitionData.type = (TransitionType) EditorGUILayout.EnumPopup("Type", transitionData.type);
        transitionData.duration = EditorGUILayout.FloatField("Duration", transitionData.duration);

        if (transitionData.type == TransitionType.Curve)
            transitionData.curve = EditorGUILayout.CurveField(transitionData.curve);

        return transitionData;

    }

    private void DrawRuntimeDebugData()
    {
        if (!Application.isPlaying)
            return;

        for (int i = 0; i < animationPlayer.GetStateCount(selectedLayer); i++)
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

        EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetCurrentPlayingState(selectedLayer));
        for (int i = animationPlayer.GetStateCount(selectedLayer) - 1; i >= 0; i--)
        {
            EditorGUILayout.LabelField("weigh for " + i + ": " + animationPlayer.GetClipWeight(i, selectedLayer));
        }

        EditorGUILayout.Space();

        var newBlendVal = EditorGUILayout.Slider("Blend val", blendVal, -5f, 5f);
        if (newBlendVal != blendVal)
        {
            blendVal = newBlendVal;
            animationPlayer.SetBlendVar("blend", blendVal, selectedLayer);
        }
    }

    private float blendVal;

    private string TextField(string label, string text, float width)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(width));
        text = EditorGUILayout.TextField(text);
        EditorGUILayout.EndHorizontal();
        return text;
    }

    private double DoubleField(string label, double value, float width)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(width));
        value = EditorGUILayout.DoubleField(value);
        EditorGUILayout.EndHorizontal();
        return value;
    }

    private T ObjectField<T>(string label, T obj, float width) where T : Object
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(width));
        obj = EditorUtilities.ObjectField(obj);
        EditorGUILayout.EndHorizontal();
        return obj;
    }

    private T ObjectField<T>(string label, T obj) where T : Object
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label);
        obj = EditorUtilities.ObjectField(obj);
        EditorGUILayout.EndHorizontal();
        return obj;
    }

    private float FloatField(string label, float value, float width)
    {
        EditorGUILayout.BeginHorizontal();
        var rect = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.label, GUILayout.Width(width));
        rect.xMax += 35f; //Unity steals 35 pixels of space between horizontal elements for no fucking reason
        EditorGUI.LabelField(rect, label);

        value = EditorGUILayout.FloatField(value);
        EditorGUILayout.EndHorizontal();
        return value;
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