using UnityEditor;
using UnityEngine;
using BlendTreeEntry = Animation_Player.AnimationState.BlendTreeEntry;
using AnimationStateType = Animation_Player.AnimationState.AnimationStateType;

namespace Animation_Player
{
    public static class StateDataDrawer
    {
        public static void DrawStateData(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                         AnimationPlayerEditor currentEditor)
        {
            var updateStateNames = false;
            var layer = animationPlayer.layers[selectedLayer];

            if (layer.states.Count == 0)
            {
                EditorGUILayout.LabelField("No states");
                return;
            }

            if (!layer.states.IsInBounds(selectedState))
            {
                Debug.LogError("Out of bounds state: " + selectedState + " out of " + layer.states.Count + " states! Resetting to 0");
                selectedState.SetTo(0);
                return;
            }

            EditorGUILayout.LabelField("State");

            EditorGUI.indentLevel++;
            DrawStateData(layer.states[selectedState], ref updateStateNames);

            GUILayout.Space(20f);

            var deleteThisState = DrawDeleteStateButton(selectedLayer, selectedState);
            EditorGUI.indentLevel--;

            if (deleteThisState)
            {
                DeleteState(animationPlayer, layer, selectedState);
                updateStateNames = true;
            }

            if (updateStateNames)
                currentEditor.MarkDirty();

            currentEditor.previewer.DrawStatePreview(selectedLayer, selectedState);
        }

        private static void DrawStateData(AnimationState state, ref bool updateStateNames)
        {
            const float labelWidth = 55f;

            var old = state.Name;
            state.Name = EditorUtilities.TextField("Name", state.Name, labelWidth);
            if (old != state.Name)
                updateStateNames = true;

            state.speed = EditorUtilities.DoubleField("Speed", state.speed, labelWidth);

            switch (state.type)
            {
                case AnimationStateType.SingleClip:
                    var oldClip = state.clip;
                    state.clip = EditorUtilities.ObjectField("Clip", state.clip, labelWidth);
                    if (state.clip != null && state.clip != oldClip)
                        updateStateNames |= state.OnClipAssigned(state.clip);
                    break;
                case AnimationStateType.BlendTree1D:
                    state.blendVariable = EditorUtilities.TextField("Blend with variable", state.blendVariable, 120f);
                    EditorGUI.indentLevel++;
                    foreach (var blendTreeEntry in state.blendTree)
                        updateStateNames |= DrawBlendTreeEntry(state, blendTreeEntry, state.blendVariable);

                    EditorGUI.indentLevel--;

                    GUILayout.Space(10f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                        state.blendTree.Add(new BlendTreeEntry());
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    break;
                case AnimationStateType.BlendTree2D:
                    state.blendVariable = EditorUtilities.TextField("First blend variable", state.blendVariable, 120f);
                    state.blendVariable2 = EditorUtilities.TextField("Second blend variable", state.blendVariable2, 120f);
                    EditorGUI.indentLevel++;
                    foreach (var blendTreeEntry in state.blendTree)
                        updateStateNames |= DrawBlendTreeEntry(state, blendTreeEntry, state.blendVariable, state.blendVariable2);

                    EditorGUI.indentLevel--;

                    GUILayout.Space(10f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                        state.blendTree.Add(new BlendTreeEntry());
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    break;
                default:
                    EditorGUILayout.LabelField("Unknown animation state type: " + state.type);
                    break;
            }
        }

        private static bool DrawBlendTreeEntry(AnimationState state, BlendTreeEntry blendTreeEntry, string blendVarName, string blendVarName2 = null)
        {
            var changedName = false;
            var oldClip = blendTreeEntry.clip;
            blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
            if (blendTreeEntry.clip != oldClip && blendTreeEntry.clip != null)
                changedName = state.OnClipAssigned(blendTreeEntry.clip);

            blendTreeEntry.threshold = EditorUtilities.FloatField($"When '{blendVarName}' =", blendTreeEntry.threshold, 150f, 200f);
            if (blendVarName2 != null)
                blendTreeEntry.threshold2 = EditorUtilities.FloatField($"When '{blendVarName2}' =", blendTreeEntry.threshold2, 150f, 200f);
            return changedName;
        }

        private static bool DrawDeleteStateButton(PersistedInt selectedLayer, PersistedInt selectedState)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var deleteThisState = EditorUtilities.AreYouSureButton("Delete state", "are you sure", "DeleteState_" + selectedState + "_" + selectedLayer, 1f);
            EditorGUILayout.EndHorizontal();
            return deleteThisState;
        }

        private static void DeleteState(AnimationPlayer animationPlayer, AnimationLayer layer, PersistedInt selectedState)
        {
            EditorUtilities.RecordUndo(animationPlayer, "Deleting state " + layer.states[selectedState].Name);
            layer.transitions.RemoveAll(transition => transition.FromState == layer.states[selectedState] ||
                                                      transition.ToState == layer.states[selectedState]);
            layer.states.RemoveAt(selectedState);

            if (selectedState == layer.states.Count) //was last state
                selectedState.SetTo(selectedState - 1);
        }
    }
}