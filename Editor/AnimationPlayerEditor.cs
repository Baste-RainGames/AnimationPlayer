using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player
{
[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor : Editor
{
    private Toggle statesToggle;
    private Toggle transitionsToggle;
    private Toggle layerToggle;
    private Toggle editPlayerSettingsToggle;
    private Toggle viewMetaDataToggle;

    private VisualElement entireUIRoot;
    private VisualElement mainUIRoot;
    private StateList stateList;
    private StateEditor stateEditor;
    private TransitionEditor transitionEditor;
    private LayerEditor layerEditor;
    private VisualElement editPlayerSettingsView;
    private VisualElement metaDataView;
    private PreviewSection previewSection;
    private VisualElement layerBar;
    private Label errorLabel;
    private Label runtimeInfoLabel;

    private BulkStateAdder currentBulkStateAdder;
    private VisualElement dragAndDropClipTarget;

    private string sessionStateID_UIState;
    private string sessionStateID_SelectedLayer;
    private string sessionStateID_SelectedState;
    private string sessionStateID_SelectedToState;

    private static Dictionary<Type, AnimationStateEditor> editorsForStateTypes;

    private new AnimationPlayer target => (AnimationPlayer) base.target;

    private UIState uiState;
    private int selectedLayerIndex;
    private int selectedStateIndex;
    private int selectedToStateIndex;

    private SerializedProperty layersProp => serializedObject.FindProperty("layers_v1");
    private SerializedProperty SelectedLayerProp => layersProp.GetArrayElementAtIndex(selectedLayerIndex);
    private SerializedProperty SelectedStateProp => SelectedLayerProp.FindPropertyRelative("states").GetArrayElementAtIndex(selectedStateIndex);
    private SerializedProperty SelectedToStateProp => SelectedLayerProp.FindPropertyRelative("states").GetArrayElementAtIndex(selectedToStateIndex);

    private void OnEnable()
    {
        if (target.EnsureVersionUpgraded())
            EditorUtility.SetDirty(target);

        var sessionStateID = $"AnimationPlayer_{GlobalObjectId.GetGlobalObjectIdSlow(target)}";
        sessionStateID_UIState         = sessionStateID + "_UI State";
        sessionStateID_SelectedLayer   = sessionStateID + "_Selected Layer";
        sessionStateID_SelectedState   = sessionStateID + "_Selected State";
        sessionStateID_SelectedToState = sessionStateID + "_Selected To State";

        // These saved values will be applied in CreateInspectorGUI
        if (TryGetFromSessionState(sessionStateID_UIState, out var savedUIState))
            uiState = (UIState) savedUIState;
        if (TryGetFromSessionState(sessionStateID_SelectedLayer, out var savedSelectedLayer))
            selectedLayerIndex = savedSelectedLayer;
        if (TryGetFromSessionState(sessionStateID_SelectedState, out var savedSelectedState))
            selectedStateIndex = savedSelectedState;
        if (TryGetFromSessionState(sessionStateID_SelectedToState, out var savedSelectedToState))
            selectedToStateIndex = savedSelectedToState;

        if (Application.isPlaying)
        {
            if (updateRuntimeInfoCoroutine != null)
                target.StopCoroutine(updateRuntimeInfoCoroutine);
            updateRuntimeInfoCoroutine = target.StartCoroutine(UpdateRuntimeInfoLabel());
        }
        
        EditorApplication.update += UpdateDragAndDropTarget;
        Undo.undoRedoEvent += UndoRedo;
        
        bool TryGetFromSessionState(string ID, out int value)
        {
            value = SessionState.GetInt(ID, -1);
            return value != -1;
        }
    }

    private void OnDisable()
    {
        if (updateRuntimeInfoCoroutine != null && target)
        {
            target.StopCoroutine(updateRuntimeInfoCoroutine);
            updateRuntimeInfoCoroutine = null;
        }

        previewSection.StopAnyPreview();
        currentBulkStateAdder?.Deactivate();
        EditorApplication.update -= UpdateDragAndDropTarget;
        Undo.undoRedoEvent -= UndoRedo;
    }

    private AnimationPlayerState lastDisplayedState;
    private Coroutine updateRuntimeInfoCoroutine;

    private IEnumerator UpdateRuntimeInfoLabel()
    {
        while (true)
        {
            yield return null;
            var state = target.GetPlayingState();
            if (lastDisplayedState != state)
            {
                lastDisplayedState = state;
                runtimeInfoLabel.SetFlex(true);
                runtimeInfoLabel.text = $"Playing state {state?.Name ?? "null"}";
            }
            
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        const string path = "Packages/com.baste.animationplayer/Editor/UI/AnimationPlayerEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        entireUIRoot = new VisualElement();
        visualTreeAsset.CloneTree(entireUIRoot);

        mainUIRoot = entireUIRoot.Q("MainUIRoot");

        runtimeInfoLabel = mainUIRoot.Q<Label>("RuntimeInfoLabel");
        runtimeInfoLabel.SetFlex(false);

        errorLabel = mainUIRoot.Q<Label>("ErrorLabel");
        errorLabel.SetFlex(false);

        stateList              = new StateList(this, mainUIRoot.Q("StateList"));
        stateEditor            = new StateEditor(mainUIRoot, errorLabel);
        transitionEditor       = new TransitionEditor(this, mainUIRoot);
        layerEditor            = new LayerEditor(this, mainUIRoot.Q("EditLayerView"));
        editPlayerSettingsView = mainUIRoot.Q("EditPlayerSettingsView");
        metaDataView           = mainUIRoot.Q("MetaDataView");
        previewSection         = new PreviewSection(this, mainUIRoot.Q("PreviewRoot"));

        var stateBar = mainUIRoot.Q("StateBar");

        statesToggle             = stateBar.Q<Toggle>("EditStatesToggle");
        transitionsToggle        = stateBar.Q<Toggle>("EditTransitionsToggle");
        layerToggle              = stateBar.Q<Toggle>("EditLayerToggle");
        editPlayerSettingsToggle = stateBar.Q<Toggle>("EditPlayerSettingsToggle");
        viewMetaDataToggle       = stateBar.Q<Toggle>("ViewMetaDataToggle");

        statesToggle            .RegisterValueChangedCallback(c => UIStateClicked(c, UIState.EditStates));
        transitionsToggle       .RegisterValueChangedCallback(c => UIStateClicked(c, UIState.EditTransitions));
        layerToggle             .RegisterValueChangedCallback(c => UIStateClicked(c, UIState.EditLayer));
        editPlayerSettingsToggle.RegisterValueChangedCallback(c => UIStateClicked(c, UIState.EditPlayerSettings));
        viewMetaDataToggle      .RegisterValueChangedCallback(c => UIStateClicked(c, UIState.ViewMetaData));

        layerBar = mainUIRoot.Q("LayerBar");
        var layerDropdown = layerBar.Q<DropdownField>("SelectedLayerDropdown");
        layerDropdown.choices = new (layersProp.arraySize);
        for (int i = 0; i < layersProp.arraySize; i++)
            layerDropdown.choices.Add(layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
        layerDropdown.index = selectedLayerIndex;

        layerDropdown.RegisterValueChangedCallback(SelectedLayerChanged);
        
        dragAndDropClipTarget = mainUIRoot.Q("DragAndDropTarget");
        dragAndDropClipTarget.SetFlex(false);
        SetupDragAndDropTester(dragAndDropClipTarget);

        SetUIState(uiState, true);
        SetLayer(selectedLayerIndex, true);
        SetSelectedState(selectedStateIndex, true);

        return entireUIRoot;

        void UIStateClicked(ChangeEvent<bool> evt, UIState state)
        {
            if (!evt.newValue)
                return;
            SetUIState(state);
        }

        void SelectedLayerChanged(ChangeEvent<string> _)
        {
            SetLayer(layerDropdown.index);
        }
    }

    private void UpdateDragAndDropTarget()
    {
        if (dragAndDropClipTarget != null) // null for one editor frame during recompile
           dragAndDropClipTarget.SetFlex(uiState == UIState.EditStates && DragAndDrop.objectReferences.Any(o => o is AnimationClip || AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(o)) is ModelImporter));
    }
    
    private void UndoRedo(in UndoRedoInfo undo)
    {
        serializedObject.Update();
        
        SetLayer(selectedLayerIndex, true);
        SetSelectedState(selectedStateIndex, true);
    }

    private void SetupDragAndDropTester(VisualElement tester)
    {
        tester.RegisterCallback<DragUpdatedEvent>(DragUpdated);
        tester.RegisterCallback<DragPerformEvent>(DragPerform);

        void DragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        void DragPerform(DragPerformEvent evt)
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

            if (animationClips.Count > 0)
            {
                ActivateDragAndDropUI(animationClips);
            }
        }
    }

    private void ActivateDragAndDropUI(List<AnimationClip> animationClips)
    {
        currentBulkStateAdder = new BulkStateAdder(animationClips, this);
    }

    private void SetLayer(int selectedLayer, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected Layer to " + selectedLayer);

        SessionState.SetInt(sessionStateID_SelectedLayer, selectedLayer);
        selectedLayerIndex = selectedLayer;
        stateList.SetLayer(layersProp.GetArrayElementAtIndex(selectedLayer));
    }

    private void SetSelectedState(int selectedStateIndex, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected State to " + selectedStateIndex);

        SessionState.SetInt(sessionStateID_SelectedState, selectedStateIndex);
        stateList.listView.SetSelectionWithoutNotify(new [] { selectedStateIndex });
        this.selectedStateIndex = selectedStateIndex;

        try
        {
            if (layersProp.arraySize == 0)
            {
                ShowError("No Layers");
                return;
            }

            if (selectedLayerIndex < 0 || selectedLayerIndex >= layersProp.arraySize)
            {
                ShowError($"Selected layer {selectedLayerIndex} out of bounds! There are {layersProp.arraySize} layers");
                return;
            }

            var statesProp = layersProp.GetArrayElementAtIndex(selectedLayerIndex).FindPropertyRelative("states");
            if (statesProp.arraySize == 0)
            {
                ShowError("No States");
                return;
            }

            if (selectedStateIndex < 0 || selectedStateIndex >= statesProp.arraySize)
            {
                ShowError($"Selected state {selectedStateIndex} out of bounds! There are {statesProp.arraySize} states");
                return; }

            var state = statesProp.GetArrayElementAtIndex(selectedStateIndex);

            stateEditor     .ShowAnimationState(state);
            transitionEditor.OnSelectedAnimationStatesChanged();
            previewSection  .OnAnimationStateChanged();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ShowError(e.ToString());
        }
    }

    private void ShowError(string error)
    {
        errorLabel.text = error;
        errorLabel.SetFlex(true);
    }

    private void SetUIState(UIState uiState, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set UI State");
        this.uiState = uiState;
        SessionState.SetInt(sessionStateID_UIState, (int) uiState);

        stateEditor           .SetDisplayed(uiState is UIState.EditStates);
        previewSection        .SetDisplayed(uiState is UIState.EditStates);
        transitionEditor      .SetDisplayed(uiState is UIState.EditTransitions);
        layerEditor           .SetDisplayed(uiState is UIState.EditLayer);
        editPlayerSettingsView.SetFlex(uiState is UIState.EditPlayerSettings);
        metaDataView          .SetFlex(uiState is UIState.ViewMetaData);

        stateList.SetDisplayed(uiState is UIState.EditStates);
        layerBar.SetFlex (uiState is UIState.EditStates or UIState.EditTransitions or UIState.EditLayer);

        statesToggle            .SetValueWithoutNotify(uiState is UIState.EditStates);
        transitionsToggle       .SetValueWithoutNotify(uiState is UIState.EditTransitions);
        layerToggle             .SetValueWithoutNotify(uiState is UIState.EditLayer);
        editPlayerSettingsToggle.SetValueWithoutNotify(uiState is UIState.EditPlayerSettings);
        viewMetaDataToggle      .SetValueWithoutNotify(uiState is UIState.ViewMetaData);
    }

    private enum UIState
    {
        EditStates,
        EditTransitions,
        EditLayer,
        EditPlayerSettings,
        ViewMetaData,
    }

    [Serializable]
    private class StateList
    {
        public ListView listView;
        private readonly VisualElement root;
        private readonly AnimationPlayerEditor parentEditor;

        public StateList(AnimationPlayerEditor parentEditor, VisualElement stateListRoot)
        {
            this.parentEditor = parentEditor;
            root = stateListRoot;
            listView = root.Q<ListView>();

            listView.RegisterCallback<KeyDownEvent>(KeyPressedWhileStateListFocused);
            void KeyPressedWhileStateListFocused(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.F2 && parentEditor.uiState == UIState.EditStates)
                    parentEditor.stateEditor.RenamePressedOnState();
            }

            listView.makeItem = () =>
            {
                var item = new VisualElement().WithClass("state-list--state-label-line")
                                              .WithChild(new Label().WithName("name").WithClass("state-list--state-label"))
                                              .WithChild(new Label().WithName("type").WithClass("state-list--state-label"));
                return item;
            };
            listView.bindItem = (ve, index) =>
            {
                var stateProp = parentEditor.layersProp
                                            .GetArrayElementAtIndex(parentEditor.selectedLayerIndex)
                                            .FindPropertyRelative(nameof(AnimationLayer.states))
                                            .GetArrayElementAtIndex(index);

                var nameLabel = ve.Q<Label>("name");
                var typeLabel = ve.Q<Label>("type");
                nameLabel.BindProperty(stateProp.FindPropertyRelative("name"));
                typeLabel.text = $"({stateProp.managedReferenceValue.GetType().Name})";
            };
            listView.unbindItem = (ve, _) =>
            {
                ve.Unbind();
            };

            listView.Q<Button>(BaseListView.footerAddButtonName)   .clickable = new Clickable(ShowAddStateMenu);
            listView.Q<Button>(BaseListView.footerRemoveButtonName).clickable = new Clickable(DeleteSelectedState);

            listView.selectedIndicesChanged += indices =>
            {
                // The selected indices changed that we get from an Undo operation gives the index from before the undo operation, not the index after the undo.
                // This would cause the wrong index (potentially out of bounds) to be selected.
                // So this message is ignored during undo, and the UndoRedo callback instead sets the correct index in the editor (and on the listview, without notify).
                // @TODO: Report this as a bug to Unity, it seems wrong!
                if (Undo.isProcessing)
                    return;

                // Code is silly because listView returns an IEnumerable instead of eg. an IReadOnlyList or something where we can actually check the count lol
                var indexList = indices.ToList();
                if (indexList.Count > 0)
                {
                    parentEditor.SetSelectedState(indexList[0]);
                }
            };
        }

        private void ShowAddStateMenu()
        {
            var gm = new GenericMenu();
            foreach (var stateType in editorsForStateTypes.Keys)
                gm.AddItem(new GUIContent(stateType.Name), false, () => AddStateOfType(stateType));
            gm.ShowAsContext();

            void AddStateOfType(Type type)
            {
                var editor = editorsForStateTypes[type];
                var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));

                parentEditor.serializedObject.Update();

                var newState = editor.CreateNewState(statesProp.arraySize);
                statesProp.arraySize++;
                statesProp.GetArrayElementAtIndex(statesProp.arraySize - 1).managedReferenceValue = newState;

                parentEditor.serializedObject.ApplyModifiedProperties();
                parentEditor.SetSelectedState(statesProp.arraySize - 1);
                listView.SetSelectionWithoutNotify(new[] { statesProp.arraySize - 1 });
            }
        }

        private void DeleteSelectedState()
        {
            parentEditor.serializedObject.Update();
            var statesProp = parentEditor.layersProp.GetArrayElementAtIndex(parentEditor.selectedLayerIndex).FindPropertyRelative(nameof(AnimationLayer.states));
            statesProp.DeleteArrayElementAtIndex(parentEditor.selectedStateIndex);
            parentEditor.serializedObject.ApplyModifiedProperties();

            var index = listView.selectedIndex;
            if (index >= statesProp.arraySize)
            {
                index = statesProp.arraySize - 1;
                listView.selectedIndex = index;
            }

            parentEditor.SetSelectedState(index);
        }

        public void SetLayer(SerializedProperty layer)
        {
            listView.Unbind();
            listView.BindProperty(layer.FindPropertyRelative(nameof(AnimationLayer.states)));
        }

        public void SetDisplayed(bool displayed)
        {
            root.SetFlex(displayed);
        }
    }
    
    [Serializable]
    private class StateEditor
    {
        private readonly Label errorLabel;
        private readonly VisualElement stateEditorRoot;
        private readonly VisualElement selectedStateRoot;

        private AnimationStateEditor shownEditor;

        public StateEditor(VisualElement entireUIRoot, Label errorLabel)
        {
            stateEditorRoot = entireUIRoot.Q("EditStateView");
            selectedStateRoot = stateEditorRoot.Q("SelectedState");
            this.errorLabel = errorLabel;
        }

        public void SetDisplayed(bool displayed)
        {
            stateEditorRoot.SetFlex(displayed);
        }

        public void ShowAnimationState(SerializedProperty stateProperty)
        {
            if (shownEditor != null)
            {
                shownEditor.ClearBindings(stateProperty);
                shownEditor.RootVisualElement.parent.Remove(shownEditor.RootVisualElement);
                shownEditor = null;
            }

            if (stateProperty.managedReferenceValue == null)
            {
                ShowError("State is null");
                return;
            }

            var stateType = stateProperty.managedReferenceValue.GetType();
            if (editorsForStateTypes.TryGetValue(stateType, out var editor))
            {
                editor.GenerateUI();
                editor.BindUI(stateProperty);

                shownEditor = editor;

                shownEditor.RootVisualElement.Add(new Button(() =>
                {
                    stateProperty.serializedObject.FindProperty("editTimePreviewState").managedReferenceValue = stateProperty.managedReferenceValue;
                    stateProperty.serializedObject.ApplyModifiedProperties();
                }) { text = "Set As Player Default"});

                selectedStateRoot.Add(shownEditor.RootVisualElement);
                errorLabel.SetFlex(false);
            }
            else
            {
                ShowError("No known editor for state type " + stateType);
            }
        }

        private void ShowError(string error)
        {
            selectedStateRoot.SetFlex(false);
            errorLabel.text = error;
            errorLabel.SetFlex(true);
        }

        public void RenamePressedOnState()
        {
            stateEditorRoot.Q<TextField>("name").Focus();
        }
    }

    [Serializable]
    private class TransitionEditor
    {
        private readonly AnimationPlayerEditor parentEditor;
        private readonly VisualElement transitionEditorRoot;
        private readonly ListView namedTransitionList;
        private readonly Label defaultTransitionLabel;
        private readonly Label additionalTransitionsLabel;
        private readonly DropdownField fromStateDropdown;
        private readonly DropdownField toStateDropdown;
        private readonly DropdownField useDefaultStateDropdown;
        private readonly VisualElement defaultTransitionEditor;

        private readonly string defaultTransitionLabelOriginalText;
        private readonly string additionalTransitionsLabelOriginalText;

        private bool hasDefaultTransition;
        private SerializedProperty defaultTransitionPropForSelectedStates;
        private List<SerializedProperty> transitionPropsForSelectedStates = new();

        public TransitionEditor(AnimationPlayerEditor parentEditor, VisualElement entireUIRoot)
        {
            this.parentEditor = parentEditor;
            transitionEditorRoot = entireUIRoot.Q("EditTransitionsView");

            namedTransitionList        = transitionEditorRoot.Q<ListView>("Transitions");
            fromStateDropdown          = transitionEditorRoot.Q<DropdownField>("TransitionsFromDropdown");
            toStateDropdown            = transitionEditorRoot.Q<DropdownField>("TransitionsToDropdown");
            defaultTransitionLabel     = transitionEditorRoot.Q<Label>("DefaultTransitionLabel");
            additionalTransitionsLabel = transitionEditorRoot.Q<Label>("AdditionalTransitionsLabel");
            useDefaultStateDropdown    = transitionEditorRoot.Q<DropdownField>("DefaultTransitionUseDefaultDropdown");

            defaultTransitionEditor = transitionEditorRoot.Q<TemplateContainer>("DefaultTransitionBetweenStates");
            defaultTransitionEditor.Q<TextField>().SetEnabled(false);

            fromStateDropdown.RegisterValueChangedCallback(FromStateSelected);
            toStateDropdown  .RegisterValueChangedCallback(ToStateSelected);

            defaultTransitionLabelOriginalText     = defaultTransitionLabel    .text;
            additionalTransitionsLabelOriginalText = additionalTransitionsLabel.text;

            namedTransitionList.itemsSource = transitionPropsForSelectedStates;

            useDefaultStateDropdown.RegisterValueChangedCallback(UseDefaultStateChanged);

            const string path = "Packages/com.baste.animationplayer/Editor/UI/TransitionListEntry.uxml";
            namedTransitionList.makeItem = () => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path).CloneRoot();

            namedTransitionList.bindItem = BindTransitionListItem;
            namedTransitionList.unbindItem = (ve, _) =>
            {
                ve.Unbind();
            };

            namedTransitionList.itemsAdded += indices =>
            {
                foreach (var index in indices)
                {
                    var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.namedTransitions));
                    var newTransition = transitionsProp.AppendToArray();
                    
                    var nameProp      = newTransition.FindPropertyRelative(nameof(NamedStateTransition.name));
                    var fromStateProp = newTransition.FindPropertyRelative(nameof(NamedStateTransition.fromState));
                    var toStateProp   = newTransition.FindPropertyRelative(nameof(NamedStateTransition.toState));

                    fromStateProp.managedReferenceValue = parentEditor.SelectedStateProp.managedReferenceValue;
                    toStateProp.managedReferenceValue = parentEditor.SelectedToStateProp.managedReferenceValue;

                    nameProp.stringValue = $"Transition {parentEditor.SelectedStateProp.FindPropertyRelative("name").stringValue}->" +
                                           $"{parentEditor.SelectedToStateProp.FindPropertyRelative("name").stringValue}";

                    var newData = newTransition.FindPropertyRelative(nameof(NamedStateTransition.transitionData));

                    newData.FindPropertyRelative(nameof(TransitionData.type))                   .intValue             = (int) TransitionType.Linear;
                    newData.FindPropertyRelative(nameof(TransitionData.duration))               .floatValue           = 1f;
                    newData.FindPropertyRelative(nameof(TransitionData.timeOffsetIntoNewState)) .doubleValue          = 0f;
                    newData.FindPropertyRelative(nameof(TransitionData.curve))                  .animationCurveValue  = null;
                    newData.FindPropertyRelative(nameof(TransitionData.clip))                   .objectReferenceValue = null;

                    parentEditor.serializedObject.ApplyModifiedProperties();
                    transitionPropsForSelectedStates[index] = newTransition;
                }
            };
            
            namedTransitionList.Q<Button>(BaseListView.footerRemoveButtonName).clickable = new Clickable(() =>
            {
                var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.namedTransitions));

                var index = namedTransitionList.selectedIndex;
                if (index == -1)
                    index = transitionPropsForSelectedStates.Count - 1;
                if (index == -1)
                    return;
                
                var property = transitionPropsForSelectedStates[index];
                var propertyPath = property.propertyPath;

                var startOfIndex = propertyPath.LastIndexOf("[", StringComparison.Ordinal) + 1;
                var propertyIndex = int.Parse(propertyPath[startOfIndex..^1]);

                transitionsProp.DeleteArrayElementAtIndex(propertyIndex);
                parentEditor.serializedObject.ApplyModifiedProperties();
                
                var selectedStateObject   = (AnimationPlayerState) parentEditor.SelectedStateProp  .managedReferenceValue;
                var selectedToStateObject = (AnimationPlayerState) parentEditor.SelectedToStateProp.managedReferenceValue;

                int j = 0;
                for (int i = 0; i < transitionsProp.arraySize; i++)
                {
                    var transition = transitionsProp.GetArrayElementAtIndex(i);
                    var fromState = transition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue;
                    var toState   = transition.FindPropertyRelative(nameof(StateTransition.toState))  .managedReferenceValue;

                    if (fromState == selectedStateObject && toState == selectedToStateObject)
                    {
                        transitionPropsForSelectedStates[j] = transition;
                        j++;
                    }
                }
               
                namedTransitionList.viewController.RemoveItem(transitionPropsForSelectedStates.Count - 1);
            });
        }

        public void OnSelectedAnimationStatesChanged()
        {
            var selectedStateObject   = (AnimationPlayerState) parentEditor.SelectedStateProp  .managedReferenceValue;
            var selectedToStateObject = (AnimationPlayerState) parentEditor.SelectedToStateProp.managedReferenceValue;

            UpdateNamedTransitions();
            UpdateLabels();
            UpdateDefaultTransition();

            void UpdateLabels()
            {
                if (selectedStateObject == selectedToStateObject)
                {
                    defaultTransitionLabel    .text = string.Format(defaultTransitionLabelOriginalText,     selectedStateObject.Name, "itself");
                    additionalTransitionsLabel.text = string.Format(additionalTransitionsLabelOriginalText, selectedStateObject.Name, "itself");
                }
                else
                {
                    defaultTransitionLabel    .text = string.Format(defaultTransitionLabelOriginalText,     selectedStateObject.Name, selectedToStateObject.Name);
                    additionalTransitionsLabel.text = string.Format(additionalTransitionsLabelOriginalText, selectedStateObject.Name, selectedToStateObject.Name);
                }
            }

            void UpdateNamedTransitions()
            {
                transitionPropsForSelectedStates.Clear();
                var namedTransitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.namedTransitions));
                for (int i = 0; i < namedTransitionsProp.arraySize; i++)
                {
                    var transition = namedTransitionsProp.GetArrayElementAtIndex(i);
                    var fromState = transition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue;
                    var toState   = transition.FindPropertyRelative(nameof(StateTransition.toState))  .managedReferenceValue;

                    if (fromState == selectedStateObject && toState == selectedToStateObject)
                    {
                        transitionPropsForSelectedStates.Add(transition);
                    }
                }
                namedTransitionList.RefreshItems();
            }

            void UpdateDefaultTransition()
            {
                hasDefaultTransition = false;
                defaultTransitionPropForSelectedStates = null;

                var defaultTransitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.defaultTransitions));
                for (int i = 0; i < defaultTransitionsProp.arraySize; i++)
                {
                    var transition = defaultTransitionsProp.GetArrayElementAtIndex(i);
                    var fromState = transition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue;
                    var toState   = transition.FindPropertyRelative(nameof(StateTransition.toState))  .managedReferenceValue;

                    if (fromState == selectedStateObject && toState == selectedToStateObject)
                    {
                        if (hasDefaultTransition)
                        {
                            Debug.LogWarning($"There are two default transitions from {selectedStateObject.Name} to {selectedToStateObject.Name}! " +
                                             $"There should only be one. This issue is probably be the result of a merge! One will be deleted.");
                            defaultTransitionsProp.DeleteArrayElementAtIndex(i);
                            i--;
                        }
                        else
                        {
                            hasDefaultTransition = true;
                            defaultTransitionPropForSelectedStates = transition;
                        }
                    }
                }

                if (hasDefaultTransition)
                {
                    defaultTransitionEditor.Unbind();
                    BindTransition(defaultTransitionEditor, defaultTransitionPropForSelectedStates);
                    defaultTransitionEditor.SetEnabled(true);
                    useDefaultStateDropdown.SetValueWithoutNotify(useDefaultStateDropdown.choices[1]);
                }
                else
                {
                    defaultTransitionEditor.Unbind();
                    var playerDefault = parentEditor.serializedObject.FindProperty(nameof(AnimationPlayer.defaultTransition));
                    BindTransitionData(defaultTransitionEditor, playerDefault);
                    defaultTransitionEditor.SetEnabled(false);
                    useDefaultStateDropdown.SetValueWithoutNotify(useDefaultStateDropdown.choices[0]);
                }
            }
        }

        public void SetDisplayed(bool displayed)
        {
            transitionEditorRoot.SetFlex(displayed);
            if (displayed)
            {
                var stateNames = GetStateNames();

                fromStateDropdown.choices = stateNames;
                toStateDropdown  .choices = stateNames;

                if (stateNames.Count > 0)
                {
                    fromStateDropdown.SetValueWithoutNotify(stateNames[parentEditor.selectedStateIndex]);
                    toStateDropdown  .SetValueWithoutNotify(stateNames[parentEditor.selectedToStateIndex]);
                    
                    OnSelectedAnimationStatesChanged();
                }
            }
        }

        private List<string> GetStateNames()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            var stateNames = new List<string>();
            for (int i = 0; i < statesProp.arraySize; i++)
            {
                var state = statesProp.GetArrayElementAtIndex(i);
                stateNames.Add(((AnimationPlayerState) state.managedReferenceValue).Name);
            }

            return stateNames;
        }

        private void FromStateSelected(ChangeEvent<string> evt)
        {
            parentEditor.SetSelectedState(GetStateNames().IndexOf(evt.newValue));
            OnSelectedAnimationStatesChanged();
        }
        
        private void ToStateSelected(ChangeEvent<string> evt)
        {
            parentEditor.selectedToStateIndex = GetStateNames().IndexOf(evt.newValue);
            SessionState.SetInt(parentEditor.sessionStateID_SelectedToState, parentEditor.selectedToStateIndex);
            OnSelectedAnimationStatesChanged();
        }

        private void UseDefaultStateChanged(ChangeEvent<string> evt)
        {
            var useAnimationPlayerDefault = evt.newValue == useDefaultStateDropdown.choices[0];
            if (useAnimationPlayerDefault)
            {
                RemoveOldCustomDefaultTransition();
                BindDefaultTransitionEditorToPlayerDefault();
                defaultTransitionEditor.SetEnabled(false);
            }
            else
            {
                AddNewCustomDefaultTransition();
                BindDefaultTransitionEditorToNewDefaultTransition();
                defaultTransitionEditor.SetEnabled(true);
            }

            void RemoveOldCustomDefaultTransition()
            {
                var defaultTransitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.defaultTransitions));
                var selectedStateObject   = (AnimationPlayerState) parentEditor.SelectedStateProp  .managedReferenceValue;
                var selectedToStateObject = (AnimationPlayerState) parentEditor.SelectedToStateProp.managedReferenceValue;

                for (int i = defaultTransitionsProp.arraySize - 1; i >= 0; i--)
                {
                    var transition = defaultTransitionsProp.GetArrayElementAtIndex(i);
                    var fromState = transition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue;
                    var toState   = transition.FindPropertyRelative(nameof(StateTransition.toState))  .managedReferenceValue;

                    if (fromState == selectedStateObject && toState == selectedToStateObject)
                    {
                        defaultTransitionsProp.DeleteArrayElementAtIndex(i);
                    }
                }

                hasDefaultTransition = false;
                defaultTransitionPropForSelectedStates = null;
                parentEditor.serializedObject.ApplyModifiedProperties();
            }

            void BindDefaultTransitionEditorToPlayerDefault()
            {
                var playerDefault = parentEditor.serializedObject.FindProperty(nameof(AnimationPlayer.defaultTransition));
                defaultTransitionEditor.Q<TextField>("TransitionName").SetFlex(false);
                defaultTransitionEditor.Unbind();
                BindTransitionData(defaultTransitionEditor, playerDefault);
                defaultTransitionEditor.SetEnabled(false);
            }

            void AddNewCustomDefaultTransition()
            {
                var defaultTransitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.defaultTransitions));
                var newTransition = defaultTransitionsProp.AppendToArray();

                var fromStateProp = newTransition.FindPropertyRelative(nameof(StateTransition.fromState));
                var toStateProp   = newTransition.FindPropertyRelative(nameof(StateTransition.toState));

                fromStateProp.managedReferenceValue = parentEditor.SelectedStateProp.managedReferenceValue;
                toStateProp.managedReferenceValue   = parentEditor.SelectedToStateProp.managedReferenceValue;

                var newData = newTransition.FindPropertyRelative(nameof(StateTransition.transitionData));

                newData.FindPropertyRelative(nameof(TransitionData.type))                   .intValue             = (int) TransitionType.Linear;
                newData.FindPropertyRelative(nameof(TransitionData.duration))               .floatValue           = 1f;
                newData.FindPropertyRelative(nameof(TransitionData.timeOffsetIntoNewState)) .doubleValue          = 0f;
                newData.FindPropertyRelative(nameof(TransitionData.curve))                  .animationCurveValue  = null;
                newData.FindPropertyRelative(nameof(TransitionData.clip))                   .objectReferenceValue = null;

                parentEditor.serializedObject.ApplyModifiedProperties();

                hasDefaultTransition = true;
                defaultTransitionPropForSelectedStates = newTransition;
            }

            void BindDefaultTransitionEditorToNewDefaultTransition()
            {
                defaultTransitionEditor.Unbind();
                BindTransition(defaultTransitionEditor, defaultTransitionPropForSelectedStates);
                defaultTransitionEditor.SetEnabled(true);
            }
        }
        
        private void BindTransitionListItem(VisualElement ve, int index)
        {
            var transitionProp = transitionPropsForSelectedStates[index];
            BindTransition(ve, transitionProp);
        }

        private void BindTransition(VisualElement ve, SerializedProperty transitionProp)
        {
            // Transition can either be NamedStateTransition or just StateTransition
            var nameProp  = transitionProp.FindPropertyRelative(nameof(NamedStateTransition.name));
            var nameField = ve.Q<TextField>("TransitionName");
            if (nameProp != null)
            {
                nameField.style.display = DisplayStyle.Flex;
                nameField.BindProperty(nameProp);
            }
            else
                nameField.style.display = DisplayStyle.None;

            var transitionDataProp = transitionProp.FindPropertyRelative(nameof(StateTransition.transitionData));
            BindTransitionData(ve, transitionDataProp);
        }

        private void BindTransitionData(VisualElement ve, SerializedProperty transitionDataProp)
        {
            var transitionTypeProp = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.type));
            var curveProp          = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.curve));
            var durationProp       = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.duration));
            var clipProp           = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.clip));

            var transitionTypeField = ve.Q<EnumField>("TransitionType");

            var durationField = ve.Q<FloatField> ("Duration");
            var curveField    = ve.Q<CurveField> ("Curve");
            var clipField     = ve.Q<ObjectField>("Clip");

            transitionTypeField.BindProperty(transitionTypeProp);
            durationField      .BindProperty(durationProp);
            curveField         .BindProperty(curveProp);
            clipField          .BindProperty(clipProp);

            clipField.objectType = typeof(AnimationClip);

            DisplayFieldsForType((TransitionType) transitionTypeProp.intValue);
            transitionTypeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null) // Seems to get null when we change UI?
                {
                    var newValue = (TransitionType) evt.newValue;
                    DisplayFieldsForType(newValue);
                }
            });

            void DisplayFieldsForType(TransitionType transitionType)
            {
                clipField .SetFlex(transitionType == TransitionType.Clip);
                curveField.SetFlex(transitionType == TransitionType.Curve);
            }
        }
    }

    [Serializable]
    private class LayerEditor
    {
        private readonly AnimationPlayerEditor parentEditor;
        private readonly VisualElement layerSectionRoot;

        public LayerEditor(AnimationPlayerEditor parentEditor, VisualElement layerSectionRoot)
        {
            this.parentEditor = parentEditor;
            this.layerSectionRoot = layerSectionRoot;
        }

        public void SetDisplayed(bool displayed)
        {
            layerSectionRoot.SetFlex(displayed);
            if (displayed)
            {
                layerSectionRoot.Q<TextField>  ("LayerNameInput")   .bindingPath = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.name)).propertyPath;
                layerSectionRoot.Q<Slider>     ("LayerWeightSlider").bindingPath = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.startWeight)).propertyPath;
                layerSectionRoot.Q<ObjectField>("AvatarMaskField")  .bindingPath = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.mask)).propertyPath;
                layerSectionRoot.Q<EnumField>  ("LayerTypeField")   .bindingPath = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.type)).propertyPath;
                
                layerSectionRoot.Bind(parentEditor.serializedObject);
            }
            else
            {
                layerSectionRoot.Unbind();
            }
        }
    }

    [Serializable]
    private class PreviewSection
    {
        private readonly AnimationPlayerEditor parentEditor;
        private readonly AnimationPlayer animationPlayer;
        private readonly VisualElement previewRoot;
        private readonly Button playReplayButton;
        private readonly Button pauseResumeButton;
        private readonly Button stopButton;
        private readonly Slider previewSlider;
        private readonly Slider blendVar1Slider;
        private readonly Slider blendVar2Slider;
        private readonly AnimationPlayerPreviewer previewer;

        public PreviewSection(AnimationPlayerEditor parentEditor, VisualElement previewRoot)
        { 
            this.previewRoot  = previewRoot;
            this.parentEditor = parentEditor;
            animationPlayer   = parentEditor.target;
            playReplayButton  = previewRoot.Q<Button>("PlayReplayButton");
            pauseResumeButton = previewRoot.Q<Button>("PauseResumeButton");
            stopButton        = previewRoot.Q<Button>("StopButton");
            previewSlider     = previewRoot.Q<Slider>("ProgressSlider");
            blendVar1Slider   = previewRoot.Q<Slider>("BlendVar1Slider");
            blendVar2Slider   = previewRoot.Q<Slider>("BlendVar2Slider");
            
            animationPlayer.previewer ??= new AnimationPlayerPreviewer(parentEditor.target);
            previewer = animationPlayer.previewer;

            playReplayButton.clicked += () =>
            {
                if (previewer.IsPreviewing)
                    previewer.StopPreview();
                previewer.StartPreview(parentEditor.selectedLayerIndex, parentEditor.selectedStateIndex, true, previewSlider, blendVar1Slider.value, 
                                       blendVar2Slider.value);
                SetButtonVisualStates();
            };
            
            stopButton.clicked += () =>
            {
                previewer.StopPreview();
                SetButtonVisualStates();
            };

            pauseResumeButton.clicked += () =>
            {
                previewer.AutomaticPlayback = !previewer.AutomaticPlayback;
                SetButtonVisualStates();
            };

            previewSlider.RegisterValueChangedCallback(change =>
            {
                if (previewer.IsPreviewing)
                    previewer.AutomaticPlayback = false;
                else
                {
                    previewer.StartPreview(parentEditor.selectedLayerIndex, parentEditor.selectedStateIndex, true, previewSlider, blendVar1Slider.value,
                                           blendVar2Slider.value);
                    previewer.AutomaticPlayback = false;
                    SetButtonVisualStates();
                }

                previewer.SetTime(change.newValue / animationPlayer.GetPlayingState().Duration);

            });

            blendVar1Slider.RegisterValueChangedCallback(change =>
            {
                if (!previewer.IsPreviewing)
                    return;
                var currentState = animationPlayer.GetState(parentEditor.selectedStateIndex, parentEditor.selectedLayerIndex);
                if (currentState is BlendTree1D blendTree1D)
                {
                    animationPlayer.SetBlendVar(blendTree1D.blendVariable, change.newValue);
                    if (!previewer.AutomaticPlayback)
                        previewer.Resample();
                }
                else if (currentState is BlendTree2D blendTree2D)
                {
                    animationPlayer.SetBlendVar(blendTree2D.blendVariable, change.newValue);
                    if (!previewer.AutomaticPlayback)
                        previewer.Resample();
                }
                else
                    Debug.LogError($"Visible Blend Var 1 Slider for a {(currentState == null ? "null state" : currentState.GetType().ToString())}");
            });
            
            blendVar2Slider.RegisterValueChangedCallback(change =>
            {
                if (!previewer.IsPreviewing)
                    return;
                var currentState = animationPlayer.GetState(parentEditor.selectedStateIndex, parentEditor.selectedLayerIndex);
                if (currentState is BlendTree2D blendTree2D)
                {
                    animationPlayer.SetBlendVar(blendTree2D.blendVariable2, change.newValue);
                    if (!previewer.AutomaticPlayback)
                        previewer.Resample(); 
                }
                else
                    Debug.LogError($"Visible Blend Var 2 Slider for a {(currentState == null ? "null state" : currentState.GetType().ToString())}");
            });
        }

        public void SetDisplayed(bool displayed)
        {
            previewRoot.SetFlex(displayed);
            if (!displayed)
                StopAnyPreview();
            else
            {
                SetButtonVisualStates();
                SetSliderVisibility();
            }
        }

        public void StopAnyPreview()
        {
            previewer?.StopPreview();
        }

        public void OnAnimationStateChanged()
        {
            StopAnyPreview();
            SetSliderVisibility();
            SetButtonVisualStates();
        }
        
        private void SetButtonVisualStates()
        {
            var hasAnyStates = animationPlayer.layers[parentEditor.selectedLayerIndex].states.Count > 0;

            playReplayButton .text = previewer.IsPreviewing ? "Replay" : "Play";
            pauseResumeButton.text = previewer.IsPreviewing && !previewer.AutomaticPlayback ? "Resume" : "Pause";

            playReplayButton .SetEnabled(hasAnyStates);
            pauseResumeButton.SetEnabled(hasAnyStates && previewer.IsPreviewing);
            stopButton       .SetEnabled(hasAnyStates && previewer.IsPreviewing);
        }
        
        private void SetSliderVisibility()
        {
            if (animationPlayer.layers[parentEditor.selectedLayerIndex].states.Count == 0)
            {
                blendVar1Slider.SetFlex(false);
                blendVar2Slider.SetFlex(false);
                return;
            }
            var currentState = animationPlayer.GetState(parentEditor.selectedStateIndex, parentEditor.selectedLayerIndex);
            if (currentState is BlendTree1D blendTree1D)
            {
                blendVar1Slider.SetFlex(true);
                blendVar2Slider.SetFlex(false);

                blendVar1Slider.label = blendTree1D.blendVariable;
                var hasEntries = blendTree1D.entries.Count > 0;
                blendVar1Slider.lowValue  = hasEntries ? blendTree1D.entries.Min(e => e.threshold) : 0;
                blendVar1Slider.highValue = hasEntries ? blendTree1D.entries.Max(e => e.threshold) : 1;
            }
            else if (currentState is BlendTree2D blendTree2D)
            {
                blendVar1Slider.SetFlex(true);
                blendVar2Slider.SetFlex(true);
                
                var hasEntries = blendTree2D.entries.Count > 0;

                blendVar1Slider.label     = blendTree2D.blendVariable;
                blendVar1Slider.lowValue  = hasEntries ? blendTree2D.entries.Min(e => e.threshold1) : 0;
                blendVar1Slider.highValue = hasEntries ? blendTree2D.entries.Max(e => e.threshold1) : 1;

                blendVar2Slider.label     = blendTree2D.blendVariable2;
                blendVar2Slider.lowValue  = hasEntries ? blendTree2D.entries.Min(e => e.threshold2) : 0;
                blendVar2Slider.highValue = hasEntries ? blendTree2D.entries.Max(e => e.threshold2) : 1;
            }
            else
            {
                blendVar1Slider.SetFlex(false);
                blendVar2Slider.SetFlex(false);
            }

            previewSlider.highValue = currentState.Duration;
        }
    }

    public static readonly Comparison<Type> AddTypesSorter = SortTypesForAddStatDropdown;
    private static readonly Type[] builtinTypeOrder =
    {
        typeof(SingleClipEditor),
        typeof(SequenceEditor),
        typeof(RandomClipEditor),
        typeof(BlendTree1DEditor),
        typeof(BlendTree2DEditor),
    };

    private static int SortTypesForAddStatDropdown(Type x, Type y)
    {
        var xBuiltinOrder = Array.IndexOf(builtinTypeOrder, x);
        var yBuiltinOrder = Array.IndexOf(builtinTypeOrder, y);

        if (xBuiltinOrder != -1 && yBuiltinOrder != -1)
            return xBuiltinOrder.CompareTo(yBuiltinOrder);
        if (xBuiltinOrder != -1)
            return -1;
        if (yBuiltinOrder != -1)
            return 1;
        return string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
    }

    static AnimationPlayerEditor()
    {
        editorsForStateTypes = new();
        var editorTypes = TypeCache.GetTypesDerivedFrom(typeof(AnimationStateEditor)).ToList();

        editorTypes.RemoveAll(t => t.IsAbstract);
        editorTypes.Sort(AddTypesSorter);

        foreach (var editorType in editorTypes)
        {
            try
            {
                var editor = (AnimationStateEditor) Activator.CreateInstance(editorType);
                var editedType = editor.GetEditedType();
                if (editorsForStateTypes.ContainsKey(editedType))
                {
                    Debug.LogError($"There are two (or more) editors for the type {editedType.Name} - {editorType.Name} and " +
                                   $"{editorsForStateTypes[editedType].GetType()}. The last one found by TypeCache will be used.");
                }
                editorsForStateTypes[editedType] = editor;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception when trying to create a {editorType.FullName}. It must have a parameterless, public constructor!");
                Debug.LogException(e);
            }
        }
    }
    
    public class BulkStateAdder
    {
        private readonly AnimationPlayerEditor parentEditor;
        private readonly List<AnimationClip> clips;
        private readonly VisualElement uiRoot;

        private readonly Button singleButton;
        private readonly Button sequenceButton;
        private readonly Button randomButton;
        private readonly Button blendTree1DButton;
        private readonly Button blendTree2DButton;
        private readonly Button cancelButton;

        public BulkStateAdder(List<AnimationClip> clips, AnimationPlayerEditor parentEditor)
        {
            this.parentEditor = parentEditor;
            this.clips = clips;
            parentEditor.mainUIRoot.SetFlex(false);

            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.baste.animationplayer/Editor/UI/BulkStateAdder.uxml");
            uiRoot = new ();
            visualTreeAsset.CloneTree(uiRoot);
            parentEditor.entireUIRoot.Add(uiRoot);

            var list = uiRoot.Q<ListView>("ClipList");
            list.itemsSource = clips;

            singleButton      = uiRoot.Q<Button>("SingleClip");
            sequenceButton    = uiRoot.Q<Button>("Sequence");
            randomButton      = uiRoot.Q<Button>("RandomSelection");
            blendTree1DButton = uiRoot.Q<Button>("BlendTree1D");
            blendTree2DButton = uiRoot.Q<Button>("BlendTree2D");
            cancelButton      = uiRoot.Q<Button>("Cancel");

            singleButton     .text = singleButton     .text.Replace("X", clips.Count.ToString(), StringComparison.Ordinal);
            sequenceButton   .text = sequenceButton   .text.Replace("X", clips.Count.ToString(), StringComparison.Ordinal);
            randomButton     .text = randomButton     .text.Replace("X", clips.Count.ToString(), StringComparison.Ordinal);
            blendTree1DButton.text = blendTree1DButton.text.Replace("X", clips.Count.ToString(), StringComparison.Ordinal);
            blendTree2DButton.text = blendTree2DButton.text.Replace("X", clips.Count.ToString(), StringComparison.Ordinal);

            if (clips.Count == 1)
            {
                singleButton     .text = singleButton     .text.Replace("states", "state");
                sequenceButton   .text = sequenceButton   .text.Replace("clips", "clip");
                randomButton     .text = randomButton     .text.Replace("clips", "clip");
                blendTree1DButton.text = blendTree1DButton.text.Replace("clips", "clip");
                blendTree2DButton.text = blendTree2DButton.text.Replace("clips", "clip");
            }

            singleButton     .clickable = new Clickable(AddNewSingleClips);
            sequenceButton   .clickable = new Clickable(AddNewSequence);
            randomButton     .clickable = new Clickable(AddNewRandom);
            blendTree1DButton.clickable = new Clickable(AddNewBlendTree1D);
            blendTree2DButton.clickable = new Clickable(AddNewBlendTree2D);
            cancelButton     .clickable = new Clickable(Deactivate);
        }

        private void AddNewSingleClips()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            parentEditor.serializedObject.Update();

            foreach (var clip in clips)
                statesProp.AppendToArray().managedReferenceValue = SingleClip.Create(clip.name, clip);

            parentEditor.serializedObject.ApplyModifiedProperties();
            Deactivate();
        }

        private void AddNewSequence()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            parentEditor.serializedObject.Update();

            var sequence = Sequence.Create(clips[0].name);
            sequence.clips = clips;

            statesProp.AppendToArray().managedReferenceValue = sequence;

            parentEditor.serializedObject.ApplyModifiedProperties();
            Deactivate();
        }

        private void AddNewRandom()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            parentEditor.serializedObject.Update();

            var random = PlayRandomClip.Create(clips[0].name);
            random.clips = clips;

            statesProp.AppendToArray().managedReferenceValue = random;

            parentEditor.serializedObject.ApplyModifiedProperties();
            Deactivate();
        }

        private void AddNewBlendTree1D()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            parentEditor.serializedObject.Update();

            var blendTree = BlendTree1D.Create(clips[0].name);
            blendTree.entries = clips.Select((clip, index) => new BlendTreeEntry1D { clip = clip, threshold = index}).ToList();

            statesProp.AppendToArray().managedReferenceValue = blendTree;

            parentEditor.serializedObject.ApplyModifiedProperties();
            Deactivate();
        }

        private void AddNewBlendTree2D()
        {
            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            parentEditor.serializedObject.Update();

            var blendTree = BlendTree2D.Create(clips[0].name);
            blendTree.entries = clips.Select((clip, index) => new BlendTreeEntry2D { clip = clip, threshold1 = index, threshold2 = index}).ToList();

            statesProp.AppendToArray().managedReferenceValue = blendTree;

            parentEditor.serializedObject.ApplyModifiedProperties();
            Deactivate();
        }

        public void Deactivate()
        {
            singleButton     .clickable = null;
            sequenceButton   .clickable = null;
            randomButton     .clickable = null;
            blendTree1DButton.clickable = null;
            blendTree2DButton.clickable = null;
            cancelButton     .clickable = null;

            parentEditor.entireUIRoot.Remove(uiRoot);
            parentEditor.mainUIRoot.SetFlex(true);
            parentEditor.currentBulkStateAdder = null;
        }
    }
}

}