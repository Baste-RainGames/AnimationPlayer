using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Animation_Player
{
// [CustomEditor(typeof(AnimationPlayer))]
public class NewAnimationPlayerEditor : Editor
{
    private const string rootUXMLPath = "Packages/com.baste.animationplayer/Editor/UXML/AnimationPlayer.uxml";
    private const string layerUXMLPath = "Packages/com.baste.animationplayer/Editor/UXML/AnimationLayer.uxml";
    private const string singleClipPath = "Packages/com.baste.animationplayer/Editor/UXML/SingleClipState.uxml";
    private const string blendTree1DPath = "Packages/com.baste.animationplayer/Editor/UXML/BlendTree1D.uxml";

    private SerializedProperty layers;
    private VisualElement layersContainer;
    private VisualElement topBar;
    private List<VisualElement> visualElementsForLayers;
    private PopupField<VisualElement> layersDropdown;
    private VisualTreeAsset layerTemplate;
    private VisualTreeAsset singleClipTemplate;
    private VisualTreeAsset blendTree1DTemplate;
    private VisualElement editorRootElement;
    private VisualElement rootHackForUndo;
    private int selectedLayer;
    private List<Type> allStateTypes;

    private void OnEnable()
    {
        layers = serializedObject.FindProperty(nameof(AnimationPlayer.layers));

        var directReference = (AnimationPlayer) target;
        if (directReference.layers == null)
            InitializeNewAnimationPlayer();

        Undo.undoRedoPerformed += OnUndo;

        allStateTypes = new List<Type>
        {
            typeof(SingleClip),
            typeof(BlendTree1D),
            typeof(BlendTree2D),
            typeof(Sequence),
            typeof(PlayRandomClip)
        };
    }

    private void InitializeNewAnimationPlayer()
    {
        layers.arraySize = 1;
        var baseLayer = layers.GetArrayElementAtIndex(0);
        baseLayer.FindPropertyRelative("name").stringValue = "Base Layer";
        baseLayer.FindPropertyRelative("startWeight").floatValue = 1f;
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
        editorRootElement = CloneVisualTreeAsset(rootUXMLPath).CloneTree();
        layerTemplate = CloneVisualTreeAsset(layerUXMLPath);
        singleClipTemplate = CloneVisualTreeAsset(singleClipPath);
        blendTree1DTemplate = CloneVisualTreeAsset(blendTree1DPath);

        serializedObject.Update();

        CreateLayers();
        FillTopBar();

        SetSelectedLayer(selectedLayer);

        container.Add(editorRootElement);
    }

    private static VisualTreeAsset CloneVisualTreeAsset(string assetPath)
    {
        var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
        Assert.IsNotNull(template, $"no uxml asset at path {assetPath}");
        return template;
    }

    private void CreateLayers()
    {
        layersContainer = editorRootElement.Q<VisualElement>("Layers Container");
        visualElementsForLayers = new List<VisualElement>();
        for (int i = 0; i < layers.arraySize; i++)
        {
            var layerElement = CreateLayerVisualElement(i);
            visualElementsForLayers.Add(layerElement);
        }
    }

    private void FillTopBar()
    {
        topBar = editorRootElement.Q<VisualElement>(className: "topBar");

        layersDropdown = new PopupField<VisualElement>(
            choices: visualElementsForLayers,
            defaultIndex: selectedLayer,
            formatSelectedValueCallback: GetDropdownName,
            formatListItemCallback: GetDropdownName
        );

        layersDropdown.AddToClassList("topBar__element");
        layersDropdown.RegisterValueChangedCallback(LayerDropdownChanged);

        topBar.Q("Layer Dropdown Placeholder").RemoveFromHierarchy();
        topBar.Insert(0, layersDropdown);
        topBar.Q<Button>("Add Layer").clickable = new Clickable(AddLayer);

        var removeLayerButton = topBar.Q<Button>("Remove Layer");
        removeLayerButton.clickable = new Clickable(RemoveLayer);
        removeLayerButton.visible = layers.arraySize > 1;

        string GetDropdownName(VisualElement ve)
        {
            var attachedProp = ((SerializedProperty) ve.userData);
            return attachedProp.FindPropertyRelative("name").stringValue;
        }
    }

    private void RemoveLayer()
    {
        if (layers.arraySize == 1)
            return;

        visualElementsForLayers.RemoveAt(selectedLayer);
        layers.DeleteArrayElementAtIndex(selectedLayer);
        serializedObject.ApplyModifiedProperties();

        for (int i = 0; i < visualElementsForLayers.Count; i++)
        {
            // the serialized property just has the path, so they need to also be shifted back to not point at the wrong indices
            visualElementsForLayers[i].userData = layers.GetArrayElementAtIndex(i);
        }

        topBar.Q<Button>("Remove Layer").visible = layers.arraySize > 1;
        SetSelectedLayer(selectedLayer);
    }

    private void SetSelectedLayer(int newLayer)
    {
        if (visualElementsForLayers.Count == 0)
        {
            selectedLayer = -1;
            return;
        }

        newLayer = Mathf.Clamp(newLayer, 0, visualElementsForLayers.Count - 1);
        // Pass this through the dropdown to sync the dropdown display
        if (newLayer != layersDropdown.index)
            layersDropdown.index = newLayer;
        else
            LayerDropdownChanged(ChangeEvent<VisualElement>.GetPooled(visualElementsForLayers[selectedLayer], visualElementsForLayers[newLayer]));
    }

    private void LayerDropdownChanged(ChangeEvent<VisualElement> evt)
    {
        selectedLayer = visualElementsForLayers.IndexOf(evt.newValue);
        layersContainer.Clear();
        layersContainer.Add(visualElementsForLayers[selectedLayer]);
    }

    private void AddLayer()
    {
        layers.arraySize++;
        InitializeNewLayer(layers.GetArrayElementAtIndex(layers.arraySize - 1));

        var newLayerVisualElement = CreateLayerVisualElement(layers.arraySize - 1);
        layersContainer.Add(newLayerVisualElement);
        visualElementsForLayers.Add(newLayerVisualElement);
        serializedObject.ApplyModifiedProperties();

        topBar.Q<Button>("Remove Layer").visible = layers.arraySize > 1;
        SetSelectedLayer(layers.arraySize - 1);
    }

    private void InitializeNewLayer(SerializedProperty layerProp)
    {
        layerProp.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue = $"Layer {layers.arraySize}";
        layerProp.FindPropertyRelative(nameof(AnimationLayer.startWeight)).floatValue = 1f;
    }

    private VisualElement CreateLayerVisualElement(int layerIndex)
    {
        var layerElement = layerTemplate.CloneTree();
        var layerProp = layers.GetArrayElementAtIndex(layerIndex);

        layerElement.userData = layerProp;

        layerElement.Q<ObjectField>("Avatar Mask").objectType = typeof(AvatarMask);

        Bind<TextField>("Layer Name", nameof(AnimationLayer.name));
        Bind<Slider>("Layer Weight", nameof(AnimationLayer.startWeight));
        Bind<ObjectField>("Avatar Mask", nameof(AnimationLayer.mask));
        Bind<EnumField>("Layer Type", nameof(AnimationLayer.type));

        layerElement.Q<Button>("Add State").clickable = new Clickable(AddStateClicked);

        var statesParent = layerElement.Q<VisualElement>("States Parent");
        var singleClipsParent = statesParent.Q<VisualElement>("Single Clip States");
        var blendTrees1DParent = statesParent.Q<VisualElement>("1D Blend Trees");

        var singleClips = layerProp.FindPropertyRelative("serializedSingleClipStates");
        var blendTrees1D = layerProp.FindPropertyRelative("serializedBlendTree1Ds");

        singleClipsParent.style.display = singleClips.arraySize > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        for (int i = 0; i < singleClips.arraySize; i++)
            singleClipsParent.Add(CreateSingleClipStateVisualElement(singleClips.GetArrayElementAtIndex(i)));

        blendTrees1DParent.style.display = blendTrees1D.arraySize > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        for (int i = 0; i < blendTrees1D.arraySize; i++)
            blendTrees1DParent.Add(CreateBlendTree1DVisualElement(blendTrees1D.GetArrayElementAtIndex(i)));

        if (singleClips.arraySize + blendTrees1D.arraySize > 0)
            statesParent.Q<Label>("No States").style.display = DisplayStyle.None;

        return layerElement;

        void Bind<T>(string elementName, string propName) where T : VisualElement, IBindable
        {
            layerElement.Q<T>(elementName).BindProperty(layerProp.FindPropertyRelative(propName));
        }
    }

    private VisualElement CreateSingleClipStateVisualElement(SerializedProperty singleClipStateProp)
    {
        var singleClipElement = singleClipTemplate.CloneTree();
        AddSharedPropertiesForAnimationStates(singleClipStateProp, singleClipElement);

        var clipField = singleClipElement.Q<ObjectField>("Clip");
        clipField.objectType = typeof(AnimationClip);
        clipField.BindProperty(singleClipStateProp.FindPropertyRelative("clip"));

        return singleClipElement;
    }

    private VisualElement CreateBlendTree1DVisualElement(SerializedProperty blendTree1DProp)
    {
        var blendTreeElement = blendTree1DTemplate.CloneTree();

        AddSharedPropertiesForAnimationStates(blendTree1DProp, blendTreeElement);

        var blendVariableField = blendTreeElement.Q<TextField>("Blend Variable");
        blendVariableField.BindProperty(blendTree1DProp.FindPropertyRelative("blendVariable"));

        var entriesParent = blendTreeElement.Q<VisualElement>("Entries");


        var addEntryButton = entriesParent.Q<Button>("Add Entry");
        addEntryButton.clickable = new Clickable(AddBlendTreeEntry);

        return blendTreeElement;

        void AddBlendTreeEntry()
        {
            var layer = layers.GetArrayElementAtIndex(selectedLayer);
            var blendTreeIndex = blendTreeElement.parent.IndexOf(blendTreeElement) - 1; //there's a label as the first element
            var blendTree1D = layer.FindPropertyRelative("serializedBlendTree1Ds").GetArrayElementAtIndex(blendTreeIndex);
            var entries = blendTree1D.FindPropertyRelative("entries");
            entries.arraySize++;

            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("clip").objectReferenceValue = null; // auto-inits to value of prev element, don't want that!
            entry.FindPropertyRelative("threshold").floatValue += 1f; //default to 0 (if first), or last entry + 1

            entry.serializedObject.ApplyModifiedProperties();
        }
    }

    private void AddSharedPropertiesForAnimationStates(SerializedProperty stateProp, VisualElement stateElement)
    {
        stateElement.Q<TextField>("State Name").BindProperty(stateProp.FindPropertyRelative("name"));
        stateElement.Q<DoubleField>("State Speed").BindProperty(stateProp.FindPropertyRelative("speed"));
    }

    private void AddStateClicked()
    {
        var gm = new GenericMenu();
        foreach (var stateType in allStateTypes)
            gm.AddItem(new GUIContent(stateType.Name), false, () => StateToAddSelected(stateType));
        gm.ShowAsContext();
    }

    private void StateToAddSelected(Type stateType)
    {
        var layer = layers.GetArrayElementAtIndex(selectedLayer);

        SerializedProperty stateProp;
        var serializedOrder = AppendArray(layer, "serializedStateOrder");

        if (stateType == typeof(SingleClip))
        {
            stateProp = AppendArray(layer, "serializedSingleClipStates");
        }
        else if (stateType == typeof(BlendTree1D))
        {
            stateProp = AppendArray(layer, "serializedBlendTree1Ds");
            stateProp.FindPropertyRelative("blendVariable").stringValue = "blend";
            stateProp.FindPropertyRelative("compensateForDifferentDurations").boolValue = true;
        }
        else
        {
            Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
            return;
        }

        HandleSharedProps();
        HandleGUIDOfNewState();

        layer.serializedObject.ApplyModifiedProperties();

        visualElementsForLayers[selectedLayer] = CreateLayerVisualElement(selectedLayer);
        layersContainer.Clear();
        layersContainer.Add(visualElementsForLayers[selectedLayer]);

        SerializedProperty AppendArray(SerializedProperty prop, string arrayPropName)
        {
            var arrayProp = prop.FindPropertyRelative(arrayPropName);
            arrayProp.arraySize++;
            return arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
        }

        void HandleSharedProps()
        {
            stateProp.FindPropertyRelative("speed").doubleValue = 1d;
            stateProp.FindPropertyRelative("name").stringValue = $"New {ObjectNames.NicifyVariableName(stateType.Name)}";
        }

        void HandleGUIDOfNewState()
        {
            var guid = SerializedGUID.Create().GUID.ToString();
            stateProp.FindPropertyRelative("guid").FindPropertyRelative("guidSerialized").stringValue = guid;
            serializedOrder.FindPropertyRelative("guidSerialized").stringValue = guid;
        }
    }
}
}