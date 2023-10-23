using UnityEditor;

using UnityEngine.UIElements;

namespace Animation_Player
{
[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor_UXML : Editor
{
    private static VisualTreeAsset LoadVisualTree() {
        // Editor/UI/AnimationPlayerEditorNew.uss
        return AssetDatabase.LoadAssetAtPath <VisualTreeAsset> ( AssetDatabase.GUIDToAssetPath("127de2295f82bdc48a4e27786dfc0be3") );
    }

    private Toggle statesToggle;
    private Toggle transitionsToggle;
    private Toggle layerToggle;
    private Toggle editPlayerSettingsToggle;
    private Toggle viewMetaDataToggle;

    private VisualElement editStatesView;
    private VisualElement editTransitionsView;
    private VisualElement editLayerView;
    private VisualElement editPlayerSettingsView;
    private VisualElement metaDataView;


    public override VisualElement CreateInspectorGUI()
    {
        var visualTreeAsset = LoadVisualTree();
        var root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        SetupCallbacks(root);
        SetState(UIState.EditStates);
        return root;
    }

    private void SetupCallbacks(VisualElement root)
    {
        SetupUIStateSelection(root);
    }

    private void SetupUIStateSelection(VisualElement root)
    {
        var middleBar = root.Q("MiddleBar");

        statesToggle             = middleBar.Q<Toggle>("EditStatesToggle");
        transitionsToggle        = middleBar.Q<Toggle>("EditTransitionsToggle");
        layerToggle              = middleBar.Q<Toggle>("EditLayerToggle");
        editPlayerSettingsToggle = middleBar.Q<Toggle>("EditPlayerSettingsToggle");
        viewMetaDataToggle       = middleBar.Q<Toggle>("ViewMetaDataToggle");

        editStatesView         = root.Q("EditStatesView");
        editTransitionsView    = root.Q("EditTransitionsView");
        editLayerView          = root.Q("EditLayerView");
        editPlayerSettingsView = root.Q("EditPlayerSettingsView");
        metaDataView           = root.Q("MetaDataView");

        statesToggle            .RegisterValueChangedCallback(c => StateClicked(c, UIState.EditStates));
        transitionsToggle       .RegisterValueChangedCallback(c => StateClicked(c, UIState.EditTransitions));
        layerToggle             .RegisterValueChangedCallback(c => StateClicked(c, UIState.EditLayer));
        editPlayerSettingsToggle.RegisterValueChangedCallback(c => StateClicked(c, UIState.EditPlayerSettings));
        viewMetaDataToggle      .RegisterValueChangedCallback(c => StateClicked(c, UIState.ViewMetaData));

        void StateClicked(ChangeEvent<bool> evt, UIState state)
        {
            if (!evt.newValue)
                return;
            SetState(state);
        }
    }

    private void SetState(UIState state)
    {
        editStatesView        .SetDisplayed(state == UIState.EditStates);
        editTransitionsView   .SetDisplayed(state == UIState.EditTransitions);
        editLayerView         .SetDisplayed(state == UIState.EditLayer);
        editPlayerSettingsView.SetDisplayed(state == UIState.EditPlayerSettings);
        metaDataView          .SetDisplayed(state == UIState.ViewMetaData);

        statesToggle            .SetValueWithoutNotify(state == UIState.EditStates);
        transitionsToggle       .SetValueWithoutNotify(state == UIState.EditTransitions);
        layerToggle             .SetValueWithoutNotify(state == UIState.EditLayer);
        editPlayerSettingsToggle.SetValueWithoutNotify(state == UIState.EditPlayerSettings);
        viewMetaDataToggle      .SetValueWithoutNotify(state == UIState.ViewMetaData);
    }

    private enum UIState
    {
        EditStates,
        EditTransitions,
        EditLayer,
        EditPlayerSettings,
        ViewMetaData,
    }
}
}