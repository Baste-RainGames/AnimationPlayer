using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Animation_Player {
[CustomEditor(typeof(AnimationPlayer))]
public class NewAnimationPlayerEditor : Editor {
    private const string rootUXMLPath = "Packages/com.baste.animationplayer/Editor/UXML/AnimationPlayer.uxml";
    private const string layerUXMLPath = "Packages/com.baste.animationplayer/Editor/UXML/AnimationLayer.uxml";
    private const string singleClipPath = "Packages/com.baste.animationplayer/Editor/UXML/SingleClipState.uxml";

    private SerializedProperty layers;
    private VisualElement layersContainer;
    private List<VisualElement> visualElementsForLayers;
    private PopupField<VisualElement> layersDropdown;
    private VisualTreeAsset layerTemplate;
    private VisualTreeAsset singleClipTemplate;
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
        editorRootElement  = CloneVisualTreeAsset(rootUXMLPath).CloneTree();
        layerTemplate      = CloneVisualTreeAsset(layerUXMLPath);
        singleClipTemplate = CloneVisualTreeAsset(singleClipPath);

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
        var topBar = editorRootElement.Q<VisualElement>(className: "topBar");

        layersDropdown = new PopupField<VisualElement>(
            choices: visualElementsForLayers,
            defaultIndex: selectedLayer,
            formatSelectedValueCallback: GetDropdownName,
            formatListItemCallback:      GetDropdownName
        );

        layersDropdown.AddToClassList("topBar__element");
        layersDropdown.RegisterValueChangedCallback(LayerDropdownChanged);

        topBar.Q("Layer Dropdown Placeholder").RemoveFromHierarchy();
        topBar.Insert(0, layersDropdown);
        topBar.Q<Button>("Add Layer")   .clickable = new Clickable(AddLayer);
        topBar.Q<Button>("Remove Layer").clickable = new Clickable(RemoveLayer);

        string GetDropdownName(VisualElement ve)
        {
            var attachedProp = ((SerializedProperty) ve.userData);
            return attachedProp.FindPropertyRelative("name").stringValue;
        }
    }

    private void RemoveLayer()
    {
        visualElementsForLayers.RemoveAt(selectedLayer);
        layers.DeleteArrayElementAtIndex(selectedLayer);
        serializedObject.ApplyModifiedProperties();

        for (int i = 0; i < visualElementsForLayers.Count; i++)
        {
            // the serialized property just has the path, so they need to also be shifted back to not point at the wrong indices
            visualElementsForLayers[i].userData = layers.GetArrayElementAtIndex(i);
        }

        SetSelectedLayer(selectedLayer);
    }

    private void LayerDropdownChanged(ChangeEvent<VisualElement> evt)
    {
        SetSelectedLayer(visualElementsForLayers.IndexOf(evt.newValue));
    }

    private void SetSelectedLayer(int index)
    {
        if (visualElementsForLayers.Count == 0)
        {
            selectedLayer = -1;
            return;
        }

        selectedLayer = Mathf.Clamp(index, 0, visualElementsForLayers.Count - 1);

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

        layerElement.Q<Button>("Add State").clickable = new Clickable(evt => AddStateClicked(layerElement));

        var statesParent = layerElement.Q<VisualElement>("States Parent");

        var singleClips = layerProp.FindPropertyRelative("serializedSingleClipStates");
        var anyStates = singleClips.arraySize > 0;


        for (int i = 0; i < singleClips.arraySize; i++)
            statesParent.Add(CreateSingleClipStateVisualElement(singleClips.GetArrayElementAtIndex(i)));

        if (anyStates)
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

    private void AddSharedPropertiesForAnimationStates(SerializedProperty stateProp, VisualElement stateElement)
    {
        stateElement.Q<TextField>("State Name").BindProperty(stateProp.FindPropertyRelative("name"));
        stateElement.Q<DoubleField>("State Speed").BindProperty(stateProp.FindPropertyRelative("speed"));
    }

    // The layer element is passed instead of the user data, as the user data is the layer prop that gets regenerated as layers are deleted
    private void AddStateClicked(TemplateContainer layerElement)
    {
        var gm = new GenericMenu();
        gm.allowDuplicateNames = true;
        foreach (var stateType in allStateTypes)
            gm.AddItem(new GUIContent(stateType.Name), false, () => StateSelected((SerializedProperty) layerElement.userData, stateType));
        gm.ShowAsContext();
    }

    private void StateSelected(SerializedProperty layer, Type stateType)
    {
        SerializedProperty stateProp;
        var serializedOrder = AppendArray(layer, "serializedStateOrder");

        if (stateType == typeof(SingleClip))
        {
            stateProp = AppendArray(layer, "serializedSingleClipStates");
        }
        else
        {
            Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
            return;
        }

        HandleSharedProps();
        HandleGUIDOfNewState();

        layer.serializedObject.ApplyModifiedProperties();

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