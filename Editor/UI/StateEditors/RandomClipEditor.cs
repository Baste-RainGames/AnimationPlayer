using System;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Animation_Player
{
public class RandomClipEditor : AnimationStateEditor
{
    private VisualElement root;
    private TextField nameTextField;
    private DoubleField speedField;
    private ListView clipList;

    public override VisualElement RootVisualElement => root;
    public override void GenerateUI()
    {
        var path = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/RandomClipEditor.uxml";
        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        nameTextField = root.Q<TextField>("name");
        speedField    = root.Q<DoubleField>("speed");
        clipList      = root.Q<ListView>("clips");
    }

    public override void BindUI(SerializedProperty stateProperty)
    {
        nameTextField.BindProperty(stateProperty.FindPropertyRelative("name"));
        speedField   .BindProperty(stateProperty.FindPropertyRelative(nameof(PlayRandomClip.speed)));
        clipList     .BindProperty(stateProperty.FindPropertyRelative(nameof(PlayRandomClip.clips)));
    }

    public override void ClearBindings(SerializedProperty stateProperty)
    {
        nameTextField.Unbind();
        speedField   .Unbind();
        clipList     .Unbind();
    }

    public override Type GetEditedType() => typeof(PlayRandomClip);

    public override AnimationPlayerState CreateNewState(int stateIndex)
    {
        return PlayRandomClip.Create($"State {stateIndex}");
    }
}
}