using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public static class StateDataDrawer
    {
        private static object reloadChecker;
        private static GUILayoutOption[] upDownButtonOptions;
        private static GUIStyle upDownButtonStyle;

        public static void DrawStateData(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                         AnimationPlayerEditor currentEditor)
        {
            if (reloadChecker == null)
            {
                reloadChecker = new object();
                upDownButtonOptions = new[] {GUILayout.Width(25f), GUILayout.Height(15f)};

                upDownButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.UpperCenter,
                    clipping = TextClipping.Overflow
                };
            }

            var markDirty = false;
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
            DrawStateData(layer.states[selectedState], ref markDirty);

            GUILayout.Space(20f);

            var deleteThisState = DrawDeleteStateButton(selectedLayer, selectedState);
            EditorGUI.indentLevel--;

            if (deleteThisState)
            {
                DeleteState(animationPlayer, layer, selectedState);
                markDirty = true;
            }

            if (markDirty)
                currentEditor.MarkDirty();

            currentEditor.previewer.DrawStatePreview(selectedLayer, selectedState);
        }

        private static void DrawStateData(AnimationState state, ref bool markDirty)
        {
            const float labelWidth = 55f;

            var old = state.Name;
            state.Name = EditorUtilities.TextField("Name", state.Name, labelWidth);
            if (old != state.Name)
                markDirty = true;

            state.speed = EditorUtilities.DoubleField("Speed", state.speed, labelWidth);

            //@TODO: C# 7 pattern matching
            var type = state.GetType();
            if (type == typeof(SingleClipState))
            {
                DrawSingleClipState((SingleClipState) state, ref markDirty, labelWidth);
            }
            else if (type == typeof(BlendTree1D))
            {
                Draw1DBlendTree((BlendTree1D) state, ref markDirty);
            }

            else if (type == typeof(BlendTree2D))
            {
                Draw2DBlendTree((BlendTree2D) state, ref markDirty);
            }
            else
            {
                EditorGUILayout.LabelField($"Unknown animation state type: {type.Name}");
            }
        }

        private static void DrawSingleClipState(SingleClipState state, ref bool markDirty, float labelWidth)
        {
            var oldClip = state.clip;
            state.clip = EditorUtilities.ObjectField("Clip", state.clip, labelWidth);
            if (state.clip != oldClip)
            {
                state.OnClipAssigned(state.clip);
                markDirty = true;
            }
        }

        private static void Draw1DBlendTree(BlendTree1D state, ref bool markDirty)
        {
            state.blendVariable = EditorUtilities.TextField("Blend with variable", state.blendVariable, 130f);
            EditorGUI.indentLevel++;

            int swapIndex = -1;
            for (var i = 0; i < state.blendTree.Count; i++)
            {
                var blendTreeEntry = state.blendTree[i];

                EditorGUILayout.BeginHorizontal();
                {
                    var oldClip = blendTreeEntry.clip;
                    blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
                    if (blendTreeEntry.clip != oldClip && blendTreeEntry.clip != null)
                        markDirty |= state.OnClipAssigned(blendTreeEntry.clip);

                    EditorGUI.BeginDisabledGroup(i == 0);
                    if (GUILayout.Button("\u2191", upDownButtonStyle, upDownButtonOptions))
                        swapIndex = i;
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    blendTreeEntry.threshold = EditorUtilities.FloatField($"When '{state.blendVariable}' =", blendTreeEntry.threshold, 150f, 200f);

                    EditorGUI.BeginDisabledGroup(i == state.blendTree.Count - 1);
                    if (GUILayout.Button("\u2193", upDownButtonStyle, upDownButtonOptions))
                        swapIndex = i + 1;
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();

                if (i != state.blendTree.Count - 1)
                {
                    EditorUtilities.Splitter(width:350f);
                }
            }

            if (swapIndex != -1)
            {
                markDirty = true;
                state.blendTree.Swap(swapIndex, swapIndex - 1);
            }

            EditorGUI.indentLevel--;

            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                state.blendTree.Add(new BlendTreeEntry1D());
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void Draw2DBlendTree(BlendTree2D state, ref bool markDirty)
        {
            state.blendVariable = EditorUtilities.TextField("First blend variable", state.blendVariable, 120f);
            state.blendVariable2 = EditorUtilities.TextField("Second blend variable", state.blendVariable2, 120f);
            EditorGUI.indentLevel++;

            int swapIndex = -1;
            for (var i = 0; i < state.blendTree.Count; i++)
            {
                var blendTreeEntry = state.blendTree[i];

                var oldClip = blendTreeEntry.clip;
                blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
                if (blendTreeEntry.clip != oldClip && blendTreeEntry.clip != null)
                    markDirty |= state.OnClipAssigned(blendTreeEntry.clip);

                EditorGUILayout.BeginHorizontal();
                {
                    blendTreeEntry.threshold1 = EditorUtilities.FloatField($"When '{state.blendVariable}' =", blendTreeEntry.threshold1, 150f, 200f);
                    EditorGUI.BeginDisabledGroup(i == 0);
                    if (GUILayout.Button("\u2191", upDownButtonStyle, upDownButtonOptions))
                        swapIndex = i;
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    blendTreeEntry.threshold2 = EditorUtilities.FloatField($"When '{state.blendVariable2}' =", blendTreeEntry.threshold2, 150f, 200f);

                    EditorGUI.BeginDisabledGroup(i == state.blendTree.Count - 1);
                    if (GUILayout.Button("\u2193", upDownButtonStyle, upDownButtonOptions))
                        swapIndex = i + 1;
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();

                if (i != state.blendTree.Count - 1)
                {
                    EditorUtilities.Splitter(width:350f);
                }
            }

            if (swapIndex != -1)
            {
                markDirty = true;
                state.blendTree.Swap(swapIndex, swapIndex - 1);
            }

            EditorGUI.indentLevel--;

            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add blend tree entry", GUILayout.Width(150f)))
                state.blendTree.Add(new BlendTreeEntry2D());
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawBlendTreeEntry(AnimationState state, BlendTreeEntry2D blendTreeEntry, string blendVarName, string blendVarName2,
                                               ref bool markDirty)
        {
            var oldClip = blendTreeEntry.clip;
            blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
            if (blendTreeEntry.clip != oldClip && blendTreeEntry.clip != null)
                markDirty = state.OnClipAssigned(blendTreeEntry.clip);

            blendTreeEntry.threshold1 = EditorUtilities.FloatField($"When '{blendVarName}' =", blendTreeEntry.threshold1, 150f, 200f);
            blendTreeEntry.threshold2 = EditorUtilities.FloatField($"When '{blendVarName2}' =", blendTreeEntry.threshold2, 150f, 200f);
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