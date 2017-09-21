using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using EditMode = Animation_Player.AnimationPlayerEditor.AnimationPlayerEditMode;
using PersistedEditMode = Animation_Player.AnimationPlayerEditor.PersistedAnimationPlayerEditMode;

namespace Animation_Player
{
    public static class StateSelectionAndAdditionDrawer
    {
        private static GUILayoutOption width;

        public static void Draw(AnimationPlayer animationPlayer, PersistedInt selectedLayer,
                                PersistedInt selectedState, PersistedEditMode selectedEditMode, ref bool shouldUpdateStateNames)
        {
            width = width ?? (width = GUILayout.Width(200f));

            EditorGUILayout.BeginVertical(width);
            shouldUpdateStateNames |= DrawSelection(animationPlayer, selectedLayer, selectedState, selectedEditMode);
            EditorGUILayout.EndVertical();
        }

        private static bool DrawSelection(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                          PersistedEditMode selectedEditMode)
        {
            var shouldUpdateStateNames = false;
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

            var dragAndDropRect = EditorUtilities.ReserveRect(GUILayout.Height(30f));
            shouldUpdateStateNames |= DoDragAndDrop(dragAndDropRect, animationPlayer, selectedLayer, selectedState);

            if (GUILayout.Button("Add Normal State", width))
            {
                Undo.RecordObject(animationPlayer, "Add state to animation player");
                layer.states.Add(AnimationState.SingleClip(GetUniqueStateName(AnimationState.DefaultSingleClipName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                shouldUpdateStateNames = true;
            }

            if (GUILayout.Button("Add 1D Blend Tree", width))
            {
                Undo.RecordObject(animationPlayer, "Add blend tree to animation player");
                layer.states.Add(AnimationState.BlendTree1D(GetUniqueStateName(AnimationState.Default1DBlendTreeName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                shouldUpdateStateNames = true;
            }

            if (GUILayout.Button("Add 2D Blend Tree", width))
            {
                Undo.RecordObject(animationPlayer, "Add 2D blend tree to animation player");
                layer.states.Add(AnimationState.BlendTree2D(GetUniqueStateName(AnimationState.Default2DBlendTreeName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                shouldUpdateStateNames = true;
            }

            return shouldUpdateStateNames;
        }

        private static bool DoDragAndDrop(Rect dragAndDropRect, AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState)
        {
            Event evt = Event.current;

            if (DragAndDrop.objectReferences.Length > 0)
                GUI.Box(dragAndDropRect, "Drag clips here to\nadd them to the player!");

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dragAndDropRect.Contains(evt.mousePosition))
                        return false;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        var animationClips = DragAndDrop.objectReferences.Select(obj => obj as AnimationClip).Where(c => c != null).ToArray();
                        if (animationClips.Length > 0)
                        {
                            Undo.RecordObject(animationPlayer, "Added clip to Animation Player");
                            var layer = animationPlayer.layers[selectedLayer];
                            int numClipsBefore = layer.states.Count;

                            foreach (var clip in animationClips)
                            {
                                var newStateName = GetUniqueStateName(clip.name, layer.states);
                                var newState = AnimationState.SingleClip(newStateName, clip);
                                layer.states.Add(newState);
                            }

                            selectedState.SetTo(numClipsBefore);

                            return true;
                        }
                    }
                    break;
            }
            return false;
        }

        private static string GetUniqueStateName(string wantedName, List<AnimationState> otherStates)
        {
            if (otherStates.All(state => state.Name != wantedName))
                return wantedName;

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
}