using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player
{
[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor_UXML : Editor
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
    private VisualElement editLayerView;
    private VisualElement editPlayerSettingsView;
    private VisualElement metaDataView;
    private VisualElement layerBar;
    private Label errorLabel;
    private Label runtimeInfoLabel;

    private BulkStateAdder currentBulkStateAdder;
    private VisualElement dragAndDropClipTarget;

    private static Dictionary<Type, AnimationStateEditor> editorsForStateTypes;

    private new AnimationPlayer target => (AnimationPlayer) base.target;


    [SerializeField] private UIState uiState;
    [SerializeField] private int selectedLayerIndex;
    [SerializeField] private int selectedStateIndex;

    private SerializedProperty layersProp => serializedObject.FindProperty(nameof(AnimationPlayer.layers));
    private SerializedProperty SelectedLayerProp => layersProp.GetArrayElementAtIndex(selectedLayerIndex);
    private SerializedProperty SelectedStateProp => SelectedLayerProp.FindPropertyRelative("states").GetArrayElementAtIndex(selectedStateIndex);

    private void OnEnable()
    {
        if (target.EnsureVersionUpgraded())
            EditorUtility.SetDirty(target);

        if (Application.isPlaying)
        {
            if (updateRuntimeInfoCoroutine != null)
                target.StopCoroutine(updateRuntimeInfoCoroutine);
            updateRuntimeInfoCoroutine = target.StartCoroutine(UpdateRuntimeInfoLabel());
        }
        
        EditorApplication.update += UpdateDragAndDropTarget;
        Undo.undoRedoEvent += UndoRedo;
    }

    private void OnDisable()
    {
        if (updateRuntimeInfoCoroutine != null && target)
        {
            target.StopCoroutine(updateRuntimeInfoCoroutine);
            updateRuntimeInfoCoroutine = null;
        }

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
                runtimeInfoLabel.SetDisplayed(true);
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
        runtimeInfoLabel.SetDisplayed(false);

        errorLabel = mainUIRoot.Q<Label>("ErrorLabel");
        errorLabel.SetDisplayed(false);

        stateList              = new StateList(this, mainUIRoot.Q("StateList"));
        stateEditor            = new StateEditor(mainUIRoot, errorLabel);
        transitionEditor       = new TransitionEditor(this, mainUIRoot, errorLabel);
        editLayerView          = mainUIRoot.Q("EditLayerView");
        editPlayerSettingsView = mainUIRoot.Q("EditPlayerSettingsView");
        metaDataView           = mainUIRoot.Q("MetaDataView");

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
        dragAndDropClipTarget.SetDisplayed(false);
        SetupDragAndDropTester(dragAndDropClipTarget);

        SetUIState(UIState.EditStates, true);
        SetLayer(selectedLayerIndex, true);
        SetAnimationState(selectedStateIndex, true);

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
           dragAndDropClipTarget.SetDisplayed(uiState == UIState.EditStates && DragAndDrop.objectReferences.Any(o => o is AnimationClip));
    }
    
    private void UndoRedo(in UndoRedoInfo undo)
    {
        serializedObject.Update();
        
        SetLayer(selectedLayerIndex, true);
        SetAnimationState(selectedStateIndex, true);
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

        this.selectedLayerIndex = selectedLayer;
        stateList.SetLayer(layersProp.GetArrayElementAtIndex(selectedLayer));
    }

    private void SetAnimationState(int selectedStateIndex, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected State to " + selectedStateIndex);
        
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
                return;
            }

            var state = statesProp.GetArrayElementAtIndex(selectedStateIndex);

            stateEditor     .ShowAnimationState(state);
            transitionEditor.OnSelectedAnimationStateChanged();
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
        errorLabel.SetDisplayed(true);
    }

    private void SetUIState(UIState uiState, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set UI State");
        this.uiState = uiState;

        stateEditor           .SetDisplayed(uiState == UIState.EditStates);
        transitionEditor      .SetDisplayed(uiState == UIState.EditTransitions);
        editLayerView         .SetDisplayed(uiState == UIState.EditLayer);
        editPlayerSettingsView.SetDisplayed(uiState == UIState.EditPlayerSettings);
        metaDataView          .SetDisplayed(uiState == UIState.ViewMetaData);

        stateList.SetDisplayed(uiState is UIState.EditStates or UIState.EditTransitions);
        layerBar.SetDisplayed (uiState is UIState.EditStates or UIState.EditTransitions or UIState.EditLayer);

        statesToggle            .SetValueWithoutNotify(uiState == UIState.EditStates);
        transitionsToggle       .SetValueWithoutNotify(uiState == UIState.EditTransitions);
        layerToggle             .SetValueWithoutNotify(uiState == UIState.EditLayer);
        editPlayerSettingsToggle.SetValueWithoutNotify(uiState == UIState.EditPlayerSettings);
        viewMetaDataToggle      .SetValueWithoutNotify(uiState == UIState.ViewMetaData);
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
        private readonly AnimationPlayerEditor_UXML parentEditor;

        public StateList(AnimationPlayerEditor_UXML parentEditor, VisualElement stateListRoot)
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

            // @TODO: Try this instead maybe: https://forum.unity.com/threads/how-to-customize-the-itemsadded-for-a-listview.1175981/#post-7572853
            // listView.Q<Button>("unity-list-view__add-button").clickable = new Clickable(() =>
            var addButton = (Button) typeof(BaseListView).GetField("m_AddButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(listView);
            addButton.clickable = new Clickable(ShowAddStateMenu);

            var deleteButton = (Button) typeof(BaseListView).GetField("m_RemoveButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(listView);
            deleteButton.clickable = new Clickable(DeleteSelectedState);

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
                    parentEditor.SetAnimationState(indexList[0]);
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
                parentEditor.SetAnimationState(statesProp.arraySize - 1);
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

            parentEditor.SetAnimationState(index);
        }

        public void SetLayer(SerializedProperty layer)
        {
            listView.Unbind();
            listView.BindProperty(layer.FindPropertyRelative(nameof(AnimationLayer.states)));
        }

        public void SetDisplayed(bool displayed)
        {
            root.SetDisplayed(displayed);
        }
    }

    [Serializable]
    private class TransitionEditor
    {
        private readonly AnimationPlayerEditor_UXML parentEditor;
        private readonly Label errorLabel;
        private readonly VisualElement transitionEditorRoot;
        private readonly Label transitionFromLabel;
        private readonly ListView transitionsList;

        private readonly List<SerializedProperty> transitions = new();

        public TransitionEditor(AnimationPlayerEditor_UXML parentEditor, VisualElement entireUIRoot, Label errorLabel)
        {
            this.parentEditor = parentEditor;
            this.errorLabel = errorLabel;
            transitionEditorRoot = entireUIRoot.Q("EditTransitionsView");
            transitionFromLabel = transitionEditorRoot.Q<Label>("TransitionsFromLabel");
            transitionsList = transitionEditorRoot.Q<ListView>("Transitions");

            transitionsList.itemsSource = transitions;

            const string path = "Packages/com.baste.animationplayer/Editor/UI/TransitionListEntry.uxml";
            transitionsList.makeItem = () => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path).CloneRoot();

            transitionsList.bindItem = BindTransitionListItem;
            transitionsList.unbindItem = (ve, _) =>
            {
                ve.Unbind();
            };

            transitionsList.itemsAdded += indices =>
            {
                foreach (var index in indices)
                {
                    var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));
                    var newTransition = transitionsProp.AppendToArray();
                    
                    var nameProp      = newTransition.FindPropertyRelative(nameof(StateTransition.name));
                    var isDefaultProp = newTransition.FindPropertyRelative(nameof(StateTransition.isDefault));
                    var fromStateProp = newTransition.FindPropertyRelative(nameof(StateTransition.fromState));
                    var toStateProp   = newTransition.FindPropertyRelative(nameof(StateTransition.toState));

                    nameProp.stringValue = "Transition";
                    isDefaultProp.boolValue = index == 0;

                    fromStateProp.managedReferenceValue = parentEditor.SelectedStateProp.managedReferenceValue;
                    toStateProp.managedReferenceValue = null;
                    var newData = newTransition.FindPropertyRelative(nameof(StateTransition.transitionData));

                    newData.FindPropertyRelative(nameof(TransitionData.type))                   .intValue             = (int) TransitionType.Linear;
                    newData.FindPropertyRelative(nameof(TransitionData.duration))               .floatValue           = 1f;
                    newData.FindPropertyRelative(nameof(TransitionData.timeOffsetIntoNewState)) .doubleValue          = 0f;
                    newData.FindPropertyRelative(nameof(TransitionData.curve))                  .animationCurveValue  = null;
                    newData.FindPropertyRelative(nameof(TransitionData.clip))                   .objectReferenceValue = null;

                    parentEditor.serializedObject.ApplyModifiedProperties();
                    transitions[index] = newTransition;
                }
            };

            transitionsList.itemsRemoved += indices =>
            {
                var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));

                foreach (var index in indices)
                {
                    var property = transitions[index];
                    var propertyPath = property.propertyPath;

                    var startOfIndex = propertyPath.LastIndexOf("[", StringComparison.Ordinal) + 1;
                    var propertyIndex = int.Parse(propertyPath[startOfIndex..^1]);

                    transitionsProp.DeleteArrayElementAtIndex(propertyIndex);
                    parentEditor.serializedObject.ApplyModifiedProperties();
                }

                // OnSelectedAnimationStateChanged();
            };
        }

        private void AddTransition()
        {
            var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));
            var newTransition = transitionsProp.AppendToArray();
            newTransition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue = parentEditor.SelectedStateProp.managedReferenceValue;

            transitions.Add(transitionsProp);

            parentEditor.serializedObject.ApplyModifiedProperties();
            transitionsList.RefreshItems();
        }

        public void SetDisplayed(bool displayed)
        {
            transitionEditorRoot.SetDisplayed(displayed);
            if (displayed)
                OnSelectedAnimationStateChanged();
        }

        public void OnSelectedAnimationStateChanged()
        {
            transitionFromLabel.text = $"Transitions from {parentEditor.SelectedStateProp.FindPropertyRelative("name").stringValue}";

            transitions.Clear();

            var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));
            var selectedStateObject = (AnimationPlayerState) parentEditor.SelectedStateProp.managedReferenceValue;

            for (int i = 0; i < transitionsProp.arraySize; i++)
            {
                var transition = transitionsProp.GetArrayElementAtIndex(i);
                var fromState = transition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue;

                if (fromState == selectedStateObject)
                    transitions.Add(transition);
            }

            transitionsList.RefreshItems();
        }

        private void BindTransitionListItem(VisualElement ve, int index)
        {
            var transitionProp = transitions[index];

            var nameProp           = transitionProp.FindPropertyRelative(nameof(StateTransition.name));
            var toStateProp        = transitionProp.FindPropertyRelative(nameof(StateTransition.toState));
            var transitionDataProp = transitionProp.FindPropertyRelative(nameof(StateTransition.transitionData));

            var transitionTypeProp = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.type));
            var curveProp          = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.curve));
            var durationProp       = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.duration));
            var clipProp           = transitionDataProp.FindPropertyRelative(nameof(StateTransition.transitionData.clip));

            var topRow    = ve.Q("TopRow");
            var bottomRow = ve.Q("BottomRow");

            var transitionTypeField = topRow.Q<EnumField>("TransitionType");
            var toStateDropdown     = topRow.Q<DropdownField>("ToStateDropdown");

            var nameField     = bottomRow.Q<TextField>  ("TransitionName");
            var durationField = bottomRow.Q<FloatField> ("Duration");
            var curveField    = bottomRow.Q<CurveField> ("Curve");
            var clipField     = bottomRow.Q<ObjectField>("Clip");

            transitionTypeField.BindProperty(transitionTypeProp);
            nameField          .BindProperty(nameProp);
            durationField      .BindProperty(durationProp);
            curveField         .BindProperty(curveProp);
            clipField          .BindProperty(clipProp);

            clipField.objectType = typeof(AnimationClip);

            var statesProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.states));
            var states = new List<AnimationPlayerState>();
            for (int i = 0; i < statesProp.arraySize; i++)
            {
                var state = statesProp.GetArrayElementAtIndex(i);
                if (state.propertyPath != parentEditor.SelectedStateProp.propertyPath)
                    states.Add((AnimationPlayerState) state.managedReferenceValue);
            }

            var stateNames = states.Select(s => s.Name).ToList();

            toStateDropdown.choices = stateNames;

            {
                // There's no public SetIndexWithoutNotify. There is an internal SetIndexWithoutNotify, but it also finds the element and calls SetValueWithoutNotify. 
                var indexOfState = states.IndexOf((AnimationPlayerState) toStateProp.managedReferenceValue);
                if (indexOfState == -1)
                {
                    toStateDropdown.SetValueWithoutNotify(null); // When we create a new state, thee
                }
                else
                {
                    var stateName = stateNames[indexOfState];
                    toStateDropdown.SetValueWithoutNotify(stateName);
                }
            }

            toStateDropdown.RegisterValueChangedCallback(_ =>
            {
                toStateProp.managedReferenceValue = states[toStateDropdown.index];
                parentEditor.serializedObject.ApplyModifiedProperties();
            });

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
                clipField .SetDisplayed(transitionType == TransitionType.Clip);
                curveField.SetDisplayed(transitionType == TransitionType.Curve);
            }
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
            stateEditorRoot.SetDisplayed(displayed);
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
                selectedStateRoot.Add(shownEditor.RootVisualElement);
                errorLabel.SetDisplayed(false);
            }
            else
            {
                ShowError("No known editor for state type " + stateType);
            }
        }

        private void ShowError(string error)
        {
            selectedStateRoot.SetDisplayed(false);
            errorLabel.text = error;
            errorLabel.SetDisplayed(true);
        }

        public void RenamePressedOnState()
        {
            stateEditorRoot.Q<TextField>("name").Focus();
        }
    }

    public static Comparison<Type> AddTypesSorter = SortTypesForAddStatDropdown;
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

    static AnimationPlayerEditor_UXML()
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
        private readonly AnimationPlayerEditor_UXML parentEditor;
        private readonly List<AnimationClip> clips;
        private readonly VisualElement uiRoot;

        private readonly Button singleButton;
        private readonly Button sequenceButton;
        private readonly Button randomButton;
        private readonly Button blendTree1DButton;
        private readonly Button blendTree2DButton;
        private readonly Button cancelButton;

        public BulkStateAdder(List<AnimationClip> clips, AnimationPlayerEditor_UXML parentEditor)
        {
            this.parentEditor = parentEditor;
            this.clips = clips;
            parentEditor.mainUIRoot.SetDisplayed(false);

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
            parentEditor.mainUIRoot.SetDisplayed(true);
            parentEditor.currentBulkStateAdder = null;
        }
    }
}

}