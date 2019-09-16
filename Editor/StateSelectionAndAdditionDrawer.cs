using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using EditMode = Animation_Player.AnimationPlayerEditor.AnimationPlayerEditMode;

namespace Animation_Player
{
    public static class StateSelectionAndAdditionDrawer
    {
        private static GUILayoutOption buttonWidth;
        private static GUIContent selectedStateLabel;
        private static GUIStyle popupStyle;
        private static GUIStyle dragAndDropBoxStyle;

        public static void Draw(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState, AnimationPlayerEditor editor,
                                List<AnimationClip> multiSelectClips)
        {
            if (popupStyle == null)
            {
                buttonWidth = GUILayout.Width(200f);
                selectedStateLabel = new GUIContent("Selected state: ");

                popupStyle = new GUIStyle(EditorStyles.popup)
                {
                    fontSize = GUI.skin.button.fontSize,
                    font = GUI.skin.button.font,
                    fontStyle = GUI.skin.button.fontStyle,
                    fixedHeight = 20f,
                    alignment = TextAnchor.MiddleCenter
                };
                dragAndDropBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12,
                };
            }

            var layer = animationPlayer.layers[selectedLayer];

            var wrap = Screen.width < 420f;
            if (wrap)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginVertical();
                {
                    DrawSelection(layer, selectedState);
                    GUILayout.Space(10f);
                    DrawAddition(animationPlayer, selectedLayer, selectedState, editor, layer, multiSelectClips);
                }
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    DrawSelection(layer, selectedState);
                    GUILayout.Space(20f);
                    DrawAddition(animationPlayer, selectedLayer, selectedState, editor, layer, multiSelectClips);

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawSelection(AnimationLayer selectedLayer, PersistedInt selectedState)
        {
            UpdateLayerDropdownNames(selectedLayer);

            EditorGUILayout.BeginVertical(buttonWidth);
            {
                EditorGUILayout.LabelField(selectedStateLabel, buttonWidth);
                selectedState.SetTo(EditorGUILayout.Popup(selectedState, selectedLayer.layersForEditor, popupStyle, buttonWidth));
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawAddition(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState, AnimationPlayerEditor editor,
                                         AnimationLayer layer, List<AnimationClip> multiSelectClips)
        {
            if (multiSelectClips != null && multiSelectClips.Count > 0)
            {
                EditorGUILayout.BeginVertical(buttonWidth);
                {
                    DrawMultiSelectChoice(animationPlayer, selectedLayer, selectedState, multiSelectClips, editor);
                }
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.BeginVertical(buttonWidth);
            {
                EditorGUILayout.LabelField("Add new state:");
                if (DragAndDrop.objectReferences.Length > 0)
                    DoDragAndDrop(animationPlayer, selectedLayer, selectedState, editor);
                else
                    DrawAddStateButtons(animationPlayer, selectedState, editor, layer);
            }
            EditorGUILayout.EndVertical();
        }

        private static void DoDragAndDrop(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState, AnimationPlayerEditor editor)
        {
            var dragAndDropRect = EditorUtilities.ReserveRect(GUILayout.Height(83f));
            Event evt = Event.current;

            GUI.Box(dragAndDropRect, "Drag clips here to add\nthem to the player!", dragAndDropBoxStyle);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dragAndDropRect.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        var animationClips = DragAndDrop.objectReferences.OfType<AnimationClip>().ToList();
                        foreach (var obj in DragAndDrop.objectReferences.OfType<GameObject>())
                        {
                            var assetPath = AssetDatabase.GetAssetPath(obj);
                            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter))
                                return;

                            animationClips.AddRange(AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().
                                                                  Where(clip => (clip.hideFlags & HideFlags.HideInHierarchy) == 0));
                        }

                        if (animationClips.Count == 1)
                        {
                            AddClipsAsSeperateStates(animationPlayer, selectedLayer, selectedState, editor, animationClips);
                        }
                        else if (animationClips.Count > 1)
                        {
                            editor.StartDragAndDropMultiChoice(animationClips);
                        }
                    }

                    break;
            }
        }

        private static void DrawMultiSelectChoice(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                                  List<AnimationClip> multiSelectClips, AnimationPlayerEditor editor)
        {

            EditorGUILayout.LabelField($"Adding {multiSelectClips.Count} clips! How do you want to add them?");

            // @TODO: Sequences, Random Selection

            if (GUILayout.Button("Separate Clips"))
            {
                AddClipsAsSeperateStates(animationPlayer, selectedLayer, selectedState, editor, multiSelectClips);
                editor.StopDragAndDropMultiChoice();
            }

            if (GUILayout.Button("Blend Tree"))
            {
                AddClipsAsBlendTree(animationPlayer, selectedLayer, selectedState, editor, multiSelectClips);
                editor.StopDragAndDropMultiChoice();
            }

            if (GUILayout.Button("Cancel"))
            {
                editor.StopDragAndDropMultiChoice();
            }
        }

        private static void AddClipsAsSeperateStates(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                                     AnimationPlayerEditor editor, List<AnimationClip> animationClips)
        {
            EditorUtilities.RecordUndo(animationPlayer, "Added clip to Animation Player");
            var layer = animationPlayer.layers[selectedLayer];
            int numClipsBefore = layer.states.Count;

            foreach (var clip in animationClips)
            {
                var newStateName = GetUniqueStateName(clip.name, layer.states);
                var newState = SingleClip.Create(newStateName, clip);
                layer.states.Add(newState);
            }

            selectedState.SetTo(numClipsBefore);

            editor.MarkDirty();
        }

        private static void AddClipsAsBlendTree(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                                AnimationPlayerEditor editor, List<AnimationClip> animationClips)
        {
            EditorUtilities.RecordUndo(animationPlayer, "Added clip to Animation Player");
            var layer = animationPlayer.layers[selectedLayer];
            int numClipsBefore = layer.states.Count;

            var newStateName = GetUniqueStateName(BlendTree1D.DefaultName, layer.states);
            var newState = BlendTree1D.Create(newStateName);

            foreach (var clip in animationClips)
            {
                var newEntry = new BlendTreeEntry1D
                {
                    clip = clip
                };
                newState.blendTree.Add(newEntry);
            }

            layer.states.Add(newState);

            selectedState.SetTo(numClipsBefore);

            editor.MarkDirty();
        }

        private static void DrawAddStateButtons(AnimationPlayer animationPlayer, PersistedInt selectedState, AnimationPlayerEditor editor, AnimationLayer layer)
        {
            if (GUILayout.Button("Single Clip", buttonWidth))
            {
                EditorUtilities.RecordUndo(animationPlayer, "Add state to animation player");
                layer.states.Add(SingleClip.Create(GetUniqueStateName(SingleClip.DefaultName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                editor.MarkDirty();
            }

            if (GUILayout.Button("Play Random Clip", buttonWidth))
            {
                EditorUtilities.RecordUndo(animationPlayer, "Add random state to animation player");
                layer.states.Add(PlayRandomClip.Create(GetUniqueStateName(PlayRandomClip.DefaultName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                editor.MarkDirty();
            }

            if (GUILayout.Button("Sequence", buttonWidth))
            {
                EditorUtilities.RecordUndo(animationPlayer, "Add sequence to animation player");
                layer.states.Add(Sequence.Create(GetUniqueStateName(Sequence.DefaultName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                editor.MarkDirty();
            }

            if (GUILayout.Button("1D Blend Tree", buttonWidth))
            {
                EditorUtilities.RecordUndo(animationPlayer, "Add blend tree to animation player");
                layer.states.Add(BlendTree1D.Create(GetUniqueStateName(BlendTree1D.DefaultName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                editor.MarkDirty();
            }

            if (GUILayout.Button("2D Blend Tree", buttonWidth))
            {
                EditorUtilities.RecordUndo(animationPlayer, "Add 2D blend tree to animation player");
                layer.states.Add(BlendTree2D.Create(GetUniqueStateName(BlendTree2D.DefaultName, layer.states)));
                selectedState.SetTo(layer.states.Count - 1);
                editor.MarkDirty();
            }
        }

        private static string GetUniqueStateName(string wantedName, List<AnimationPlayerState> otherStates)
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

        private static void UpdateLayerDropdownNames(AnimationLayer selectedLayer)
        {
            if (selectedLayer.layersForEditor == null || selectedLayer.layersForEditor.Length != selectedLayer.states.Count)
            {
                selectedLayer.layersForEditor = new GUIContent[selectedLayer.states.Count];
                for (int i = 0; i < selectedLayer.layersForEditor.Length; i++)
                {
                    selectedLayer.layersForEditor[i] = new GUIContent(selectedLayer.states[i].Name);
                }
            }
            else
            {
                for (int i = 0; i < selectedLayer.layersForEditor.Length; i++)
                {
                    selectedLayer.layersForEditor[i].text = selectedLayer.states[i].Name;
                }
            }
        }
    }
}