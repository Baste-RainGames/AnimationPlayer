using System;
using System.Collections.Generic;
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

    private EditStateSection editStateView;
    private StateList stateList;
    private VisualElement editTransitionsView;
    private VisualElement editLayerView;
    private VisualElement editPlayerSettingsView;
    private VisualElement metaDataView;
    private VisualElement layerBar;
    private Label errorLabel;

    private static Dictionary<Type, AnimationStateEditor> editorsForStateTypes;

    private new AnimationPlayer target => (AnimationPlayer) base.target;

    private SerializedProperty layersProp;

    [SerializeField] private UIState uiState;
    [SerializeField] private int selectedLayer;
    [SerializeField] private int selectedState;

    private SerializedProperty SelectedLayerProp => layersProp.GetArrayElementAtIndex(selectedLayer);
    private SerializedProperty SelectedStateProp => SelectedLayerProp.FindPropertyRelative("states").GetArrayElementAtIndex(selectedState);

    public override VisualElement CreateInspectorGUI()
    {
        var path = "Packages/com.baste.animationplayer/Editor/UI/AnimationPlayerEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        root = new ();
        visualTreeAsset.CloneTree(root);

        layersProp = serializedObject.FindProperty(nameof(AnimationPlayer.layers));

        errorLabel = root.Q<Label>("ErrorLabel");
        errorLabel.SetDisplayed(false);

        editStateView          = new EditStateSection(root, errorLabel);
        stateList              = new StateList(this);
        editTransitionsView    = root.Q("EditTransitionsView");
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
        layerDropdown.index = selectedLayer;

        layerDropdown.RegisterValueChangedCallback(SelectedLayerChanged);

        SetUIState(UIState.EditStates, true);
        SetLayer(selectedLayer, true);
        SetAnimationState(selectedState, true);

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

        this.selectedLayer = selectedLayer;
        stateList.SetLayer(layersProp.GetArrayElementAtIndex(selectedLayer));
    }

    private void SetAnimationState(int selectedState, bool skipUndo = false)
    {
        if (!skipUndo)
            Undo.RecordObject(this, "Set Selected State");
        this.selectedState = selectedState;

        try
        {
            if (layersProp.arraySize == 0)
            {
                ShowError("No Layers");
                return;
            }

            if (selectedLayer < 0 || selectedLayer >= layersProp.arraySize)
            {
                ShowError($"Selected layer {selectedLayer} out of bounds! There are {layersProp.arraySize} layers");
                return;
            }

            var statesProp = layersProp.GetArrayElementAtIndex(selectedLayer).FindPropertyRelative("states");
            if (statesProp.arraySize == 0)
            {
                ShowError("No States");
                return;
            }

            if (selectedState < 0 || selectedState >= statesProp.arraySize)
            {
                ShowError($"Selected state {selectedState} out of bounds! There are {statesProp.arraySize} states");
                return;
            }

            var state = statesProp.GetArrayElementAtIndex(selectedState);
            editStateView.ShowAnimationState(state);
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

        editStateView         .SetDisplayed(uiState == UIState.EditStates);
        editTransitionsView   .SetDisplayed(uiState == UIState.EditTransitions);
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
                                            .GetArrayElementAtIndex(parentEditor.selectedLayer)
                                            .FindPropertyRelative(nameof(AnimationLayer.states))
                                            .GetArrayElementAtIndex(index);

                var nameLabel = ve.Q<Label>("name");
                var typeLabel = ve.Q<Label>("type");
                nameLabel.BindProperty(stateProp.FindPropertyRelative("name"));
                typeLabel.text = $"({stateProp.managedReferenceValue.GetType().Name})";
            };
            listView.unbindItem = (ve, _) =>
            {
                var nameLabel = ve.Q<Label>("name");
                nameLabel.Unbind();
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
            }
        }

        public void SetLayer(SerializedProperty layer)
        {
            listView.Unbind();
            listView.BindProperty(layer.FindPropertyRelative(nameof(AnimationLayer.states)));

            // listView.Unbind();
            // listView.bindingPath = layer.FindPropertyRelative(nameof(AnimationLayer.states)).propertyPath;
            // listView.Bind(layer.serializedObject);
        }

        public void SetDisplayed(bool displayed)
        {
            root.SetDisplayed(displayed);
        }
    }

    [Serializable]
    private class EditStateSection
    {
        private readonly Label errorLabel;
        private readonly VisualElement editStateRoot;
        private readonly VisualElement selectedStateRoot;

        private AnimationStateEditor shownEditor;

        public EditStateSection(VisualElement entireUIRoot, Label errorLabel)
        {
            editStateRoot = entireUIRoot.Q("EditStateView");
            selectedStateRoot = editStateRoot.Q("SelectedState");
            this.errorLabel = errorLabel;
        }

        public void SetDisplayed(bool displayed)
        {
            editStateRoot.SetDisplayed(displayed);
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