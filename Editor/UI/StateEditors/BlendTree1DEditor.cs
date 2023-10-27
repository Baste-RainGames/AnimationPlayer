using System;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player
{
public class BlendTree1DEditor : AnimationStateEditor
{
    private VisualElement root;
    private TextField nameTextField;
    private Toggle compensateToggle;
    private DoubleField speedField;
    private TextField blendVariableField;
    private ListView entriesField;

    public override VisualElement RootVisualElement => root;
    public override void GenerateUI()
    {
        var editorPath = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/BlendTree1DEditor.uxml";
        var entryPath  = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/BlendTree1DEditor_Entry.uxml";

        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(editorPath);
        var blendEntryAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(entryPath);

        root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        nameTextField      = root.Q<TextField>  ("name");
        speedField         = root.Q<DoubleField>("speed");
        blendVariableField = root.Q<TextField>  ("blendVariable");
        compensateToggle   = root.Q<Toggle>     ("compensate");
        entriesField       = root.Q<ListView>   ("entries");

        entriesField.makeItem = () =>
        {
            var dumbClone = blendEntryAsset.CloneTree();
            var actualThingIWant = dumbClone.Children().First();
            dumbClone.Remove(actualThingIWant);

            actualThingIWant.Q<ObjectField>().objectType = typeof(AnimationClip);
            return actualThingIWant;
        };

        entriesField.unbindItem = (ve, _) =>
        {
            ve.Q<ObjectField>("Clip")     .Unbind();
            ve.Q<FloatField> ("Threshold").Unbind();
        };
    }

    public override void BindUI(SerializedProperty stateProperty)
    {
        nameTextField     .BindProperty(stateProperty.FindPropertyRelative("name"));
        speedField        .BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree1D.speed)));
        blendVariableField.BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree1D.blendVariable)));
        compensateToggle  .BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree1D.compensateForDifferentDurations)));

        var entriesProp = stateProperty.FindPropertyRelative(nameof(BlendTree1D.entries));
        entriesField.BindProperty(entriesProp);

        entriesField.bindItem = (ve, index) =>
        {
            var entry = entriesProp.GetArrayElementAtIndex(index);
            ve.Q<ObjectField>("Clip").BindProperty(entry.FindPropertyRelative("clip"));
            ve.Q<FloatField> ("Threshold").BindProperty(entry.FindPropertyRelative("threshold"));
        };
    }

    public override void ClearBindings(SerializedProperty stateProperty)
    {
        nameTextField     .Unbind();
        speedField        .Unbind();
        blendVariableField.Unbind();
        entriesField      .Unbind();
    }

    public override Type GetEditedType() => typeof(BlendTree1D);

    public override AnimationPlayerState CreateNewState(int stateIndex)
    {
        return BlendTree1D.Create($"State {stateIndex}");
    }
}
}