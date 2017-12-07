using UnityEditor;
using UnityEngine;
using BlendTreeEntry = Animation_Player.AnimationState.BlendTreeEntry;

namespace Animation_Player
{
    public static class StateDataDrawer
    {
        public static void DrawStateData(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                         AnimationPlayerEditor currentEditor)
        {
            var updateStateNames = false;
            var layer = animationPlayer.layers[selectedLayer];

            if (layer.animationStates.Count == 0)
            {
                EditorGUILayout.LabelField("No states");
                return;
            }

            if (!layer.animationStates.IsInBounds(selectedState))
            {
                Debug.LogError("Out of bounds state: " + selectedState + " out of " + layer.animationStates.Count + " states! Resetting to 0");
                selectedState.SetTo(0);
                return;
            }

            EditorGUILayout.LabelField("State");

            EditorGUI.indentLevel++;
            DrawStateData(layer.animationStates[selectedState], ref updateStateNames);

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

            //@TODO: Use pattern matching when C# 7
            var type = state.GetType();
            if (type == typeof(SingleClipState))
            {
                var singleClipState = (SingleClipState) state;
                var oldClip = singleClipState.clip;
                singleClipState.clip = EditorUtilities.ObjectField("Clip", singleClipState.clip, labelWidth);
                if (singleClipState.clip != null && singleClipState.clip != oldClip)
                    updateStateNames |= singleClipState.OnClipAssigned(singleClipState.clip);
            }
            else if (type == typeof(BlendTree1D))
            {
                var blendTree = (BlendTree1D) state;
                blendTree.blendVariable = EditorUtilities.TextField("Blend with variable", blendTree.blendVariable, 120f);
                EditorGUI.indentLevel++;
                foreach (var blendTreeEntry in blendTree.blendTree)
                    updateStateNames |= DrawBlendTreeEntry(state, blendTreeEntry, blendTree.blendVariable);

                EditorGUI.indentLevel--;

                GUILayout.Space(10f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                    blendTree.blendTree.Add(new BlendTreeEntry());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            else if (type == typeof(BlendTree2D))
            {
                var blendTree2D = (BlendTree2D) state;
                blendTree2D.blendVariable = EditorUtilities.TextField("First blend variable", blendTree2D.blendVariable, 120f);
                blendTree2D.blendVariable2 = EditorUtilities.TextField("Second blend variable", blendTree2D.blendVariable2, 120f);
                EditorGUI.indentLevel++;
                foreach (var blendTreeEntry in blendTree2D.blendTree)
                    updateStateNames |= DrawBlendTreeEntry(state, blendTreeEntry, blendTree2D.blendVariable, blendTree2D.blendVariable2);

                EditorGUI.indentLevel--;

                GUILayout.Space(10f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                    blendTree2D.blendTree.Add(new BlendTreeEntry());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField($"Unknown animation state type: {type.Name}");
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
            EditorUtilities.RecordUndo(animationPlayer, "Deleting state " + layer.animationStates[selectedState].Name);
            layer.transitions.RemoveAll(transition => transition.FromState == layer.animationStates[selectedState] ||
                                                      transition.ToState == layer.animationStates[selectedState]);
            layer.animationStates.RemoveAt(selectedState);

            if (selectedState == layer.animationStates.Count) //was last state
                selectedState.SetTo(selectedState - 1);
        }
    }
}