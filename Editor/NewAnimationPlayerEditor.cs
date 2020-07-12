using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Animation_Player {
[CustomEditor(typeof(AnimationPlayer))]
public class NewAnimationPlayerEditor : Editor {
    private SerializedProperty layers;
    private VisualElement layersRoot;
    private List<VisualElement> layerVisualElements = new List<VisualElement>();
    private PopupField<VisualElement> layersDropdown;

    private void OnEnable()
    {
        layers = serializedObject.FindProperty(nameof(AnimationPlayer.layers));
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();

        layersRoot = CreateLayers();
        layersDropdown = CreateLayerDropdown();
        var addLayersButton = CreateAddLayerButton();

        root.Add(layersDropdown);
        root.Add(layersRoot);
        root.Add(addLayersButton);

        return root;
    }

    private Button CreateAddLayerButton()
    {
        return new Button(AddLayer)
        {
            text = "Add Layer!"
        };
    }

    private VisualElement CreateLayers()
    {
        var layersContainer = new VisualElement();
        for (int i = 0; i < layers.arraySize; i++)
            layersContainer.Add(CreateLayerVisualElement(i));

        layerVisualElements[0].style.display = DisplayStyle.Flex;
        return layersContainer;
    }

    private PopupField<VisualElement> CreateLayerDropdown()
    {
        var layerDropdown = new PopupField<VisualElement>(
            choices:                     layerVisualElements,
            defaultIndex:                0,
            formatSelectedValueCallback: ve => ve.Q<Label>().text,
            formatListItemCallback:      ve => ve.Q<Label>().text
        );
        layerDropdown.RegisterValueChangedCallback(LayerDropdownChanged);
        return layerDropdown;
    }

    private void LayerDropdownChanged(ChangeEvent<VisualElement> evt)
    {
        evt.previousValue.style.display = DisplayStyle.None;
        evt.newValue.style.display      = DisplayStyle.Flex;
    }

    private void AddLayer()
    {
        layers.arraySize++;
        var newLayerVisualElement = CreateLayerVisualElement(layers.arraySize - 1);
        layersRoot.Add(newLayerVisualElement);
        layersDropdown.value = newLayerVisualElement;
        serializedObject.ApplyModifiedProperties();
    }

    private VisualElement CreateLayerVisualElement(int i)
    {
        var layerVis = new VisualElement();
        layerVis.Add(new Label("Layer " + i));
        layerVis.style.display = DisplayStyle.None;

        layerVisualElements.Add(layerVis);

        return layerVis;
    }
}
}