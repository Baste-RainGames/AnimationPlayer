using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Animation_Player {
[CustomEditor(typeof(AnimationPlayer))]
public class NewAnimationPlayerEditor : Editor {
    private const string rootUXMLPath = "Packages/com.baste.animationplayer/Editor/AnimationPlayer.uxml";
    private const string layerUXMLPath = "Packages/com.baste.animationplayer/Editor/AnimationLayer.uxml";

    private SerializedProperty layers;
    private VisualElement layersContainer;
    private List<VisualElement> visualElementsForLayers;
    private PopupField<VisualElement> layersDropdown;
    private VisualTreeAsset layerTemplate;
    private VisualElement editorRootElement;
    private VisualElement rootHackForUndo;
    private TextField layerNameField;
    private int selectedLayer;

    private void OnEnable()
    {
        layers = serializedObject.FindProperty(nameof(AnimationPlayer.layers));

        var directReference = (AnimationPlayer) target;
        if (directReference.layers == null)
            InitializeNewAnimationPlayer();

        Undo.undoRedoPerformed += OnUndo;
    }

    private void InitializeNewAnimationPlayer()
    {
        layers.arraySize = 1;
        var baseLayer = layers.GetArrayElementAtIndex(0);
        baseLayer.FindPropertyRelative("name").stringValue = "Base Layer";
        var defaultTransition = serializedObject.FindProperty("defaultTransition");
        defaultTransition.FindPropertyRelative("duration").floatValue = .1f;
        serializedObject.ApplyModifiedProperties();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndo;
    }

    private void OnUndo()
    {
        rootHackForUndo.Clear();
        CreateGUI(rootHackForUndo);
    }

    public override VisualElement CreateInspectorGUI()
    {
        rootHackForUndo = new VisualElement();
        CreateGUI(rootHackForUndo);
        return rootHackForUndo;
    }

    private void CreateGUI(VisualElement container)
    {
        serializedObject.Update();
        editorRootElement = CloneVisualTreeAsset(rootUXMLPath).CloneTree();

        CreateLayers();
        FillTopBar();

        SetSelectedLayer(selectedLayer);

        container.Add(editorRootElement);
    }

    private static VisualTreeAsset CloneVisualTreeAsset(string assetPath)
    {
        var rootTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
        Assert.IsNotNull(rootTemplate, $"no uxml asset at path {rootUXMLPath}");
        return rootTemplate;
    }

    private void CreateLayers()
    {
        layersContainer = editorRootElement.Q<VisualElement>("Layers Container");
        layerTemplate = CloneVisualTreeAsset(layerUXMLPath);
        visualElementsForLayers = new List<VisualElement>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            var layerElement = CreateLayerVisualElement(i);
            visualElementsForLayers.Add(layerElement);
        }
    }

    private void FillTopBar()
    {
        var topBar = editorRootElement.Q<VisualElement>(className: "topBar");

        layersDropdown = new PopupField<VisualElement>(
            choices: visualElementsForLayers,
            defaultIndex: selectedLayer,
            formatSelectedValueCallback: ve => ve.Q<Label>().text,
            formatListItemCallback: ve => ve.Q<Label>().text
        );

        layersDropdown.RegisterValueChangedCallback(LayerDropdownChanged);

        topBar.Q("Layer Dropdown Placeholder").RemoveFromHierarchy();

        topBar.Insert(0, layersDropdown);
        topBar.Q<Button>("Add Layer").clickable = new Clickable(AddLayer);
        topBar.Q<Button>("Remove Layer").clickable = new Clickable(RemoveLayer);

        layerNameField = topBar.Q<TextField>("Layer Name");
    }

    private void RemoveLayer()
    {
        layersContainer.Remove(visualElementsForLayers[selectedLayer]);
        visualElementsForLayers.RemoveAt(selectedLayer);
        layers.DeleteArrayElementAtIndex(selectedLayer);
        selectedLayer--;
        layersDropdown.index = selectedLayer;
    }

    private void LayerDropdownChanged(ChangeEvent<VisualElement> evt)
    {
        SetSelectedLayer(visualElementsForLayers.IndexOf(evt.newValue));
    }

    private void SetSelectedLayer(int index)
    {
        if (visualElementsForLayers.Count == 0)
        {
            layerNameField.Unbind();
            selectedLayer = -1;
            return;
        }

        selectedLayer = Mathf.Clamp(index, 0, visualElementsForLayers.Count - 1);

        layersContainer.Clear();
        layersContainer.Add(visualElementsForLayers[selectedLayer]);

        layerNameField.BindProperty(layers.GetArrayElementAtIndex(index).FindPropertyRelative("name"));
    }

    private void AddLayer()
    {
        layers.arraySize++;
        InitializeNewLayer(layers.GetArrayElementAtIndex(layers.arraySize - 1));

        var newLayerVisualElement = CreateLayerVisualElement(layers.arraySize - 1);
        layersContainer.Add(newLayerVisualElement);
        visualElementsForLayers.Add(newLayerVisualElement);
        layersDropdown.value = newLayerVisualElement;
        serializedObject.ApplyModifiedProperties();

        SetSelectedLayer(layers.arraySize - 1);
    }

    private void InitializeNewLayer(SerializedProperty layerProp)
    {
        layerProp.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue = $"Layer {layers.arraySize}";
    }

    private VisualElement CreateLayerVisualElement(int i)
    {
        var layerElement = layerTemplate.CloneTree();
        layerElement.Q<Label>("Layer Label").text = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;

        return layerElement;
    }
}
}