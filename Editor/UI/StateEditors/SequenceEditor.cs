using System;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Animation_Player
{
public class SequenceEditor : AnimationStateEditor
{
    private VisualElement root;
    private TextField nameTextField;
    private DoubleField speedField;
    private EnumField loopModeField;
    private ListView clipList;

    public override VisualElement RootVisualElement => root;
    public override void GenerateUI()
    {
        var path = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/SequenceEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        nameTextField = root.Q<TextField>  ("name");
        speedField    = root.Q<DoubleField>("speed");
        loopModeField = root.Q<EnumField>  ("loopMode");
        clipList      = root.Q<ListView>   ("clips");
    }

    public override void BindUI(SerializedProperty stateProperty)
    {
        nameTextField.BindProperty(stateProperty.FindPropertyRelative("name"));
        speedField   .BindProperty(stateProperty.FindPropertyRelative(nameof(Sequence.speed)));
        loopModeField.BindProperty(stateProperty.FindPropertyRelative(nameof(Sequence.loopMode)));

        var clipsProp = stateProperty.FindPropertyRelative(nameof(Sequence.clips));
        clipList.BindProperty(clipsProp);
    }

    public override void ClearBindings(SerializedProperty stateProperty)
    {
        nameTextField.Unbind();
        speedField   .Unbind();
        loopModeField.Unbind();
        clipList     .Unbind();
    }

    public override Type GetEditedType() => typeof(Sequence);

    public override AnimationPlayerState CreateNewState(int stateIndex)
    {
        return Sequence.Create($"State {stateIndex}");
    }
}
}