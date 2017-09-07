using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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

        selectedLayer = new PersistedInt(persistedLayer + instanceId);
        selectedEditMode = new PersistedEditMode(persistedEditMode + instanceId);
        selectedState = new PersistedInt(persistedState + instanceId);
        selectedToState = new PersistedInt(persistedToState + instanceId);

        shouldUpdateStateNames = true;
    }

    public override void OnInspectorGUI()
    {
        HandleInitialization();

        if (animationPlayer.layers == null || animationPlayer.layers.Length == 0)
            return;

        EditorUtilities.Splitter();

        DrawLayerSelection();

        if (animationPlayer.layers.Length == 0)
            return; //Deleted last layer in DrawLayerSelection

        if (selectedState == -1 && animationPlayer.layers[selectedLayer].states.Count > 0)
        {
            //Handle adding a state for the first time.
            selectedState.SetTo(0);
        }

        GUILayout.Space(10f);

        DrawSelectedLayer();

        GUILayout.Space(10f);
        EditorUtilities.Splitter();

        EditorUtilities.DrawHorizontal(() =>
        {
            var stateSelectionWidth = GUILayout.Width(200f);
            EditorUtilities.DrawVertical(() =>
            {
                DrawStateSelection(stateSelectionWidth);
            }, stateSelectionWidth);

            EditorUtilities.DrawVertical(DrawSelectedState);
        });

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
            editLayerStyle = new GUIStyle
            {
                normal =
                {
                    background = backgroundTex
                }
            };

            var buttonBackgroundTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.05f));
            var buttonSelectedTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.05f));
            var buttonNotSelectedText = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, 0.2f));

            editLayerButton_Background = new GUIStyle
            {
                normal =
                {
                    background = buttonBackgroundTex
                }
            };

            editLayerButton_NotSelected = new GUIStyle(GUI.skin.label)
            {
                normal =
                {
                    background = buttonNotSelectedText
                },
                alignment = TextAnchor.MiddleCenter
            };

            editLayerButton_Selected = new GUIStyle(GUI.skin.label)
            {
                normal =
                {
                    background = buttonSelectedTex
                },
                alignment = TextAnchor.MiddleCenter
            };

            stylesCreated = true;
        }

        if (animationPlayer.layers == null || animationPlayer.layers.Length == 0)
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
                    allStateNames[i][j] = states[j].Name;
            }
        }
    }

    private void DrawLayerSelection()
    {
        var numLayers = animationPlayer.layers.Length;

        selectedLayer.SetTo(Mathf.Clamp(selectedLayer, 0, numLayers));

        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        selectedLayer.SetTo(EditorUtilities.DrawLeftButton(selectedLayer));
        EditorGUILayout.LabelField("Selected layer: " + selectedLayer, GUILayout.Width(selectedLayerWidth));
        selectedLayer.SetTo(EditorUtilities.DrawRightButton(selectedLayer, numLayers));

        GUILayout.Space(10f);

        if (GUILayout.Button("Add layer", GUILayout.MinWidth(100f)))
        {
            Undo.RecordObject(animationPlayer, "Add layer to animation player");
            EditorUtilities.ExpandArrayByOne(ref animationPlayer.layers, AnimationLayer.CreateLayer);
            selectedLayer.SetTo(animationPlayer.layers.Length - 1);
            shouldUpdateStateNames = true;
        }
        if (EditorUtilities.AreYouSureButton("Delete layer", "Are you sure?", "DeleteLayer" + selectedLayer, 1f, GUILayout.Width(100f)))
        {
            Undo.RecordObject(animationPlayer, "Delete layer from animation player");
            EditorUtilities.DeleteIndexFromArray(ref animationPlayer.layers, selectedLayer);
            selectedLayer.SetTo(Mathf.Max(0, selectedLayer - 1));
            shouldUpdateStateNames = true;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSelectedLayer()
    {
        var layer = animationPlayer.layers[selectedLayer];

        layer.startWeight = EditorGUILayout.Slider($"Layer {selectedLayer} Weight", layer.startWeight, 0f, 1f);
        layer.mask = EditorUtilities.ObjectField($"Layer {selectedLayer} Mask", layer.mask);
    }

    private void DrawStateSelection(GUILayoutOption width)
    {
        if ((EditMode) selectedEditMode == EditMode.Transitions)
            EditorGUILayout.LabelField("Edit transitions for:", width);
        else
            EditorGUILayout.LabelField("Edit state:", width);

        GUILayout.Space(10f);

        var layer = animationPlayer.layers[selectedLayer];

        for (int i = 0; i < layer.states.Count; i++)
        {
            EditorGUI.BeginDisabledGroup(i == selectedState);
            if (GUILayout.Button(layer.states[i].Name, width))
                selectedState.SetTo(i);
            EditorGUI.EndDisabledGroup();
        }

        GUILayout.Space(30f);

        if (GUILayout.Button("Add Normal State", width))
        {
            Undo.RecordObject(animationPlayer, "Add state to animation player");
            layer.states.Add(AnimationState.SingleClip(GetUniqueStateName(AnimationState.DefaultSingleClipName, layer.states)));
            selectedState.SetTo(layer.states.Count - 1);
            shouldUpdateStateNames = true;
        }

        if (GUILayout.Button("Add blend tree", width))
        {
            Undo.RecordObject(animationPlayer, "Add blend tree to animation player");
            layer.states.Add(AnimationState.BlendTree1D(GetUniqueStateName(AnimationState.DefaultBlendTreeName, layer.states)));
        }
    }

    private void DrawSelectedState()
    {
        EditorGUILayout.BeginHorizontal(editLayerButton_Background);

        //Making tabbed views are hard!
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

            var isSelected = index == (int) selectedEditMode;
            var style = isSelected ? editLayerButton_Selected : editLayerButton_NotSelected;
            if (GUI.Button(rect, label, style))
                selectedEditMode.SetTo((EditMode) index);
        };

        Draw(editStatesRect, "Edit state", 0);
        Draw(editTransitionsRect, "Edit Transitions", 1);
        Draw(metaDataRect, "Metadata", 2);

        EditorGUILayout.BeginVertical(editLayerStyle);

        GUILayout.Space(10f);

        if (selectedEditMode == (int) EditMode.States)
            DrawStateData();
        else if (selectedEditMode == (int) EditMode.Transitions)
            DrawTransitions();
        else
            for (int i = 0; i < 30; i++)
                EditorGUILayout.LabelField("Metadata should go here (clips used, etc.)");

        GUILayout.Space(20f);
        EditorGUILayout.EndVertical();
    }

    private void DrawStateData()
    {
        var layer = animationPlayer.layers[selectedLayer];

        if (layer.states.Count == 0)
        {
            EditorGUILayout.LabelField("No states");
            return;
        }

        if (!layer.states.IsInBounds(selectedState))
        {
            Debug.LogError("Out of bounds: " + selectedState + " out of " + layer.states.Count);
            return;
        }

        var state = layer.states[selectedState];

        EditorGUILayout.LabelField("State");
        var deleteThisState = false;
        EditorUtilities.DrawIndented(() =>
        {
            const float labelWidth = 55f;

            var old = state.Name;
            state.Name = EditorUtilities.TextField("Name", state.Name, labelWidth);
            if (old != state.Name)
                shouldUpdateStateNames = true;

            state.speed = EditorUtilities.DoubleField("Speed", state.speed, labelWidth);

            if (state.type == AnimationStateType.SingleClip)
            {
                var oldClip = state.clip;
                state.clip = EditorUtilities.ObjectField("Clip", state.clip, labelWidth);
                if (state.clip != null && state.clip != oldClip)
                    state.OnClipAssigned();
            }

            else
            {
                state.blendVariable = EditorUtilities.TextField("Blend with variable", state.blendVariable, 120f);
                EditorGUI.indentLevel++;
                foreach (var blendTreeEntry in state.blendTree)
                    DrawBlendTreeEntry(blendTreeEntry, state.blendVariable);

                EditorGUI.indentLevel--;

                GUILayout.Space(10f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                    state.blendTree.Add(new BlendTreeEntry());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

            }

            GUILayout.Space(20f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            deleteThisState = EditorUtilities.AreYouSureButton("Delete state", "are you sure", "DeleteState_" + selectedState + "_" + selectedLayer, 1f);
            EditorGUILayout.EndHorizontal();
        });

        if (deleteThisState)
        {
            Undo.RecordObject(animationPlayer, "Deleting state " + layer.states[selectedState].Name);
            layer.states.RemoveAt(selectedState);
            layer.transitions.RemoveAll(transition => transition.fromState == selectedState || transition.toState == selectedState);
            foreach (var transition in layer.transitions)
            {
                //This would be so much better if transitions were placed on the state!
                if (transition.toState > selectedState)
                    transition.toState--;
                if (transition.fromState > selectedState)
                    transition.fromState--;
            }
            shouldUpdateStateNames = true;
            selectedState.SetTo(selectedState - 1);
        }
    }

    private void DrawBlendTreeEntry(BlendTreeEntry blendTreeEntry, string blendVarName)
    {
        blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
        blendTreeEntry.threshold = EditorUtilities.FloatField($"When '{blendVarName}' =", blendTreeEntry.threshold, 150f, 200f);
    }

    private void DrawTransitions()
    {
        var layer = animationPlayer.layers[selectedLayer];
        if (layer.states.Count == 0)
        {
            EditorGUILayout.LabelField("No states, can't define transitions");
            return;
        }

        EditorGUILayout.LabelField("Transitions from " + layer.states[selectedState].Name);

        EditorGUILayout.Space();

        EditorUtilities.DrawIndented(() =>
        {
            selectedToState.SetTo(EditorGUILayout.Popup("Transtion to state", selectedToState, allStateNames[selectedLayer]));
            selectedToState.SetTo(Mathf.Clamp(selectedToState, 0, layer.states.Count - 1));

            EditorGUILayout.Space();

            var transition = layer.transitions.Find(state => state.fromState == selectedState && state.toState == selectedToState);
            var fromStateName = layer.states[selectedState].Name;
            var toStateName = layer.states[selectedToState].Name;

            if (transition == null)
            {
                EditorGUILayout.LabelField($"No ({fromStateName}->{toStateName}) transition defined!");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"Create one!"))
                {
                    Undo.RecordObject(animationPlayer, $"Add transition from {fromStateName} to {toStateName}");
                    layer.transitions.Add(
                        new StateTransition
                        {
                            fromState = selectedState,
                            toState = selectedToState,
                            transitionData = TransitionData.Linear(1f)
                        });
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
                    "Clear_Transition_" + fromStateName + "_" + toStateName,
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
            string stateName = animationPlayer.layers[selectedLayer].states[i].Name;

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

    private const float selectedLayerWidth = 108f;

    private const string persistedLayer = "APE_SelectedLayer_";
    private const string persistedState = "APE_SelectedState_";
    private const string persistedToState = "APE_SelectedToState_";
    private const string persistedEditMode = "APE_EditMode_";

    private class PersistedEditMode : PersistedVal<EditMode>
    {
        public PersistedEditMode(string key) : base(key) { }

        protected override int ToInt(EditMode val)
        {
            return (int) val;
        }

        protected override EditMode ToType(int i)
        {
            return (EditMode) i;
        }

        public static implicit operator int(PersistedEditMode p)
        {
            return p.ToInt(p); //look at it go!
        }
    }

    private static string GetUniqueStateName(string wantedName, List<AnimationState> otherStates)
    {
        if (!otherStates.Any(state => state.Name == wantedName))
        {
            return wantedName;
        }

        var allNamesSorted = otherStates.Select(layer => layer.Name).Where(name => name != wantedName && name.StartsWith(wantedName));

        int greatestIndex = 0;
        foreach (var name in allNamesSorted)
        {
            int numericPostFix;
            if (int.TryParse(name.Substring(wantedName.Length + 1), out numericPostFix))
            {
                if (numericPostFix > greatestIndex)
                    greatestIndex = numericPostFix;
            }
        }

        return wantedName + (greatestIndex + 1);
    }
}