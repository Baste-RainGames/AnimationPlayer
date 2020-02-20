using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Animation_Player
{
    [CustomEditor(typeof(AnimationPlayer))]
    public class AnimationPlayerEditor : Editor
    {
        private AnimationPlayer animationPlayer;
        private SerializedObject animationPlayerSO;
        private SerializedProperty layersSP;

        public enum AnimationPlayerEditMode
        {
            States,
            Transitions,
            Events,
            MetaData
        }

        private PersistedInt selectedLayer;
        private PersistedInt selectedState;
        private PersistedShortPair selectedToState;
        private PersistedAnimationPlayerEditMode selectedEditMode;

        private int SelectedLayer
        {
            get => selectedLayer;
            set => selectedLayer.SetTo(value);
        }
        private int SelectedState
        {
            get => selectedState;
            set => selectedState.SetTo(value);
        }
        private AnimationPlayerEditMode SelectedEditMode
        {
            get => selectedEditMode;
            set => selectedEditMode.SetTo(value);
        }

        private string[][] allStateNames;

        private static bool stylesCreated;
        private static GUIStyle editLayerStyle;
        private static GUIStyle editLayerButton_Background;
        private static GUIStyle editLayerButton_NotSelected;
        private static GUIStyle editLayerButton_Selected;
        private static GUIStyle centeredLabel;

        private MetaDataDrawer metaDataDrawer;

        public AnimationStatePreviewer previewer;
        private List<AnimationClip> multiChoiceAnimationClips;

        private object scriptReloadChecker;

        private bool stateNamesNeedsUpdate = true;
        private bool transitionsNeedsUpdate = true;
        private bool drawAssignedClips;

        public void MarkDirty()
        {
            stateNamesNeedsUpdate = true;
            metaDataDrawer.usedClipsNeedsUpdate = true;
            EditorUtilities.SetDirty(animationPlayer);
            EditorSceneManager.MarkSceneDirty(animationPlayer.gameObject.scene);
        }

        private void OnEnable()
        {
            HandleInitialization(false);

            if (animationPlayer.EnsureVersionUpgraded())
            {
                EditorUtility.SetDirty(animationPlayer);
                if (animationPlayer.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(animationPlayer.gameObject.scene);
            }

            Undo.undoRedoPerformed -= CheckSelectionBounds;
            Undo.undoRedoPerformed += CheckSelectionBounds;

            var instanceId = animationPlayer.GetInstanceID();

            selectedLayer    = new PersistedInt(persistedLayer + instanceId);
            selectedState    = new PersistedInt(persistedState + instanceId);
            selectedToState  = new PersistedShortPair(persistedToState + instanceId);
            selectedEditMode = new PersistedAnimationPlayerEditMode(persistedEditMode + instanceId);

            stateNamesNeedsUpdate = true;
        }

        private void HandleInitialization(bool isGUICall)
        {
            if (scriptReloadChecker == null)
            {
                // Unity persists some objects through reload, and fails to persist others. This makes it hard to figure out if
                // something needs to be re-cached. This solves that - we know that Unity can't persist a raw object, so if it's null, a reload is neccessary.
                scriptReloadChecker = new object();

                animationPlayer = (AnimationPlayer) target;
                animationPlayerSO = serializedObject;
                layersSP = animationPlayerSO.FindProperty(nameof(AnimationPlayer.layers));

                metaDataDrawer = new MetaDataDrawer(animationPlayer);
                stylesCreated = false;
                transitionsNeedsUpdate = true;
            }

            if (isGUICall && !stylesCreated)
            {
                var backgroundTex = EditorUtilities.MakeTex(1, 1, new Color(1.0f, 1.0f, 1.0f, .1f));
                editLayerStyle = new GUIStyle {normal = {background = backgroundTex}};

                centeredLabel = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.UpperCenter
                };

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

                if (animationPlayer.defaultTransition == default)
                    animationPlayer.defaultTransition = TransitionData.Linear(.1f); //default shouldn't be snap
                EditorUtilities.SetDirty(animationPlayer);
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

            if (transitionsNeedsUpdate)
            {
                transitionsNeedsUpdate = false;
                foreach (var layer in animationPlayer.layers)
                {
                    foreach (var transition in layer.transitions)
                    {
                        transition.FetchStates(layer.states);
                    }
                }
            }

            if (previewer == null)
                previewer = new AnimationStatePreviewer(animationPlayer);
        }

        public override void OnInspectorGUI()
        {
            HandleInitialization(true);

            if (animationPlayer.layers == null || animationPlayer.layers.Length == 0)
                return;

            EditorUtilities.Splitter();

            DrawLayerSelection();

            if (animationPlayer.layers.Length == 0)
                return; //Deleted last layer in DrawLayerSelection

            if (SelectedState == -1 && animationPlayer.layers[SelectedLayer].states.Count > 0)
                SelectedState = 0; //Handle adding a state for the first time.

            GUILayout.Space(10f);

            DrawSelectedLayer();

            GUILayout.Space(10f);
            EditorUtilities.Splitter();

            var numStatesBefore = animationPlayer.layers[SelectedLayer].states.Count;
            StateSelectionAndAdditionDrawer.Draw(animationPlayer, selectedLayer, selectedState, this, multiChoiceAnimationClips);
            if (numStatesBefore != animationPlayer.layers[SelectedLayer].states.Count)
            {
                Repaint();
                return;
            }

            DrawSelectedState();

            EditorUtilities.Splitter();

            DrawRuntimeDebugData();
        }

        private void DrawLayerSelection()
        {
            var numLayers = animationPlayer.layers.Length;

            SelectedLayer = Mathf.Clamp(SelectedLayer, 0, numLayers);

            var screenWidth = Screen.width;
            var twoLines = screenWidth < 420f;

            EditorGUILayout.BeginHorizontal();
            {
                var old = SelectedLayer;
                SelectedLayer = EditorUtilities.DrawLeftButton(SelectedLayer);

                var layerName = animationPlayer.layers[SelectedLayer].name;
                if (string.IsNullOrWhiteSpace(layerName))
                    layerName = $"Layer {SelectedLayer} (no name)";

                var layerLabel = new GUIContent(layerName);

                float labelWidth;

                if (twoLines) {
                    const float otherElementsWidth = (2 * 24f) // left/right button width
                                                   + (2 * 4f)  // default spacing between elements
                                                   + 25f       // scroll bar, margin.
                                                   + 5f;       // my maths is off slightly.
                    labelWidth = screenWidth - otherElementsWidth;
                }
                else {
                    const float otherElementsWidth = (2 * 24f)  // left/right button width
                                                   + (2 * 100f) // Add layer/delete layer width
                                                   + (6 * 4f)   // default spacing between elements
                                                   + 25f        // scroll bar, margin.
                                                   + 10f;       // spacing between right button and add layer button
                    labelWidth = screenWidth - otherElementsWidth;
                }

                EditorGUILayout.LabelField(layerLabel, centeredLabel, GUILayout.Width(labelWidth));
                SelectedLayer = EditorUtilities.DrawRightButton(SelectedLayer, numLayers);

                if (SelectedLayer != old && Event.current.button == 1) {
                    var swap = animationPlayer.layers[old];
                    animationPlayer.layers[old] = animationPlayer.layers[SelectedLayer];
                    animationPlayer.layers[SelectedLayer] = swap;
                    MarkDirty();
                }

                if (twoLines)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    GUILayout.Space(10f);
                }

                if (GUILayout.Button("Add layer", GUILayout.Width(100f)))
                {
                    layersSP.arraySize++;
                    var newLayerIndex = layersSP.arraySize - 1;
                    var newLayer = layersSP.GetArrayElementAtIndex(newLayerIndex);
                    SerializedPropertyHelper.SetValue(newLayer, AnimationLayer.CreateLayer());
                    SelectedLayer = newLayerIndex;
                }

                EditorGUI.BeginDisabledGroup(numLayers < 2);
                {
                    if (EditorUtilities.AreYouSureButton("Delete layer", "Are you sure?", "DeleteLayer" + SelectedLayer, 1f, GUILayout.Width(100f)))
                    {
                        EditorUtilities.RecordUndo(animationPlayer, "Delete layer from animation player");
                        EditorUtilities.DeleteIndexFromArray(ref animationPlayer.layers, SelectedLayer);
                        SelectedLayer = Mathf.Max(0, SelectedLayer - 1);
                        MarkDirty();
                    }
                }
                EditorGUI.EndDisabledGroup();

                if (twoLines) {
                    GUILayout.FlexibleSpace();
                }
            }
            animationPlayerSO.ApplyModifiedProperties();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedLayer()
        {
            Undo.RecordObject(animationPlayer, "changing layer settings");

            var layer = animationPlayer.layers[SelectedLayer];

            layer.name = EditorGUILayout.TextField($"Layer {SelectedLayer} Name", layer.name);
            layer.startWeight = EditorGUILayout.Slider($"Layer {SelectedLayer} Weight", layer.startWeight, 0f, 1f);
            layer.mask = EditorUtilities.ObjectField($"Layer {SelectedLayer} Mask", layer.mask);

            if(SelectedLayer > 0) //Doesn't make any sense for base layer to be additive!
                layer.type = (AnimationLayerType) EditorGUILayout.EnumPopup("Type", layer.type);
            else
                EditorGUILayout.LabelField(string.Empty);
        }

        private void DrawSelectedState()
        {
            EditorGUILayout.BeginHorizontal(editLayerButton_Background);

            //Making tabbed views are hard!
            var editStatesRect = EditorUtilities.ReserveRect();
            var editTransitionsRect = EditorUtilities.ReserveRect();
            var eventsRect = EditorUtilities.ReserveRect();
            var metaDataRect = EditorUtilities.ReserveRect();

            EditorGUILayout.EndHorizontal();

            DrawTabHeader(editStatesRect, "States", 0);
            DrawTabHeader(editTransitionsRect, "Transitions", 1);
            DrawTabHeader(eventsRect, "Events", 2);
            DrawTabHeader(metaDataRect, "Metadata", 3);

            EditorGUILayout.BeginVertical(editLayerStyle);

            GUILayout.Space(10f);

            switch (SelectedEditMode)
            {
                case AnimationPlayerEditMode.States:
                    StateDataDrawer.DrawStateData(animationPlayer, SelectedLayer, selectedState, this);
                    break;
                case AnimationPlayerEditMode.Transitions:
                    AnimationTransitionDrawer.DrawTransitions(animationPlayer, SelectedLayer, SelectedState, selectedToState, allStateNames[SelectedLayer]);
                    break;
                case AnimationPlayerEditMode.Events:
                    DrawEvents();
                    break;
                case AnimationPlayerEditMode.MetaData:
                    metaDataDrawer.DrawMetaData();
                    break;
            }

            GUILayout.Space(20f);
            EditorGUILayout.EndVertical();
        }

        private void DrawTabHeader(Rect rect, string label, int index)
        {
            //Hack: expand the rects so they hit the next control
            rect.yMax += 2;
            rect.yMin -= 2;

            //Expand the first and last rects so they are aligned with the box below
            if (index == 0)
            {
                rect.x -= 4;
                rect.xMax += 4;
            }
            else if (index == 3)
            {
                rect.x += 4;
                rect.xMin -= 4;
            }

            var isSelected = index == (int) SelectedEditMode;
            var style = isSelected ? editLayerButton_Selected : editLayerButton_NotSelected;
            if (GUI.Button(rect, label, style))
                SelectedEditMode = (AnimationPlayerEditMode) index;
        }

        private void DrawEvents()
        {
            if (animationPlayer.layers[SelectedLayer].states.Count == 0)
            {
                EditorGUILayout.LabelField("No states on layer, can't make events");
                return;
            }

            var state = animationPlayer.GetState(SelectedState, SelectedLayer);
            int indexToDelete = -1;
            EditorGUILayout.LabelField($"Animation events for {state.Name}:");
            EditorUtilities.DrawIndented(() =>
            {
                for (var i = 0; i < state.animationEvents.Count; i++)
                {
                    if (DrawEvent(state.animationEvents[i], state))
                        indexToDelete = i;
                    EditorUtilities.Splitter();
                }
            });

            if (indexToDelete != -1)
                state.animationEvents.RemoveAt(indexToDelete);

            EditorUtilities.DrawHorizontal(() =>
            {
                if (GUILayout.Button("Create new Animation Event"))
                {
                    EditorUtilities.RecordUndo(animationPlayer, $"Added animation event to {state.Name}");
                    state.animationEvents.Add(new AnimationEvent {name = "New Event"});
                    MarkDirty();
                }
                GUILayout.FlexibleSpace();
            });
        }


        private bool DrawEvent(AnimationEvent animationEvent, AnimationPlayerState containingState)
        {
            const float  eventLabelWidth = 100f;
            const string nameTooltip = "The name of the event, used when subscribing.";
            const string timeTooltip = "When the event should fire, in real seconds.";
            const string mustBeActiveTooltip = "Does the state have to be the active state for the event to fire?";
            const string minWeightTooltip = "The minimum required weight for the state to fire.";

            bool shouldDelete = false;
            EditorGUI.BeginChangeCheck();
            EditorUtilities.DrawHorizontal(() =>
            {
                GUIContent nameContent = new GUIContent("Name", nameTooltip);
                EditorGUILayout.LabelField(nameContent, GUILayout.Width(eventLabelWidth));
                animationEvent.name = EditorGUILayout.TextField(animationEvent.name);
            });
            EditorUtilities.DrawHorizontal(() =>
            {
                var timeContent = new GUIContent("Time", timeTooltip);
                EditorGUILayout.LabelField(timeContent, GUILayout.Width(eventLabelWidth));
                animationEvent.time = EditorGUILayout.Slider((float) animationEvent.time, 0f, containingState.Duration);
            });
            EditorUtilities.DrawHorizontal(() =>
            {
                var mustBeActiveContent = new GUIContent("Must be active", mustBeActiveTooltip);
                EditorGUILayout.LabelField(mustBeActiveContent, GUILayout.Width(eventLabelWidth));
                animationEvent.mustBeActiveState = EditorGUILayout.Toggle(animationEvent.mustBeActiveState);
            });
            EditorUtilities.DrawHorizontal(() =>
            {
                var minWeightContent = new GUIContent("Minimum weight", minWeightTooltip);
                EditorGUILayout.LabelField(minWeightContent, GUILayout.Width(eventLabelWidth));
                animationEvent.minWeight = EditorGUILayout.Slider(animationEvent.minWeight, 0f, 1f);
            });
            EditorUtilities.DrawHorizontal(() =>
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button($"Delete '{animationEvent.name}'"))
                    shouldDelete = true;
            });
            if (EditorGUI.EndChangeCheck())
                EditorUtilities.SetDirty(animationPlayer);

            return shouldDelete;
        }


        public void StartDragAndDropMultiChoice(List<AnimationClip> animationClips)
        {
            multiChoiceAnimationClips = animationClips;
        }

        public void StopDragAndDropMultiChoice()
        {
            multiChoiceAnimationClips = null;
            //Repaint();
        }

        private void DrawRuntimeDebugData()
        {
            if (!Application.isPlaying)
                return;

            EditorGUILayout.LabelField("Current blend variable values:");
            var blendVars = animationPlayer.GetBlendVariables();
            EditorUtilities.DrawIndented(() =>
            {
                var maxWidth = 100f;
                foreach (var blendVar in blendVars)
                {
                    GUI.skin.label.CalcMinMaxWidth(new GUIContent(blendVar), out _, out var varNameMaxWidth);
                    maxWidth = Mathf.Max(maxWidth, varNameMaxWidth);
                }

                foreach (var blendVar in blendVars)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(blendVar, GUILayout.Width(maxWidth));
                        EditorGUILayout.LabelField(animationPlayer.GetBlendVar(blendVar).ToString());
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });

            EditorUtilities.Splitter();
            drawAssignedClips = EditorGUILayout.Toggle("Draw assigned clips", drawAssignedClips);
            EditorUtilities.Splitter();

            if (!animationPlayer.gameObject.scene.IsValid())
            {
                //is looking at the prefab at runtime, don't attempt to draw the graph!
                return;
            }

            EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetPlayingState(SelectedLayer));
            for (int i = 0; i < animationPlayer.GetStateCount(SelectedLayer); i++)
            {
                var state = animationPlayer.layers[SelectedLayer].states[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Current weight for state \"{state.Name}\": {animationPlayer.GetStateWeight(i, SelectedLayer)}");

                    if (GUILayout.Button("Play it!"))
                        animationPlayer.Play(i, SelectedLayer);
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    if (state is SingleClip singleClip)
                    {
                        EditorGUILayout.LabelField($"default clip: {ClipName(singleClip.clip)}");
                        var (clip, weight) = animationPlayer.layers[selectedLayer].GetCurrentActualClipAndWeight(state);
                        if (clip != singleClip.clip)
                            EditorGUILayout.LabelField($"overriden clip: {ClipName(clip)}, weight: {weight}");
                    }

                    if (state is BlendTree1D bt1d)
                    {
                        for (int j = 0; j < bt1d.blendTree.Count; j++)
                        {
                            EditorGUILayout.LabelField($"assigned {ClipName(bt1d.blendTree[j].clip)}");
                            var (clip, weight) = animationPlayer.layers[selectedLayer].GetCurrentActualClipAndWeight(state, j);
                            if (clip != bt1d.blendTree[j].clip)
                                EditorGUILayout.LabelField($"overriden clip: {ClipName(clip)}, weight: {weight}");
                        }
                    }
                }
            }
        }

        private string ClipName(AnimationClip clip) => clip == null ? "null" : clip.name;

        //Undo can remove layers, but the selected bounds are not undone, so do that manually
        private void CheckSelectionBounds()
        {
            if (animationPlayer == null || animationPlayer.layers == null)
                return;

            SelectedLayer = Mathf.Clamp(SelectedLayer, 0, animationPlayer.layers.Length - 1);
            if (animationPlayer.layers.Length == 0)
                return;
            var layer = animationPlayer.layers[SelectedLayer];
            SelectedState = Mathf.Clamp(SelectedState, 0, layer.states.Count - 1);
        }

        // private const float selectedLayerWidth = 108f;

        private const string persistedLayer = "APE_SelectedLayer_";
        private const string persistedState = "APE_SelectedState_";
        private const string persistedToState = "APE_SelectedToState_";
        private const string persistedEditMode = "APE_EditMode_";

        public class PersistedAnimationPlayerEditMode : PersistedVal<AnimationPlayerEditMode>
        {
            public PersistedAnimationPlayerEditMode(string key) : base(key) { }

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
            return Application.isPlaying || (previewer != null && previewer.IsShowingPreview) || DragAndDrop.objectReferences.Length > 0;
        }

        private void OnDestroy() {
            previewer?.Cleanup();
        }
    }
}