using System;
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
    private VisualElement root;
    private Toggle statesToggle;
    private Toggle transitionsToggle;
    private Toggle layerToggle;
    private Toggle editPlayerSettingsToggle;
    private Toggle viewMetaDataToggle;

    private StateList stateList;
    private StateEditor stateEditor;
    private TransitionEditor transitionEditor;
    private VisualElement editLayerView;
    private VisualElement editPlayerSettingsView;
    private VisualElement metaDataView;
    private VisualElement layerBar;
    private Label errorLabel;

    private static Dictionary<Type, AnimationStateEditor> editorsForStateTypes;

    private new AnimationPlayer target => (AnimationPlayer) base.target;

    private SerializedProperty layersProp;

    [SerializeField] private UIState uiState;
    [SerializeField] private int selectedLayerIndex;
    [SerializeField] private int selectedStateIndex;

    private SerializedProperty SelectedLayerProp => layersProp.GetArrayElementAtIndex(selectedLayerIndex);
    private SerializedProperty SelectedStateProp => SelectedLayerProp.FindPropertyRelative("states").GetArrayElementAtIndex(selectedStateIndex);

    private void OnEnable()
    {
        if (target.EnsureVersionUpgraded())
            EditorUtility.SetDirty(target);
    }

    public override VisualElement CreateInspectorGUI()
    {
        const string path = "Packages/com.baste.animationplayer/Editor/UI/AnimationPlayerEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        root = new ();
        visualTreeAsset.CloneTree(root);

        layersProp = serializedObject.FindProperty(nameof(AnimationPlayer.layers));

        errorLabel = root.Q<Label>("ErrorLabel");
        errorLabel.SetDisplayed(false);

        stateList              = new StateList(this);
        stateEditor            = new StateEditor(root, errorLabel);
        transitionEditor       = new TransitionEditor(this, root, errorLabel);
        editLayerView          = root.Q("EditLayerView");
        editPlayerSettingsView = root.Q("EditPlayerSettingsView");
        metaDataView           = root.Q("MetaDataView");

        var stateBar = root.Q("StateBar");

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

        layerBar = root.Q("LayerBar");
        var layerDropdown = layerBar.Q<DropdownField>("SelectedLayerDropdown");
        layerDropdown.choices = new (layersProp.arraySize);
        for (int i = 0; i < layersProp.arraySize; i++)
            layerDropdown.choices.Add(layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
        layerDropdown.index = selectedLayerIndex;

        layerDropdown.RegisterValueChangedCallback(SelectedLayerChanged);

        SetUIState(UIState.EditStates, true);
        SetLayer(selectedLayerIndex, true);
        SetAnimationState(selectedStateIndex, true);

        return root;

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

    private void SetLayer(int selectedLayer, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected Layer");

        this.selectedLayerIndex = selectedLayer;
        stateList.SetLayer(layersProp.GetArrayElementAtIndex(selectedLayer));
    }

    private void SetAnimationState(int selectedStateIndex, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected State");
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
            var layer = layersProp.GetArrayElementAtIndex(selectedLayerIndex);

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
        [SerializeField] private ListView listView;
        private readonly VisualElement root;
        private readonly AnimationPlayerEditor_UXML parentEditor;

        public StateList(AnimationPlayerEditor_UXML parentEditor)
        {
            this.parentEditor = parentEditor;
            root = parentEditor.root.Q("StateList");
            listView = root.Q<ListView>();

            listView.makeItem = () => new VisualElement().WithClass("state-list--state-label-line")
                                                         .WithChild(new Label().WithName("name").WithClass("state-list--state-label"))
                                                         .WithChild(new Label().WithName("type").WithClass("state-list--state-label"));
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

            var addButton = (Button) typeof(BaseListView).GetField("m_AddButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(listView);
            addButton.clickable = new Clickable(ShowAddStateMenu);

            listView.selectedIndicesChanged += indices =>
            {
                // Code is silly because listView returns an IEnumerable instead of eg. an IReadOnlyList or something where we can actullay check the count lol
                foreach (var index in indices)
                {
                    parentEditor.SetAnimationState(index);
                    break;
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

            var addButton = (Button) typeof(BaseListView).GetField("m_AddButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(transitionsList);
            addButton.clickable = new Clickable(() =>
            {
                var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));
                transitionsProp.arraySize++;

                var newTransition = transitionsProp.GetArrayElementAtIndex(transitionsProp.arraySize - 1);
                newTransition.FindPropertyRelative(nameof(StateTransition.fromState)).managedReferenceValue = parentEditor.SelectedStateProp.managedReferenceValue;

                transitions.Add(transitionsProp);
            });
        }

        public void SetDisplayed(bool displayed)
        {
            transitionEditorRoot.SetDisplayed(displayed);
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
            var transitionsProp = parentEditor.SelectedLayerProp.FindPropertyRelative(nameof(AnimationLayer.transitions));
            var transitionProp = transitionsProp.GetArrayElementAtIndex(index);

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
            toStateDropdown.index = states.IndexOf((AnimationPlayerState) toStateProp.managedReferenceValue);
            toStateDropdown.RegisterValueChangedCallback(_ =>
            {
                toStateProp.managedReferenceValue = states[toStateDropdown.index];
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
    }

    static AnimationPlayerEditor_UXML()
    {
        editorsForStateTypes = new();
        var editorTypes = TypeCache.GetTypesDerivedFrom(typeof(AnimationStateEditor));
        foreach (var editorType in editorTypes)
        {
            if (editorType.IsAbstract)
                continue;
            try
            {
                var editor = (AnimationStateEditor) Activator.CreateInstance(editorType);
                var editedType = editor.GetEditedType();
                if (editorsForStateTypes.ContainsKey(editedType))
                {
                    Debug.LogError("There are two ");
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
}
}