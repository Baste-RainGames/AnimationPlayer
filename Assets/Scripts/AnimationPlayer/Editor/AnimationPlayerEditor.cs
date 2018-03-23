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

        public enum AnimationPlayerEditMode
        {
            States,
            Transitions,
            Events,
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

        private MetaDataDrawer metaDataDrawer;

        public AnimationStatePreviewer previewer;
        private List<AnimationClip> multiChoiceAnimationClips;

        private object scriptReloadChecker;

        private bool stateNamesNeedsUpdate = true;
        private bool transitionsNeedsUpdate = true;

        public void MarkDirty()
        {
            stateNamesNeedsUpdate = true;
            metaDataDrawer.usedClipsNeedsUpdate = true;
            EditorUtilities.SetDirty(animationPlayer);
        }

        private void OnEnable()
        {
            animationPlayer = (AnimationPlayer) target;
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

            selectedLayer = new PersistedInt(persistedLayer + instanceId);
            selectedEditMode = new PersistedAnimationPlayerEditMode(persistedEditMode + instanceId);
            selectedState = new PersistedInt(persistedState + instanceId);
            selectedToState = new PersistedInt(persistedToState + instanceId);

            stateNamesNeedsUpdate = true;
        }

        private void HandleInitialization(bool isGUICall)
        {
            if (scriptReloadChecker == null)
            {
                // Unity persists some objects through reload, and fails to persist others. This makes it hard to figure out if
                // something needs to be re-cached. This solves that - we know that Unity can't persist a raw object, so if it's null, a reload is neccessary.
                scriptReloadChecker = new object();

                metaDataDrawer = new MetaDataDrawer(animationPlayer);
                stylesCreated = false;
                transitionsNeedsUpdate = true;
            }

            if (isGUICall && !stylesCreated)
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

            if (selectedState == -1 && animationPlayer.layers[selectedLayer].states.Count > 0)
                selectedState.SetTo(0); //Handle adding a state for the first time.

            GUILayout.Space(10f);

            DrawSelectedLayer();

            GUILayout.Space(10f);
            EditorUtilities.Splitter();

            var numStatesBefore = animationPlayer.layers[selectedLayer].states.Count;
            StateSelectionAndAdditionDrawer.Draw(animationPlayer, selectedLayer, selectedState, selectedEditMode, this, multiChoiceAnimationClips);
            if (numStatesBefore != animationPlayer.layers[selectedLayer].states.Count)
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

            selectedLayer.SetTo(Mathf.Clamp(selectedLayer, 0, numLayers));

            var twoLines = Screen.width < 420f;

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                selectedLayer.SetTo(EditorUtilities.DrawLeftButton(selectedLayer));
                EditorGUILayout.LabelField("Selected layer: " + selectedLayer, GUILayout.Width(selectedLayerWidth));
                selectedLayer.SetTo(EditorUtilities.DrawRightButton(selectedLayer, numLayers));

                if (twoLines)
                {
                    GUILayout.FlexibleSpace();
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
                    EditorUtilities.RecordUndo(animationPlayer, "Add layer to animation player");
                    EditorUtilities.ExpandArrayByOne(ref animationPlayer.layers, AnimationLayer.CreateLayer);
                    selectedLayer.SetTo(animationPlayer.layers.Length - 1);
                    MarkDirty();
                }

                EditorGUI.BeginDisabledGroup(numLayers < 2);
                {
                    if (EditorUtilities.AreYouSureButton("Delete layer", "Are you sure?", "DeleteLayer" + selectedLayer, 1f, GUILayout.Width(100f)))
                    {
                        EditorUtilities.RecordUndo(animationPlayer, "Delete layer from animation player");
                        EditorUtilities.DeleteIndexFromArray(ref animationPlayer.layers, selectedLayer);
                        selectedLayer.SetTo(Mathf.Max(0, selectedLayer - 1));
                        MarkDirty();
                    }
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedLayer()
        {
            var layer = animationPlayer.layers[selectedLayer];

            layer.startWeight = EditorGUILayout.Slider($"Layer {selectedLayer} Weight", layer.startWeight, 0f, 1f);
            layer.mask = EditorUtilities.ObjectField($"Layer {selectedLayer} Mask", layer.mask);

            if(selectedLayer > 0) //Doesn't make any sense for base layer to be additive!
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

            DrawTabHeader(editStatesRect, "Clips", 0);
            DrawTabHeader(editTransitionsRect, "Transitions", 1);
            DrawTabHeader(eventsRect, "Events", 2);
            DrawTabHeader(metaDataRect, "Metadata", 3);

            EditorGUILayout.BeginVertical(editLayerStyle);

            GUILayout.Space(10f);

            switch ((AnimationPlayerEditMode) selectedEditMode)
            {
                case AnimationPlayerEditMode.States:
                    StateDataDrawer.DrawStateData(animationPlayer, selectedLayer, selectedState, this);
                    break;
                case AnimationPlayerEditMode.Transitions:
                    AnimationTransitionDrawer.DrawTransitions(animationPlayer, selectedLayer, selectedState, selectedToState, allStateNames[selectedLayer]);
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

            var isSelected = index == (int) selectedEditMode;
            var style = isSelected ? editLayerButton_Selected : editLayerButton_NotSelected;
            if (GUI.Button(rect, label, style))
                selectedEditMode.SetTo((AnimationPlayerEditMode) index);
        }

        private void DrawEvents()
        {
            if (animationPlayer.layers[selectedLayer].states.Count == 0)
            {
                EditorGUILayout.LabelField("No states on layer, can't make events");
                return;
            }

            var state = animationPlayer.GetState(selectedState, selectedLayer);
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

        
        private bool DrawEvent(AnimationEvent animationEvent, AnimationState containingState)
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
                foreach (var blendVar in blendVars)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(blendVar, GUILayout.Width(100f));
                        EditorGUILayout.LabelField(animationPlayer.GetBlendVar(blendVar).ToString());
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });
            EditorUtilities.Splitter();

            if (!animationPlayer.gameObject.scene.IsValid())
            {
                //is looking at the prefab at runtime, don't attempt to draw the graph!
                return;
            }

            EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetPlayingState(selectedLayer));
            for (int i = animationPlayer.GetStateCount(selectedLayer) - 1; i >= 0; i--)
            {
                EditorGUILayout.LabelField("Current weigth for state " + i + ": " + animationPlayer.GetStateWeight(i, selectedLayer));
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
            return Application.isPlaying || previewer.IsShowingPreview || DragAndDrop.objectReferences.Length > 0;
        }

        private void OnDestroy()
        {
            previewer.Cleanup();
        }
    }
}