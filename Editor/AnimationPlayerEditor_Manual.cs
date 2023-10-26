using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player {
// [CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor_Manual : Editor {
    private UIRoot uiRoot;
    private AnimationPlayerPreviewer previewer;

    public override bool RequiresConstantRepaint() => previewer.IsPreviewing;

    private void OnEnable()
    {
        var directReference = (AnimationPlayer) target;
        previewer = new AnimationPlayerPreviewer(directReference);
        if (directReference.layers == null)
            InitializeJustAddedAnimationPlayer();

        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
        previewer.StopPreview();
    }

    private void HandleUndoRedo() => RebuildUI();

    private void InitializeJustAddedAnimationPlayer()
    {
        var layers = serializedObject.FindProperty(nameof(AnimationPlayer.layers));
        layers.arraySize = 1;

        var baseLayer = layers.GetArrayElementAtIndex(0);
        baseLayer.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue = "Base Layer";
        baseLayer.FindPropertyRelative(nameof(AnimationLayer.startWeight)).floatValue = 1f;

        var defaultTransition = serializedObject.FindProperty(nameof(AnimationPlayer.defaultTransition));
        defaultTransition.FindPropertyRelative(nameof(TransitionData.duration)).floatValue = .1f;

        serializedObject.ApplyModifiedProperties();
    }

    private void RebuildUI()
    {
        if (uiRoot == null)
            return;

        serializedObject.Update();

        var parentElement = uiRoot.rootVisualElement.parent;
        var index = parentElement.IndexOf(uiRoot.rootVisualElement);
        parentElement.RemoveAt(index);

        var selectedLayer = uiRoot.selectedLayer;
        uiRoot = new UIRoot(this);
        uiRoot.Init();
        uiRoot.topBar.layerDropdown.SelectLayer(Mathf.Min(selectedLayer, uiRoot.layersContainer.NumLayers - 1));

        parentElement.Insert(index, uiRoot.rootVisualElement);
    }

    public override VisualElement CreateInspectorGUI()
    {
        uiRoot = new UIRoot(this);
        uiRoot.Init();
        return uiRoot.rootVisualElement;
    }

    public abstract class AnimationPlayerUINode {
        protected readonly AnimationPlayerEditor_Manual editor;
        public VisualElement rootVisualElement;

        protected SerializedObject serializedObject => editor.serializedObject;
        protected UIRoot root => editor.uiRoot;

        protected AnimationPlayerUINode(AnimationPlayerEditor_Manual editor)
        {
            this.editor = editor;
        }
    }

    public class UIRoot : AnimationPlayerUINode {
        public TopBar topBar;
        public LayersContainer layersContainer;
        public int selectedLayer;

        public UIRoot(AnimationPlayerEditor_Manual editor) : base(editor) { }

        public void Init()
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.baste.animationplayer/Editor/USS/AnimationPlayer.uss"));

            topBar = new TopBar(editor);
            layersContainer = new LayersContainer(editor);

            rootVisualElement.Add(topBar.rootVisualElement);
            rootVisualElement.Add(layersContainer.rootVisualElement);

            topBar.layerDropdown.LayersReady();
            topBar.layerDropdown.SelectLayer(selectedLayer);

            if (selectedLayer == 0)
                layersContainer.SetLayerVisible(selectedLayer);
        }
    }

    public class TopBar : AnimationPlayerUINode {
        public readonly LayerDropdown layerDropdown;
        public readonly RemoveLayerButton removeLayerButton;

        public TopBar(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("topBar");

            layerDropdown      = new LayerDropdown(editor);
            var addLayerButton = new AddLayerButton(editor);
            removeLayerButton  = new RemoveLayerButton(editor);

            rootVisualElement.Add(layerDropdown.rootVisualElement);
            rootVisualElement.Add(addLayerButton.rootVisualElement);
            rootVisualElement.Add(removeLayerButton.rootVisualElement);
        }
    }

    public class LayerDropdown : AnimationPlayerUINode {
        private PopupField<LayerUI> popupField;

        public LayerDropdown(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            // placeholder for the popup field.
            rootVisualElement = new VisualElement();
        }

        public void LayersReady()
        {
            var old = rootVisualElement;

            rootVisualElement = popupField = new PopupField<LayerUI>(
                root.layersContainer.layers,
                0,
                LayerUI.GetNameOf,
                LayerUI.GetNameOf
            );
            popupField.AddToClassList("topBar__element");
            popupField.RegisterValueChangedCallback(LayerChanged);

            old.parent.Replace(old, rootVisualElement);
        }

        private void LayerChanged(ChangeEvent<LayerUI> evt)
        {
            var index = root.layersContainer.layers.IndexOf(evt.newValue);
            root.selectedLayer = index;
            root.layersContainer.SetLayerVisible(index);
        }

        public void SelectLayer(int newLayer) => popupField.index = newLayer;
    }

    public class RemoveLayerButton : AnimationPlayerUINode {
        public RemoveLayerButton(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            rootVisualElement = new Button
            {
                text = "Remove Layer",
                clickable = new Clickable(RemoveLayerClicked),
            };
            rootVisualElement.AddToClassList("topBar__element");
            rootVisualElement.SetEnabled(editor.serializedObject.FindProperty("layers").arraySize > 1);
        }

        private void RemoveLayerClicked()
        {
            if (root.layersContainer.NumLayers == 1)
                return;
            root.layersContainer.RemoveLayer(root.selectedLayer);
            editor.RebuildUI();
        }
    }

    public class AddLayerButton : AnimationPlayerUINode {
        public AddLayerButton(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            rootVisualElement = new Button
            {
                text = "Add Layer",
                clickable = new Clickable(AddLayerClicked)
            };
            rootVisualElement.AddToClassList("topBar__element");
        }

        private void AddLayerClicked()
        {
            root.layersContainer.AddNewLayer();
            root.topBar.layerDropdown.SelectLayer(root.layersContainer.NumLayers - 1);

            editor.uiRoot.topBar.removeLayerButton.rootVisualElement.SetEnabled(editor.serializedObject.FindProperty("layers").arraySize > 1);
        }
    }

    public class LayersContainer : AnimationPlayerUINode {
        public readonly List<LayerUI> layers;
        private SerializedProperty layersProp;

        public LayersContainer(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            rootVisualElement = new VisualElement();
            layers = new List<LayerUI>();

            layersProp = serializedObject.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
                CreateLayerUI(editor, i);
        }

        public int NumLayers => layersProp.arraySize;
        public LayerUI SelectedLayer => layers[root.selectedLayer];

        private void CreateLayerUI(AnimationPlayerEditor_Manual editor, int layerIndex)
        {
            var layer = new LayerUI(editor, layerIndex);
            layers.Add(layer);
            rootVisualElement.Add(layer.rootVisualElement);
        }

        public void SetLayerVisible(int index)
        {
            for (int i = 0; i < layers.Count; i++)
                layers[i].rootVisualElement.SetDisplayed(index == i);
        }

        public void AddNewLayer()
        {
            var newLayer = layersProp.AppendToArray();

            string layerName;
            var index = layersProp.arraySize;
            do
            {
                layerName = "Layer " + index;
                index++;
            } while (SomeLayerHasName(layerName));

            newLayer.FindPropertyRelative("serializedSingleClipStates").ClearArray();
            newLayer.FindPropertyRelative("serializedBlendTree1Ds").ClearArray();
            newLayer.FindPropertyRelative("serializedBlendTree2Ds").ClearArray();
            newLayer.FindPropertyRelative("serializedSelectRandomStates").ClearArray();
            newLayer.FindPropertyRelative("serializedSequences").ClearArray();
            newLayer.FindPropertyRelative(nameof(AnimationLayer.transitions)).ClearArray();
            newLayer.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue = layerName;
            newLayer.FindPropertyRelative(nameof(AnimationLayer.startWeight)).floatValue = 1f;
            newLayer.FindPropertyRelative(nameof(AnimationLayer.type)).enumValueIndex = (int) AnimationLayerType.Override;

            serializedObject.ApplyModifiedProperties();
            CreateLayerUI(editor, layersProp.arraySize - 1);

            bool SomeLayerHasName(string name)
            {
                foreach (var layerProp in layersProp.IterateArray())
                    if (layerProp.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue == name)
                        return true;

                return false;
            }
        }

        public void RemoveLayer(int layer)
        {
            layersProp.DeleteArrayElementAtIndex(layer);
            serializedObject.ApplyModifiedProperties();
        }
    }

    // @TODO: we're making one LayerUI for each layer, instead of having one that we fill with data.
    // Same with the other sections.
    // Re-populating everything with the correct data is a better approach in general
    // Easier to handle, less data to deal with.
    public class LayerUI : AnimationPlayerUINode {
        public readonly SerializedProperty serializedProperty;
        public readonly EditStatesSection editStatesSection;
        private readonly bool isBaseLayer;

        public LayerUI(AnimationPlayerEditor_Manual editor, int layerIndex) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer");

            isBaseLayer = layerIndex == 0;

            serializedProperty = serializedObject.FindProperty(nameof(AnimationPlayer.layers)).GetArrayElementAtIndex(layerIndex);

            var editLayerSection = new EditLayerSection(editor, layerIndex, serializedProperty);
            var addAndSearchStatesSection = new AddAndSearchStatesSection(editor);
            editStatesSection = new EditStatesSection(editor, layerIndex, serializedProperty);

            rootVisualElement.Add(editLayerSection.rootVisualElement);
            rootVisualElement.Add(addAndSearchStatesSection.rootVisualElement);
            rootVisualElement.Add(editStatesSection.rootVisualElement);
        }

        public static string GetNameOf(LayerUI layer) => layer.GetName();
        private string GetName()
        {
            if (isBaseLayer)
                return "Base Layer";
            return serializedProperty.FindPropertyRelative("name").stringValue;
        }
    }

    public class EditLayerSection : AnimationPlayerUINode {
        public EditLayerSection(AnimationPlayerEditor_Manual editor, int layerIndex, SerializedProperty layerProp) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer__subSection");

            if (layerIndex == 0)
            {
                var baseLayerLabel = new Label("Base Layer");
                baseLayerLabel.AddToClassList("animationLayer__baseLayerLabel");
                rootVisualElement.Add(baseLayerLabel);
            }
            else
            {
                var layerName = new LayerName(editor, layerProp);
                rootVisualElement.Add(layerName.rootVisualElement);

                var layerType = new LayerType(editor, layerProp);
                var layerStartWeight = new LayerStartWeight(editor, layerProp);
                var avatarMask = new AvatarMaskNode(editor, layerProp);

                rootVisualElement.Add(layerType.rootVisualElement);
                rootVisualElement.Add(layerStartWeight.rootVisualElement);
                rootVisualElement.Add(avatarMask.rootVisualElement);
            }
        }
    }

    public class LayerName : AnimationPlayerUINode {
        private TextField textField;

        public LayerName(AnimationPlayerEditor_Manual editor, SerializedProperty layerProp) :
            base(editor)
        {
            rootVisualElement = textField = new TextField("Layer Name");
            Rebind(layerProp);
        }

        public void Rebind(SerializedProperty newLayerProp)
        {
            textField.BindProperty(newLayerProp.FindPropertyRelative(nameof(AnimationLayer.name)));
        }
    }

    public class LayerType : AnimationPlayerUINode {
        public LayerType(AnimationPlayerEditor_Manual editor, SerializedProperty layerProp) : base(editor)
        {
            var enumField = new EnumField("Layer Type");
            enumField.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.type)));
            rootVisualElement = enumField;
        }
    }

    public class LayerStartWeight : AnimationPlayerUINode {
        public LayerStartWeight(AnimationPlayerEditor_Manual editor, SerializedProperty layerProp) : base(editor)
        {
            var slider = new Slider("Layer Start Weight", 0f, 1f);
            slider.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.startWeight)));
            rootVisualElement = slider;
        }
    }

    public class AvatarMaskNode : AnimationPlayerUINode {
        public AvatarMaskNode(AnimationPlayerEditor_Manual editor, SerializedProperty layerProp) : base(editor)
        {
            var objectField = new ObjectField("Avatar Mask");
            objectField.objectType = typeof(AvatarMask);
            objectField.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.mask)));

            rootVisualElement = objectField;
        }
    }

    public class AddAndSearchStatesSection : AnimationPlayerUINode {
        public AddAndSearchStatesSection(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer__subSection");
            rootVisualElement.AddToClassList("animationLayer__statesHeader");

            var statesLabel = new Label("States");
            statesLabel.AddToClassList("label");

            var statesSearch = new ToolbarSearchField();
            statesSearch.AddToClassList("animationLayer__statesHeader__search");
            statesSearch.Q("unity-text-input").AddToClassList("animationLayer__statesHeader__search__textInput");

            statesSearch.RegisterValueChangedCallback(SearchUpdated);
            var addStateButton = new AddStateButton(editor);

            rootVisualElement.Add(statesLabel);
            rootVisualElement.Add(statesSearch);
            rootVisualElement.Add(addStateButton.rootVisualElement);
        }

        private void SearchUpdated(ChangeEvent<string> evt)
        {
            root.layersContainer.SelectedLayer.editStatesSection.SetSearchTerm(evt.newValue);
        }
    }

    public class AddStateButton : AnimationPlayerUINode {
        private static List<Type> allStateTypes = new List<Type>
        {
            typeof(SingleClip),
            typeof(BlendTree1D),
            typeof(BlendTree2D),
            typeof(Sequence),
            typeof(PlayRandomClip)
        };

        public AddStateButton(AnimationPlayerEditor_Manual editor) : base(editor)
        {
            Button button;
            rootVisualElement = button = new Button();

            button.clickable = new Clickable(AddStateClicked);
            button.text = "Add State";
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
            var layerProp = root.layersContainer.layers[root.selectedLayer].serializedProperty;

            SerializedProperty stateProp;
            var serializedOrder = AppendToArray("serializedStateOrder");

            if (stateType == typeof(SingleClip))
            {
                stateProp = AppendToArray("serializedSingleClipStates");
            }
            else if (stateType == typeof(BlendTree1D))
            {
                stateProp = AppendToArray("serializedBlendTree1Ds");
                stateProp.FindPropertyRelative("blendVariable").stringValue = "blend";
                stateProp.FindPropertyRelative("compensateForDifferentDurations").boolValue = true;
            }
            else if (stateType == typeof(BlendTree2D))
            {
                stateProp = AppendToArray("serializedBlendTree2Ds");
                stateProp.FindPropertyRelative("blendVariable").stringValue = "blend";
                stateProp.FindPropertyRelative("blendVariable2").stringValue = "blend2";
            }
            else if (stateType == typeof(PlayRandomClip))
            {
                stateProp = AppendToArray("serializedSelectRandomStates");
            }
            else if (stateType == typeof(Sequence))
            {
                stateProp = AppendToArray("serializedSequences");
            }
            else
            {
                Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
                return;
            }

            HandleSharedProps();
            // HandleGUIDOfNewState();

            layerProp.serializedObject.ApplyModifiedProperties();

            root.layersContainer.layers[root.selectedLayer].editStatesSection.OnStateAdded(stateType, root.selectedLayer);

            SerializedProperty AppendToArray(string arrayPropName)
            {
                var arrayProp = layerProp.FindPropertyRelative(arrayPropName);
                if (arrayProp == null)
                {
                    Debug.LogError($"There's no property named {arrayPropName} in {layerProp.type}");
                    return null;
                }
                return arrayProp.AppendToArray();
            }

            void HandleSharedProps()
            {
                stateProp.FindPropertyRelative("speed").doubleValue = 1d;
                stateProp.FindPropertyRelative("name").stringValue = $"New {ObjectNames.NicifyVariableName(stateType.Name)}";
            }

            // void HandleGUIDOfNewState()
            // {
                // var guid = SerializedGUID.Create().GUID.ToString();
                // stateProp.FindPropertyRelative("guid").FindPropertyRelative("guidSerialized").stringValue = guid;
                // serializedOrder.FindPropertyRelative("guidSerialized").stringValue = guid;
            // }
        }
    }

    public class EditStatesSection : AnimationPlayerUINode {
        private readonly NoStatesLabel noStatesLabel;
        public readonly SingleClipSection singleClipSection;
        public readonly BlendTree1DSection blendTree1DSection;
        public readonly BlendTree2DSection blendTree2DSection;
        public readonly SequenceSection sequenceSection;
        public readonly RandomClipSection randomClipSection;

        public EditStatesSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer__subSection");
            rootVisualElement.AddToClassList("animationLayer__editStatesSection");

            singleClipSection  = new SingleClipSection (editor, currentSelectedLayer, layerProperty);
            blendTree1DSection = new BlendTree1DSection(editor, currentSelectedLayer, layerProperty);
            blendTree2DSection = new BlendTree2DSection(editor, currentSelectedLayer, layerProperty);
            sequenceSection    = new SequenceSection   (editor, currentSelectedLayer, layerProperty);
            randomClipSection  = new RandomClipSection (editor, currentSelectedLayer, layerProperty);
            noStatesLabel      = new NoStatesLabel     (editor, layerProperty, this);

            rootVisualElement.Add(noStatesLabel.rootVisualElement);
            rootVisualElement.Add(singleClipSection.rootVisualElement);
            rootVisualElement.Add(blendTree1DSection.rootVisualElement);
            rootVisualElement.Add(blendTree2DSection.rootVisualElement);
            rootVisualElement.Add(sequenceSection.rootVisualElement);
            rootVisualElement.Add(randomClipSection.rootVisualElement);
        }

        public void OnStateAdded(Type stateType, int layerIndex)
        {
            noStatesLabel.rootVisualElement.SetDisplayed(false);
            if (stateType == typeof(SingleClip))
                singleClipSection.OnStateAdded(layerIndex);
            else if (stateType == typeof(BlendTree1D))
                blendTree1DSection.OnStateAdded(layerIndex);
            else if (stateType == typeof(BlendTree2D))
                blendTree2DSection.OnStateAdded(layerIndex);
            else if (stateType == typeof(PlayRandomClip))
                randomClipSection.OnStateAdded(layerIndex);
            else if (stateType == typeof(Sequence))
                sequenceSection.OnStateAdded(layerIndex);
            else
                Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
        }

        public void SetSearchTerm(string searchTerm)
        {
            singleClipSection.SetSearchTerm(searchTerm);
            blendTree1DSection.SetSearchTerm(searchTerm);
            blendTree2DSection.SetSearchTerm(searchTerm);
            sequenceSection.SetSearchTerm(searchTerm);
            randomClipSection.SetSearchTerm(searchTerm);

            noStatesLabel.OnSearchTermUpdated();
        }
    }

    public class NoStatesLabel : AnimationPlayerUINode {
        private Label label;
        private EditStatesSection parent;
        private SerializedProperty layerProperty;

        public NoStatesLabel(AnimationPlayerEditor_Manual editor, SerializedProperty layerProperty, EditStatesSection parent) : base(editor)
        {
            this.parent = parent;
            this.layerProperty = layerProperty;
            rootVisualElement = label = new Label();

            OnSearchTermUpdated();
        }

        public void OnSearchTermUpdated()
        {
            var shouldBeHidden = parent.singleClipSection.rootVisualElement.IsDisplayed() ||
                                 parent.blendTree1DSection.rootVisualElement.IsDisplayed() ||
                                 parent.blendTree2DSection.rootVisualElement.IsDisplayed() ||
                                 parent.sequenceSection.rootVisualElement.IsDisplayed() ||
                                 parent.randomClipSection.rootVisualElement.IsDisplayed();

            label.AddToClassList("animationLayer__editStatesSection__stateSet");
            label.SetDisplayed(!shouldBeHidden);

            if (shouldBeHidden)
                return;

            var countStates = layerProperty.FindPropertyRelative("serializedSingleClipStates").arraySize +
                              layerProperty.FindPropertyRelative("serializedBlendTree1Ds").arraySize +
                              layerProperty.FindPropertyRelative("serializedBlendTree2Ds").arraySize +
                              layerProperty.FindPropertyRelative("serializedSelectRandomStates").arraySize +
                              layerProperty.FindPropertyRelative("serializedSequences").arraySize;

            label.text = countStates == 0 ? "No states in layer!" : "No states matches search!";
        }
    }

    public abstract class StateSection<TStateDisplay> : AnimationPlayerUINode where TStateDisplay : StateDisplay {
        private SerializedProperty stateListProp;
        private List<TStateDisplay> childDisplays = new List<TStateDisplay>();

        protected StateSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayerIndex, SerializedProperty layerProperty) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer__editStatesSection__stateSet");

            var label = new Label(LabelText);
            label.AddToClassList("animationLayer__editStatesSection__stateSet__header");
            rootVisualElement.Add(label);

            stateListProp = layerProperty.FindPropertyRelative(ListPropName);
            rootVisualElement.SetDisplayed(stateListProp.arraySize > 0);

            for (int i = 0; i < stateListProp.arraySize; i++)
            {
                var display = CreateDisplayForState(stateListProp, currentSelectedLayerIndex, i);
                childDisplays.Add(display);
                rootVisualElement.Add(display.rootVisualElement);
            }
        }

        protected abstract TStateDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex);

        protected abstract string LabelText { get; }
        protected abstract string ListPropName { get; }

        public void OnStateAdded(int layerIndex)
        {
            rootVisualElement.SetDisplayed(stateListProp.arraySize > 0);

            var display = CreateDisplayForState(stateListProp, layerIndex, stateListProp.arraySize - 1);
            childDisplays.Add(display);
            rootVisualElement.Add(display.rootVisualElement);
        }

        public void SetSearchTerm(string searchTerm)
        {
            var regex = new Regex(searchTerm);
            foreach (var stateDisplay in childDisplays)
            {
                var stateName = stateDisplay.stateProp.FindPropertyRelative("name").stringValue;
                stateDisplay.rootVisualElement.SetDisplayed(regex.IsMatch(stateName));
            }

            rootVisualElement.SetDisplayed(childDisplays.Any(cd => cd.rootVisualElement.IsDisplayed()));
        }
    }

    public class SingleClipSection : StateSection<SingleClipDisplay> {
        public SingleClipSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty)
            : base(editor, currentSelectedLayer, layerProperty) { }

        protected override SingleClipDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex) =>
            new SingleClipDisplay(editor, stateListProp, layerIndex, stateIndex, stateListProp.GetArrayElementAtIndex(stateIndex));

        protected override string LabelText { get; } = "Single Clips States";
        protected override string ListPropName { get; } = "serializedSingleClipStates";
    }

    public class BlendTree1DSection : StateSection<BlendTree1DDisplay> {
        public BlendTree1DSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty)
            : base(editor, currentSelectedLayer, layerProperty) { }

        protected override BlendTree1DDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex) =>
            new BlendTree1DDisplay(editor, stateListProp, layerIndex, stateIndex, stateListProp.GetArrayElementAtIndex(stateIndex));

        protected override string LabelText { get; } = "1D Blend Trees";
        protected override string ListPropName { get; } = "serializedBlendTree1Ds";
    }

    public class BlendTree2DSection : StateSection<BlendTree2DDisplay> {
        public BlendTree2DSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty)
            : base(editor, currentSelectedLayer, layerProperty) { }

        protected override BlendTree2DDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex) =>
            new BlendTree2DDisplay(editor, stateListProp, layerIndex, stateIndex, stateListProp.GetArrayElementAtIndex(stateIndex));

        protected override string LabelText { get; } = "2D Blend Trees";
        protected override string ListPropName { get; } = "serializedBlendTree2Ds";
    }

    public class SequenceSection : StateSection<SequenceDisplay> {
        public SequenceSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty)
            : base(editor, currentSelectedLayer, layerProperty) { }

        protected override SequenceDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex) =>
            new SequenceDisplay(editor, stateListProp, layerIndex, stateIndex, stateListProp.GetArrayElementAtIndex(stateIndex));

        protected override string LabelText { get; } = "Sequences";
        protected override string ListPropName { get; } = "serializedSequences";
    }

    public class RandomClipSection : StateSection<RandomClipDisplay> {
        public RandomClipSection(AnimationPlayerEditor_Manual editor, int currentSelectedLayer, SerializedProperty layerProperty)
            : base(editor, currentSelectedLayer, layerProperty) { }

        protected override RandomClipDisplay CreateDisplayForState(SerializedProperty stateListProp, int layerIndex, int stateIndex)
        {
            return new RandomClipDisplay(editor, stateListProp, layerIndex, stateIndex, stateListProp.GetArrayElementAtIndex(stateIndex));
        }

        protected override string LabelText { get; } = "Random Clip States";
        protected override string ListPropName { get; } = "serializedSelectRandomStates";
    }

    public abstract class StateDisplay : AnimationPlayerUINode {
        public readonly SerializedProperty stateProp;
        private readonly SerializedProperty stateListProp;
        private int layerIndex;
        private int stateIndex;
        private readonly VisualElement onlyVisibleWhenExpandedSection;
        private readonly Label toggleExpandedLabel;
        private Slider playbackSlider;
        private Button playPauseButton;

        protected StateDisplay(   AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp) :
            base(editor)
        {
            this.stateProp = stateProp;
            this.layerIndex = layerIndex;
            this.stateIndex = stateIndex;
            this.stateListProp = stateListProp;

            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("animationLayer__editStatesSection__stateSet__state");

            var alwaysVisibleSection = new VisualElement {name = "always visible"};
            onlyVisibleWhenExpandedSection = new VisualElement {name = "only visible when expanded"};
            var toggleExpandedButton = new VisualElement {name = "toggle expanded button"};
            toggleExpandedButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__toggleExpanded");

            toggleExpandedButton.AddManipulator(new Clickable(ToggleExpanded));
            toggleExpandedLabel = new Label("v");
            toggleExpandedButton.Add(toggleExpandedLabel);

            rootVisualElement.Add(alwaysVisibleSection);
            rootVisualElement.Add(onlyVisibleWhenExpandedSection);
            rootVisualElement.Add(toggleExpandedButton);

            FillAlwaysVisibleSection(alwaysVisibleSection);
            FillOnlyVisibleWhenExpandedSection(onlyVisibleWhenExpandedSection);
            CreatePreview(onlyVisibleWhenExpandedSection);

            onlyVisibleWhenExpandedSection.SetDisplayed(false);
        }

        private void CreatePreview(VisualElement parentSection)
        {
            parentSection.Add(new HorizontalDivider());

            var label = new Label("Preview:");
            label.AddToClassList("label");
            parentSection.Add(label);

            var playSection = new VisualElement();
            playSection.AddToClassList("animationLayer__editStatesSection__stateSet__state__preview__playSection");
            parentSection.Add(playSection);

            playPauseButton = new Button {text="play"};
            var stopButton = new Button {text="stop"};
            playbackSlider = new Slider(0f, 1f);

            playSection.Add(playPauseButton);
            playSection.Add(stopButton);
            playSection.Add(playbackSlider);

            playPauseButton.clickable = new Clickable(PlayPauseClicked);
            stopButton.clickable      = new Clickable(StopPreview);
        }

        private void PlayPauseClicked()
        {
            var previewer = editor.previewer;
            var isPlay = !previewer.IsPreviewing || !previewer.AutomaticPlayback;

            if (isPlay)
            {
                playPauseButton.text = "pause";
                if (!previewer.IsPreviewing)
                    previewer.StartPreview(layerIndex, stateIndex, true, playbackSlider);
                else
                    previewer.AutomaticPlayback = true;
            }
            else
            {
                playPauseButton.text = "play";
                previewer.AutomaticPlayback = false;
            }
        }

        private void StopPreview()
        {
            editor.previewer.StopPreview();
            playPauseButton.text = "play";
        }

        protected virtual void FillAlwaysVisibleSection(VisualElement section)
        {
            var nameSection = new VisualElement();
            nameSection.AddToClassList("animationLayer__editStatesSection__stateSet__state__nameSection");

            var deleteButton = new Button();
            deleteButton.text = "X";
            deleteButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__nameSection__deleteButton");

            deleteButton.clickable = new Clickable(DeleteState);

            var nameField = new TextField("State Name");
            nameField.BindProperty(stateProp.FindPropertyRelative("name"));

            nameSection.Add(nameField);
            nameSection.Add(deleteButton);

            section.Add(nameSection);
        }

        protected virtual void FillOnlyVisibleWhenExpandedSection(VisualElement section)
        {
            var speedField = new DoubleField("Playback Speed");
            speedField.tooltip = "Clips in the state will be played with this speed multiplier";
            speedField.BindProperty(stateProp.FindPropertyRelative(nameof(AnimationPlayerState.speed)));

            section.Add(speedField);
        }

        private void DeleteState()
        {
            stateListProp.DeleteArrayElementAtIndex(stateIndex);
            serializedObject.ApplyModifiedProperties();
            editor.RebuildUI();
        }

        private void ToggleExpanded()
        {
            onlyVisibleWhenExpandedSection.SetDisplayed(!onlyVisibleWhenExpandedSection.IsDisplayed());
            toggleExpandedLabel.text = onlyVisibleWhenExpandedSection.IsDisplayed() ? "^" : "v";
        }
    }

    public class SingleClipDisplay : StateDisplay {
        public SingleClipDisplay(AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp)
            : base(editor, stateListProp, layerIndex, stateIndex, stateProp) { }

        protected override void FillAlwaysVisibleSection(VisualElement section)
        {
            var clipField = new ObjectField("Clip")
            {
                objectType = typeof(AnimationClip)
            };
            clipField.BindProperty(stateProp.FindPropertyRelative(nameof(SingleClip.clip)));
            section.Add(clipField);
        }
    }

    public class BlendTree1DDisplay : StateDisplay {
        private SerializedProperty entriesProp;
        private List<BlendTree1DEntry> entryElements = new List<BlendTree1DEntry>();
        private SerializedProperty blendVariableProp;
        private VisualElement entrySection;

        public BlendTree1DDisplay(AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp)
            : base(editor, stateListProp, layerIndex, stateIndex, stateProp) { }

        protected override void FillAlwaysVisibleSection(VisualElement section)
        {
            base.FillAlwaysVisibleSection(section);

            blendVariableProp = stateProp.FindPropertyRelative(nameof(BlendTree1D.blendVariable));

            var blendVariableField = new TextField("Blend Variable");
            blendVariableField.BindProperty(blendVariableProp);
            blendVariableField.RegisterValueChangedCallback(BlendVariableChanged);

            section.Add(blendVariableField);
        }

        protected override void FillOnlyVisibleWhenExpandedSection(VisualElement section)
        {
            base.FillOnlyVisibleWhenExpandedSection(section);

            var compensateForDurationsField = new Toggle("Compensate For Different Durations");
            compensateForDurationsField.tooltip = "Should the blend tree compensate if the clips in the tree has different durations? This causes clips that " +
                                                  "play simultaneously to be sped up or slowed down in order to match length. This improves visuals in quite " +
                                                  "a few instances - for example, it can make blending between a jog and a run have the feet hit the ground " +
                                                  "at the same time";
            compensateForDurationsField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree1D.compensateForDifferentDurations)));
            section.Add(compensateForDurationsField);

            entrySection = new VisualElement();
            section.Add(entrySection);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree1D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree1DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                entryElements.Add(entryElement);
                entryElement.SetBlendVariableName(blendVariableProp.stringValue);
                entrySection.Add(entryElement.rootVisualElement);
            }

            var addEntryButton = new Button
            {
                text = "Add Entry",
                clickable = new Clickable(AddEntry)
            };
            addEntryButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__addButton");
            section.Add(addEntryButton);
        }

        private void AddEntry()
        {
            var entryProp = entriesProp.AppendToArray();
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.clip)).objectReferenceValue = null;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.threshold)).floatValue = 0f;

            serializedObject.ApplyModifiedProperties();

            var entryElement = new BlendTree1DEntry(editor, entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1));
            entrySection.Add(entryElement.rootVisualElement);
            entryElements.Add(entryElement);
            entryElement.SetBlendVariableName(blendVariableProp.stringValue);
        }

        private void BlendVariableChanged(ChangeEvent<string> evt)
        {
            foreach (var entryElement in entryElements)
                entryElement.SetBlendVariableName(evt.newValue);
        }
    }

    public class BlendTree1DEntry : AnimationPlayerUINode {
        private FloatField thresholdField;

        public BlendTree1DEntry(AnimationPlayerEditor_Manual editor, SerializedProperty entryProp) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("blendTreeEntry");

            var clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            thresholdField = new FloatField();
            thresholdField.AddToClassList("blendTreeEntry__threshold");
            thresholdField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.threshold)));

            rootVisualElement.Add(clipField);
            rootVisualElement.Add(thresholdField);
        }

        public void SetBlendVariableName(string blendVariableName)
        {
            thresholdField.label = $"When \"{blendVariableName}\" = ";
        }
    }

    public class BlendTree2DDisplay : StateDisplay {
        private SerializedProperty entriesProp;
        private SerializedProperty blendVariable1Prop;
        private SerializedProperty blendVariable2Prop;
        private List<BlendTree2DEntry> entryElements = new List<BlendTree2DEntry>();
        private VisualElement entrySection;

        public BlendTree2DDisplay(AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp)
            : base(editor, stateListProp, layerIndex, stateIndex, stateProp) { }

        protected override void FillAlwaysVisibleSection(VisualElement section)
        {
            base.FillAlwaysVisibleSection(section);

            var blendVariableField = new TextField("Blend Variable");
            blendVariable1Prop = stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable));
            blendVariableField.BindProperty(blendVariable1Prop);
            section.Add(blendVariableField);

            var blendVariable2Field = new TextField("Blend Variable 2");
            blendVariable2Prop = stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable2));
            blendVariable2Field.BindProperty(blendVariable2Prop);
            section.Add(blendVariable2Field);

            blendVariableField.RegisterValueChangedCallback(BlendVariable1Changed);
            blendVariable2Field.RegisterValueChangedCallback(BlendVariable2Changed);
        }

        protected override void FillOnlyVisibleWhenExpandedSection(VisualElement section)
        {
            base.FillOnlyVisibleWhenExpandedSection(section);

            entrySection = new VisualElement();
            section.Add(entrySection);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree2D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree2DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                entryElement.SetBlendVariableNames(blendVariable1Prop.stringValue, blendVariable2Prop.stringValue);

                entryElements.Add(entryElement);
                entrySection.Add(entryElement.rootVisualElement);
            }

            var addEntryButton = new Button
            {
                text = "Add Entry",
                clickable = new Clickable(AddEntry)
            };
            addEntryButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__addButton");
            section.Add(addEntryButton);
        }

        private void BlendVariable1Changed(ChangeEvent<string> evt)
        {
            foreach (var entryElement in entryElements)
                entryElement.SetBlendVariableNames(evt.newValue, blendVariable2Prop.stringValue);
        }

        private void BlendVariable2Changed(ChangeEvent<string> evt)
        {
            foreach (var entryElement in entryElements)
                entryElement.SetBlendVariableNames(blendVariable1Prop.stringValue, evt.newValue);
        }

        private void AddEntry()
        {
            var entryProp = entriesProp.AppendToArray();
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.clip)).objectReferenceValue = null;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold1)).floatValue = 0f;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold2)).floatValue = 0f;

            serializedObject.ApplyModifiedProperties();

            var entryElement = new BlendTree2DEntry(editor, entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1));
            entryElement.SetBlendVariableNames(blendVariable1Prop.stringValue, blendVariable2Prop.stringValue);
            entryElements.Add(entryElement);
            entrySection.Add(entryElement.rootVisualElement);
        }
    }

    public class BlendTree2DEntry : AnimationPlayerUINode {
        private FloatField threshold1Field;
        private FloatField threshold2Field;

        public BlendTree2DEntry(AnimationPlayerEditor_Manual editor, SerializedProperty entryProp) : base(editor)
        {
            rootVisualElement = new VisualElement();
            rootVisualElement.AddToClassList("blendTreeEntry");

            var clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            threshold1Field = new FloatField();
            threshold1Field.AddToClassList("blendTreeEntry__threshold");
            threshold1Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold1)));

            threshold2Field = new FloatField();
            threshold2Field.AddToClassList("blendTreeEntry__threshold");
            threshold2Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold2)));

            rootVisualElement.Add(clipField);
            rootVisualElement.Add(threshold1Field);
            rootVisualElement.Add(threshold2Field);
        }

        public void SetBlendVariableNames(string blendVar1, string blendVar2)
        {
            threshold1Field.label = $"When \"{blendVar1}\" = ";
            threshold2Field.label = $"When \"{blendVar2}\" = ";
        }
    }

    public class SequenceDisplay : StateDisplay {
        private SerializedProperty clipsProp;
        private VisualElement clipsSection;

        public SequenceDisplay(AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp)
            : base(editor, stateListProp, layerIndex, stateIndex, stateProp) { }

        protected override void FillOnlyVisibleWhenExpandedSection(VisualElement section)
        {
            base.FillOnlyVisibleWhenExpandedSection(section);

            var loopModeField = new EnumField("Loop Mode");
            loopModeField.BindProperty(stateProp.FindPropertyRelative(nameof(Sequence.loopMode)));
            section.Add(loopModeField);

            clipsSection = new VisualElement();
            section.Add(clipsSection);

            clipsProp = stateProp.FindPropertyRelative(nameof(Sequence.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));

            var addClipButton = new Button
            {
                text = "Add Clip",
                clickable = new Clickable(AddClip)
            };
            addClipButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__addButton");
            section.Add(addClipButton);
        }

        private void AddClipVisualElement(SerializedProperty clipProp)
        {
            var clipField = new ObjectField("Clip");
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(clipProp);
            clipsSection.Add(clipField);
        }

        private void AddClip()
        {
            var clipProp = clipsProp.AppendToArray();
            clipProp.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            AddClipVisualElement(clipProp);
        }
    }

    public class RandomClipDisplay : StateDisplay {
        private SerializedProperty clipsProp;
        private VisualElement clipsSection;

        public RandomClipDisplay(AnimationPlayerEditor_Manual editor, SerializedProperty stateListProp, int layerIndex, int stateIndex, SerializedProperty stateProp)
            : base(editor, stateListProp, layerIndex, stateIndex, stateProp) { }

        protected override void FillOnlyVisibleWhenExpandedSection(VisualElement section)
        {
            base.FillOnlyVisibleWhenExpandedSection(section);

            clipsSection = new VisualElement();
            section.Add(clipsSection);

            clipsProp = stateProp.FindPropertyRelative(nameof(PlayRandomClip.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));

            var addClipButton = new Button
            {
                text = "Add Clip",
                clickable = new Clickable(AddClip)
            };
            addClipButton.AddToClassList("unity-button");
            addClipButton.AddToClassList("animationLayer__editStatesSection__stateSet__state__addButton");
            section.Add(addClipButton);
        }

        private void AddClipVisualElement(SerializedProperty clipProp)
        {
            var clipField = new ObjectField("Clip");
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(clipProp);
            clipsSection.Add(clipField);
        }

        private void AddClip()
        {
            var clipProp = clipsProp.AppendToArray();
            clipProp.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            AddClipVisualElement(clipProp);
        }
    }
}
}