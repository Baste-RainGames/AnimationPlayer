using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public static class StateDataDrawer
    {
        private static object reloadChecker;
        private static GUILayoutOption[] upDownButtonOptions;
        private static GUIStyle upDownButtonStyle;

        public static void ReloadCheck()
        {
            if (reloadChecker == null)
            {
                reloadChecker = new object();
                upDownButtonOptions = new[] { GUILayout.Width(25f), GUILayout.Height(15f) };

                upDownButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.UpperCenter,
                    clipping = TextClipping.Overflow
                };
            }
        }

        public static void DrawStateData(AnimationPlayer animationPlayer, int selectedLayer, PersistedInt selectedState, AnimationPlayerEditor currentEditor)
        {
            ReloadCheck();

            var markDirty = false;
            var layer = animationPlayer.layers[selectedLayer];

            if (layer.states.Count == 0)
            {
                EditorGUILayout.LabelField("No states");
                return;
            }

            if (!layer.states.IsInBounds(selectedState))
            {
                Debug.LogError($"Out of bounds state: {selectedState} out of {layer.states.Count} states! Resetting to 0");
                selectedState.SetTo(0);
                return;
            }

            var stateName = layer.states[selectedState].Name;
            EditorGUILayout.LabelField($"Settings for \"{stateName}\":");

            EditorGUI.indentLevel++;
            DrawStateData(layer.states[selectedState], ref markDirty);

            GUILayout.Space(20f);

            EditorGUILayout.BeginHorizontal();
            if (selectedState == 0)
            {
                EditorGUILayout.LabelField("This is the default state of this layer", GUILayout.Width(250f));
            }
            else if (GUILayout.Button("Set as the default state of this layer", GUILayout.Width(250f)))
            {
                layer.states.Swap(selectedState, 0);
                selectedState.SetTo(0);
                markDirty = true;
            }
            GUILayout.FlexibleSpace();
            var deleteThisState = EditorUtilities.AreYouSureButton($"Delete {stateName}", "Are you sure", $"DeleteState_{selectedState}_{selectedLayer}", 1f);
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;

            GUILayout.Space(20f);

            currentEditor.previewer.DrawStatePreview(selectedLayer, selectedState);

            if (deleteThisState)
            {
                DeleteState(animationPlayer, layer, selectedState);
                markDirty = true;
            }

            if (markDirty) {
                currentEditor.MarkDirty();
            }
        }

        public static void DrawStateData(AnimationState state, ref bool markDirty)
        {
            const float labelWidth = 55f;

            var old = state.Name;
            state.Name = EditorUtilities.TextField("Name", state.Name, labelWidth);
            if (old != state.Name)
                markDirty = true;

            state.speed = EditorUtilities.DoubleField("Speed", state.speed, labelWidth);

            switch (state)
            {
                case SingleClip singleClipState:
                    DrawSingleClipState(singleClipState, ref markDirty, labelWidth);
                    break;
                case BlendTree1D blendTree1D:
                    Draw1DBlendTree(blendTree1D, ref markDirty);
                    break;
                case BlendTree2D blendTree2D:
                    Draw2DBlendTree(blendTree2D, ref markDirty);
                    break;
                case PlayRandomClip playRandom:
                    DrawSelectRandomState(playRandom, ref markDirty);
                    break;
                default:
                    EditorGUILayout.LabelField($"Unknown animation state type: {(state == null ? "null" : state.GetType().Name)}");
                    break;
            }
        }

        private static void DrawSingleClipState(SingleClip state, ref bool markDirty, float labelWidth)
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
            if (state.blendTree == null)
            {
                state.blendTree = new System.Collections.Generic.List<BlendTreeEntry1D>();
            }
            for (var i = 0; i < state.blendTree.Count; i++)
            {
                var blendTreeEntry = state.blendTree[i];

                EditorGUILayout.BeginHorizontal();
                {
                    var oldClip = blendTreeEntry.clip;
                    blendTreeEntry.clip = EditorUtilities.ObjectField("Clip", blendTreeEntry.clip, 150f, 200f);
                    if (blendTreeEntry.clip != oldClip) {
                        if(blendTreeEntry.clip != null)
                            state.OnClipAssigned(blendTreeEntry.clip);
                        markDirty = true;
                    }

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

                    // Remove 1D blend tree entry
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        state.blendTree.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();

                if (i != state.blendTree.Count - 1)
                {
                    EditorUtilities.Splitter(width: 350f);
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
            {
                state.blendTree.Add(new BlendTreeEntry1D());
                markDirty = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void Draw2DBlendTree(BlendTree2D state, ref bool markDirty)
        {
            state.blendVariable = EditorUtilities.TextField("First blend variable", state.blendVariable, 120f);
            state.blendVariable2 = EditorUtilities.TextField("Second blend variable", state.blendVariable2, 120f);
            EditorGUI.indentLevel++;

            if (state.blendTree == null)
            {
                state.blendTree = new System.Collections.Generic.List<BlendTreeEntry2D>();
            }

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

                    // Remove 2D blend tree entry
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        state.blendTree.RemoveAt(i);
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
            {
                state.blendTree.Add(new BlendTreeEntry2D());
                markDirty = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSelectRandomState(PlayRandomClip state, ref bool markDirty)
        {
            EditorGUILayout.LabelField("Select randomly from:");
            EditorGUI.indentLevel++;
            for (var i = 0; i < state.clips.Count; i++)
            {
                var oldClip = state.clips[i];

                state.clips[i] = EditorUtilities.ObjectField("Clip", oldClip, 150f, 200f);
                if (state.clips[i] != oldClip)
                {
                    state.OnClipAssigned(state.clips[i]);
                    markDirty = true;
                }
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add new choice", GUILayout.Width(150f)))
            {
                state.clips.Add(null);
                markDirty = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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