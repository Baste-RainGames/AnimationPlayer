using System;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player
{
public class BlendTree2DEditor : AnimationStateEditor
{
    private VisualElement root;
    private TextField nameTextField;
    private DoubleField speedField;
    private TextField blendVariableField;
    private TextField blendVariableField2;
    private ListView entriesField;

    public override VisualElement RootVisualElement => root;
    public override void GenerateUI()
    {
        var editorPath = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/BlendTree2DEditor.uxml";
        var entryPath  = "Packages/com.baste.animationplayer/Editor/UI/StateEditors/BlendTree2DEditor_Entry.uxml";

        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(editorPath);
        var blendEntryAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(entryPath);

        root = new VisualElement();
        visualTreeAsset.CloneTree(root);

        nameTextField       = root.Q<TextField>  ("name");
        speedField          = root.Q<DoubleField>("speed");
        blendVariableField  = root.Q<TextField>  ("blendVariable");
        blendVariableField2 = root.Q<TextField>  ("blendVariable 2");
        entriesField        = root.Q<ListView>   ("entries");

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
            ve.Q<ObjectField>("Clip")      .Unbind();
            ve.Q<FloatField> ("Threshold1").Unbind();
            ve.Q<FloatField> ("Threshold2").Unbind();
        };
    }

    public override void BindUI(SerializedProperty stateProperty)
    {
        nameTextField      .BindProperty(stateProperty.FindPropertyRelative("name"));
        speedField         .BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree2D.speed)));
        blendVariableField .BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree2D.blendVariable)));
        blendVariableField2.BindProperty(stateProperty.FindPropertyRelative(nameof(BlendTree2D.blendVariable2)));

        var entriesProp = stateProperty.FindPropertyRelative(nameof(BlendTree2D.entries));
        entriesField.BindProperty(entriesProp);

        entriesField.bindItem = (ve, index) =>
        {
            var entry = entriesProp.GetArrayElementAtIndex(index);
            ve.Q<ObjectField>("Clip")      .BindProperty(entry.FindPropertyRelative("clip"));
            ve.Q<FloatField> ("Threshold1").BindProperty(entry.FindPropertyRelative("threshold1"));
            ve.Q<FloatField> ("Threshold2").BindProperty(entry.FindPropertyRelative("threshold2"));
        };
    }

    public override void ClearBindings(SerializedProperty stateProperty)
    {
        nameTextField     .Unbind();
        speedField        .Unbind();
        blendVariableField.Unbind();
        entriesField      .Unbind();
    }

    public override Type GetEditedType() => typeof(BlendTree2D);

    public override AnimationPlayerState CreateNewState(int stateIndex)
    {
        return BlendTree2D.Create($"State {stateIndex}");
    }
}
}