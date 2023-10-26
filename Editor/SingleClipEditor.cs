using System;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Animation_Player
{
public class SingleClipEditor : AnimationStateEditor
{
    private VisualElement root;
    private TextField nameTextField;
    private ObjectField clipField;
    private DoubleField speedField;

    public override VisualElement RootVisualElement => root;
    public override Type GetEditedType() => typeof(SingleClip);

    public override AnimationPlayerState CreateNewState(int stateIndex) => SingleClip.Create("Single Clip State " + stateIndex);

    public override void GenerateUI()
    {
        var path = "Packages/com.baste.animationplayer/Editor/UI/SingleClipEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        nameTextField = root.Q<TextField>("name");
        clipField     = root.Q<ObjectField>("clip");
        speedField    = root.Q<DoubleField>("speed");
    }

    public override void BindUI(SerializedProperty stateProperty)
    {
        nameTextField.BindProperty(stateProperty.FindPropertyRelative("name"));
        clipField    .BindProperty(stateProperty.FindPropertyRelative(nameof(SingleClip.clip)));
        speedField   .BindProperty(stateProperty.FindPropertyRelative(nameof(AnimationPlayerState.speed)));
    }

    public override void ClearBindings(SerializedProperty stateProperty)
    {
        nameTextField.Unbind();
        clipField    .Unbind();
        speedField   .Unbind();
    }

}
}