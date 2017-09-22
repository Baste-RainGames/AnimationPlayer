using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AnimationStateType = Animation_Player.AnimationState.AnimationStateType;
using Object = UnityEngine.Object;

namespace Animation_Player
{
    [CustomEditor(typeof(AnimationPlayer))]
    public class AnimationPlayerEditor : Editor
    {
        private AnimationPlayer animationPlayer;

        public enum AnimationPlayerEditMode
        {
            States,
            Transitions,
            MetaData
        }

        private PersistedInt selectedLayer;
        private PersistedInt selectedState;
        private PersistedInt selectedToState;
        private PersistedAnimationPlayerEditMode selectedEditMode;

        private string[][] allStateNames;

        private static bool stylesCreated = false;
        private static GUIStyle editLayerStyle;
        private static GUIStyle editLayerButton_Background;
        private static GUIStyle editLayerButton_NotSelected;
        private static GUIStyle editLayerButton_Selected;

        private PersistedBool usedClipsFoldout;
        private PersistedBool usedModelsFoldout;
        private List<AnimationClip> animationClipsUsed;
        private List<Object> modelsUsed;

        public AnimationStatePreviewer previewer;

        private bool stateNamesNeedsUpdate;
        private bool usedClipsNeedsUpdate;

        public void MarkDirty()
        {
            stateNamesNeedsUpdate = true;
            usedClipsNeedsUpdate = true;
        }

        private void OnEnable()
        {
            animationPlayer = (AnimationPlayer) target;
            HandleInitialization();

            if (animationPlayer.EnsureVersionUpgraded())
            {
                EditorUtility.SetDirty(animationPlayer);
                if (animationPlayer.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(animationPlayer.gameObject.scene);
            }
            
            animationPlayer.editTimeUpdateCallback -= Repaint;
            animationPlayer.editTimeUpdateCallback += Repaint;

            Undo.undoRedoPerformed -= CheckSelectionBounds;
            Undo.undoRedoPerformed += CheckSelectionBounds;

            var instanceId = animationPlayer.GetInstanceID();

            selectedLayer = new PersistedInt(persistedLayer + instanceId);
            selectedEditMode = new PersistedAnimationPlayerEditMode(persistedEditMode + instanceId);
            selectedState = new PersistedInt(persistedState + instanceId);
            selectedToState = new PersistedInt(persistedToState + instanceId);
            usedClipsFoldout = new PersistedBool(persistedFoldoutUsedClips + instanceId);
            usedModelsFoldout = new PersistedBool(persistedFoldoutUsedModels + instanceId);

            stateNamesNeedsUpdate = true;

            previewer = new AnimationStatePreviewer(animationPlayer);
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
                StateSelectionAndAdditionDrawer.Draw(animationPlayer, selectedLayer, selectedState, selectedEditMode, this);

                EditorUtilities.DrawVertical(DrawSelectedState);
            });

            EditorUtilities.Splitter();

            EditorGUILayout.LabelField("Default transition");

            Undo.RecordObject(animationPlayer, "Change default transition");
            animationPlayer.defaultTransition = AnimationTransitionDrawer.DrawTransitionData(animationPlayer.defaultTransition);

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
                    normal = {background = buttonNotSelectedText},
                    alignment = TextAnchor.MiddleCenter
                };

                editLayerButton_Selected = new GUIStyle(GUI.skin.label)
                {
                    normal = {background = buttonSelectedTex},
                    alignment = TextAnchor.MiddleCenter
                };

                stylesCreated = true;
            }

            if (animationPlayer.layers == null || animationPlayer.layers.Length == 0)
            {
                animationPlayer.layers = new AnimationLayer[1];
                animationPlayer.layers[0] = AnimationLayer.CreateLayer();

                if (animationPlayer.defaultTransition == default(TransitionData))
                    animationPlayer.defaultTransition = TransitionData.Linear(.1f); //default shouldn't be snap
                return;
            }

            if (stateNamesNeedsUpdate)
            {
                stateNamesNeedsUpdate = false;
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
                MarkDirty();
            }
            EditorGUI.BeginDisabledGroup(numLayers < 2);
            if (EditorUtilities.AreYouSureButton("Delete layer", "Are you sure?", "DeleteLayer" + selectedLayer, 1f, GUILayout.Width(100f)))
            {
                Undo.RecordObject(animationPlayer, "Delete layer from animation player");
                EditorUtilities.DeleteIndexFromArray(ref animationPlayer.layers, selectedLayer);
                selectedLayer.SetTo(Mathf.Max(0, selectedLayer - 1));
                MarkDirty();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedLayer()
        {
            var layer = animationPlayer.layers[selectedLayer];

            layer.startWeight = EditorGUILayout.Slider($"Layer {selectedLayer} Weight", layer.startWeight, 0f, 1f);
            layer.mask = EditorUtilities.ObjectField($"Layer {selectedLayer} Mask", layer.mask);
        }

        private void DrawSelectedState()
        {
            EditorGUILayout.BeginHorizontal(editLayerButton_Background);

            //Making tabbed views are hard!
            var editStatesRect = EditorUtilities.ReserveRect();
            var editTransitionsRect = EditorUtilities.ReserveRect();
            var metaDataRect = EditorUtilities.ReserveRect();

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
                    selectedEditMode.SetTo((AnimationPlayerEditMode) index);
            };

            Draw(editStatesRect, "Edit state", 0);
            Draw(editTransitionsRect, "Edit Transitions", 1);
            Draw(metaDataRect, "Metadata", 2);

            EditorGUILayout.BeginVertical(editLayerStyle);

            GUILayout.Space(10f);

            switch ((AnimationPlayerEditMode) selectedEditMode)
            {
                case AnimationPlayerEditMode.States:
                    StateDataDrawer.DrawStateData(animationPlayer, selectedLayer, selectedState, this);
                    break;
                case AnimationPlayerEditMode.Transitions:
                    AnimationTransitionDrawer.DrawTransitions(animationPlayer, selectedLayer, selectedState, selectedToState, allStateNames);
                    break;
                case AnimationPlayerEditMode.MetaData:
                    DrawMetaData();
                    break;
            }

            GUILayout.Space(20f);
            EditorGUILayout.EndVertical();
        }

        private void DrawMetaData()
        {
            EditorGUI.indentLevel++;
            DrawClipsUsed();
            DrawReferencedModels();
            EditorGUI.indentLevel--;
        }

        private void DrawClipsUsed()
        {
            EnsureUsedClipsCached();

            usedClipsFoldout.SetTo(EditorGUILayout.Foldout(usedClipsFoldout, "Clips used in the animation player"));
            if (!usedClipsFoldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var clip in animationClipsUsed)
                EditorUtilities.ObjectField(clip);
            EditorGUI.indentLevel--;
        }

        private void DrawReferencedModels()
        {
            usedModelsFoldout.SetTo(EditorGUILayout.Foldout(usedModelsFoldout, "Models used in the animation player"));
            if (!usedModelsFoldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var model in modelsUsed)
                EditorUtilities.ObjectField(model);
            EditorGUI.indentLevel--;
        }

        private void EnsureUsedClipsCached()
        {
            //Unity persists the value of the bool though a script reload, but not the list. 
            if (!usedClipsNeedsUpdate && (animationClipsUsed == null || animationClipsUsed.Count == 0))
                usedClipsNeedsUpdate = true;

            if (!usedClipsNeedsUpdate)
                return;
            usedClipsNeedsUpdate = false;

            if (animationClipsUsed != null)
                animationClipsUsed.Clear();
            else
                animationClipsUsed = new List<AnimationClip>();

            if (modelsUsed != null)
                modelsUsed.Clear();
            else
                modelsUsed = new List<Object>();

            foreach (var state in animationPlayer.layers.SelectMany(layer => layer.states))
            {
                switch (state.type)
                {
                    case AnimationStateType.SingleClip:
                        if (state.clip != null && !animationClipsUsed.Contains(state.clip))
                            animationClipsUsed.Add(state.clip);
                        break;
                    case AnimationStateType.BlendTree1D:
                    case AnimationStateType.BlendTree2D:
                        foreach (var clip in state.blendTree.Select(bte => bte.clip).Where(c => c != null))
                            if (!animationClipsUsed.Contains(clip))
                                animationClipsUsed.Add(clip);
                        break;
                }
            }

            List<string> usedAssetPaths = new List<string>();
            foreach (var animationClip in animationClipsUsed)
            {
                if (AssetDatabase.IsMainAsset(animationClip))
                    continue; //standalone animation clip

                var modelPath = AssetDatabase.GetAssetPath(animationClip);
                if (!usedAssetPaths.Contains(modelPath))
                    usedAssetPaths.Add(modelPath);
            }

            foreach (var modelPath in usedAssetPaths)
            {
                var model = AssetDatabase.LoadMainAssetAtPath(modelPath);
                modelsUsed.Add(model);
            }
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
                EditorGUILayout.LabelField("weigh for " + i + ": " + animationPlayer.GetStateWeight(i, selectedLayer));
            }

            EditorGUILayout.Space();

            var newBlendVal = EditorGUILayout.Slider("Forward", blendVal, 0f, 1f);
            if (newBlendVal != blendVal)
            {
                blendVal = newBlendVal;
                animationPlayer.SetBlendVar("Forward", blendVal, selectedLayer);
            }

            var newBlendVal2 = EditorGUILayout.Slider("Turn", blendVal2, -1f, 1f);
            if (newBlendVal2 != blendVal2)
            {
                blendVal2 = newBlendVal2;
                animationPlayer.SetBlendVar("Turn", blendVal2, selectedLayer);
            }
        }

        //Undo can remove layers, but the selected bounds are not undone, so do that manually
        private void CheckSelectionBounds()
        {
            if (animationPlayer == null || animationPlayer.layers == null)
                return;

            selectedLayer.SetTo(Mathf.Clamp(selectedLayer, 0, animationPlayer.layers.Length - 1));
            if (animationPlayer.layers.Length == 0)
                return;
            var layer = animationPlayer.layers[selectedLayer];
            selectedState.SetTo(Mathf.Clamp(selectedState, 0, layer.states.Count - 1));
        }

        private float blendVal;
        private float blendVal2;

        private const float selectedLayerWidth = 108f;

        private const string persistedLayer = "APE_SelectedLayer_";
        private const string persistedState = "APE_SelectedState_";
        private const string persistedToState = "APE_SelectedToState_";
        private const string persistedEditMode = "APE_EditMode_";
        private const string persistedFoldoutUsedClips = "APE_FO_UsedClips_";
        private const string persistedFoldoutUsedModels = "APE_FO_UsedModels_";

        public class PersistedAnimationPlayerEditMode : PersistedVal<AnimationPlayerEditMode>
        {
            public PersistedAnimationPlayerEditMode(string key) : base(key)
            { }

            protected override int ToInt(AnimationPlayerEditMode val)
            {
                return (int) val;
            }

            protected override AnimationPlayerEditMode ToType(int i)
            {
                return (AnimationPlayerEditMode) i;
            }

            public static implicit operator int(PersistedAnimationPlayerEditMode p)
            {
                return p.ToInt(p); //look at it go!
            }
        }

        public override bool RequiresConstantRepaint()
        {
            return base.RequiresConstantRepaint() || previewer.IsShowingPreview;
        }

        private void OnDestroy()
        {
            previewer.Cleanup();
        }
    }

}