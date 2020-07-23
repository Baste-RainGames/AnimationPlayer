using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Animation_Player
{
[CustomEditor(typeof(AnimationPlayer))]
public class NewNewAnimationPlayerEditor : Editor
{
    private UIRoot uiRoot;

    private void OnEnable()
    {
        var directReference = (AnimationPlayer) target;
        if (directReference.layers == null)
            InitializeJustAddedAnimationPlayer();

        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= HandleUndoRedo;
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
        serializedObject.Update();

        var parentElement = uiRoot.visualElement.parent;
        var index = parentElement.IndexOf(uiRoot.visualElement);
        parentElement.RemoveAt(index);

        var selectedLayer = uiRoot.selectedLayer;
        uiRoot = new UIRoot(this);
        uiRoot.Init();
        uiRoot.topBar.layerDropdown.SelectLayer(Mathf.Min(selectedLayer, uiRoot.layersContainer.NumLayers - 1));

        parentElement.Insert(index, uiRoot.visualElement);
    }

    public override VisualElement CreateInspectorGUI()
    {
        uiRoot = new UIRoot(this);
        uiRoot.Init();
        return uiRoot.visualElement;
    }

    public abstract class AnimationPlayerUINode
    {
        protected readonly NewNewAnimationPlayerEditor editor;
        public VisualElement visualElement;

        protected SerializedObject serializedObject => editor.serializedObject;
        protected UIRoot root => editor.uiRoot;

        protected AnimationPlayerUINode(NewNewAnimationPlayerEditor editor)
        {
            this.editor = editor;
        }
    }

    public class UIRoot : AnimationPlayerUINode
    {
        public TopBar topBar;
        public LayersContainer layersContainer;
        public int selectedLayer;

        public UIRoot(NewNewAnimationPlayerEditor editor) : base(editor) { }

        public void Init()
        {
            visualElement = new VisualElement();
            visualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.baste.animationplayer/Editor/USS/AnimationPlayer.uss"));

            topBar = new TopBar(editor);
            layersContainer = new LayersContainer(editor);

            visualElement.Add(topBar.visualElement);
            visualElement.Add(layersContainer.visualElement);

            topBar.layerDropdown.LayersReady();
            topBar.layerDropdown.SelectLayer(selectedLayer);

            if (selectedLayer == 0)
                layersContainer.SetLayerVisible(selectedLayer);
        }
    }

    public class TopBar : AnimationPlayerUINode
    {
        public readonly LayerDropdown layerDropdown;
        public readonly RemoveLayerButton removeLayerButton;

        public TopBar(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("topBar");

            layerDropdown         = new LayerDropdown    (editor);
            var addLayerButton    = new AddLayerButton   (editor);
            removeLayerButton = new RemoveLayerButton(editor);

            visualElement.Add(layerDropdown    .visualElement);
            visualElement.Add(addLayerButton   .visualElement);
            visualElement.Add(removeLayerButton.visualElement);
        }
    }

    public class LayerDropdown : AnimationPlayerUINode
    {
        private PopupField<Layer> popupField;

        public LayerDropdown(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            // placeholder for the popup field.
            visualElement = new VisualElement();
        }

        public void LayersReady()
        {
            var old = visualElement;

            visualElement = popupField = new PopupField<Layer>(
                root.layersContainer.layers,
                0,
                Layer.GetNameOf,
                Layer.GetNameOf
            );
            popupField.AddToClassList("topBar__element");
            popupField.RegisterValueChangedCallback(LayerChanged);

            old.parent.Replace(old, visualElement);
        }

        private void LayerChanged(ChangeEvent<Layer> evt)
        {
            var index = root.layersContainer.layers.IndexOf(evt.newValue);
            root.selectedLayer = index;
            root.layersContainer.SetLayerVisible(index);
        }

        public void SelectLayer(int newLayer) => popupField.index = newLayer;
    }

    public class RemoveLayerButton : AnimationPlayerUINode
    {
        public RemoveLayerButton(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new Button
            {
                text = "Remove Layer",
                clickable = new Clickable(RemoveLayerClicked),

            };
            visualElement.AddToClassList("topBar__element");
            visualElement.SetEnabled(editor.serializedObject.FindProperty("layers").arraySize > 1);
        }

        private void RemoveLayerClicked()
        {
            if (root.layersContainer.NumLayers == 1)
                return;
            root.layersContainer.RemoveLayer(root.selectedLayer);
            editor.RebuildUI();
        }
    }

    public class AddLayerButton : AnimationPlayerUINode
    {
        public AddLayerButton(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new Button
            {
                text = "Add Layer",
                clickable = new Clickable(AddLayerClicked)
            };
            visualElement.AddToClassList("topBar__element");
        }

        private void AddLayerClicked()
        {
            root.layersContainer.AddNewLayer();
            root.topBar.layerDropdown.SelectLayer(root.layersContainer.NumLayers - 1);

            editor.uiRoot.topBar.removeLayerButton.visualElement.SetEnabled(editor.serializedObject.FindProperty("layers").arraySize > 1);
        }
    }

    public class LayersContainer : AnimationPlayerUINode
    {
        public readonly List<Layer> layers;
        private SerializedProperty layersProp;

        public LayersContainer(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            layers = new List<Layer>();

            layersProp = serializedObject.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
                CreateLayer(editor, i);
        }

        public int NumLayers => layersProp.arraySize;
        public Layer SelectedLayer => layers[root.selectedLayer];

        private void CreateLayer(NewNewAnimationPlayerEditor editor, int layerIndex)
        {
            var layer = new Layer(editor, layerIndex);
            layers.Add(layer);
            visualElement.Add(layer.visualElement);
        }

        public void SetLayerVisible(int index)
        {
            for (int i = 0; i < layers.Count; i++)
                layers[i].visualElement.SetDisplayed(index == i);
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
            CreateLayer(editor, layersProp.arraySize - 1);

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

    public class Layer : AnimationPlayerUINode
    {
        public readonly SerializedProperty serializedProperty;
        public readonly EditStatesSection editStatesSection;

        public Layer(NewNewAnimationPlayerEditor editor, int index) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer");

            serializedProperty = serializedObject.FindProperty(nameof(AnimationPlayer.layers)).GetArrayElementAtIndex(index);

            var editLayerSection          = new EditLayerSection         (editor, serializedProperty);
            var addAndSearchStatesSection = new AddAndSearchStatesSection(editor);
            editStatesSection             = new EditStatesSection        (editor, serializedProperty);

            visualElement.Add(editLayerSection         .visualElement);
            visualElement.Add(addAndSearchStatesSection.visualElement);
            visualElement.Add(editStatesSection        .visualElement);
        }

        public static string GetNameOf(Layer layer) => layer.GetName();
        private string GetName() => serializedProperty.FindPropertyRelative("name").stringValue;
    }

    public class EditLayerSection : AnimationPlayerUINode
    {
        public EditLayerSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");

            var layerName        = new LayerName(editor, layerProp);
            var layerType        = new LayerType(editor, layerProp);
            var layerStartWeight = new LayerStartWeight(editor, layerProp);
            var avatarMask       = new AvatarMaskNode(editor, layerProp);

            visualElement.Add(layerName.visualElement);
            visualElement.Add(layerType.visualElement);
            visualElement.Add(layerStartWeight.visualElement);
            visualElement.Add(avatarMask.visualElement);
        }
    }

    public class LayerName : AnimationPlayerUINode
    {
        private TextField textField;

        public LayerName(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) :
            base(editor)
        {
            visualElement = textField = new TextField("Layer Name");
            Rebind(layerProp);
        }

        public void Rebind(SerializedProperty newLayerProp)
        {
            textField.BindProperty(newLayerProp.FindPropertyRelative(nameof(AnimationLayer.name)));
        }
    }

    public class LayerType : AnimationPlayerUINode
    {
        public LayerType(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            var enumField = new EnumField("Layer Type");
            enumField.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.type)));
            visualElement = enumField;
        }
    }

    public class LayerStartWeight : AnimationPlayerUINode
    {
        public LayerStartWeight(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            var slider = new Slider("Layer Start Weight", 0f, 1f);
            slider.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.startWeight)));
            visualElement = slider;
        }
    }

    public class AvatarMaskNode : AnimationPlayerUINode
    {
        public AvatarMaskNode(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            var objectField = new ObjectField("Avatar Mask");
            objectField.objectType = typeof(AvatarMask);
            objectField.BindProperty(layerProp.FindPropertyRelative(nameof(AnimationLayer.mask)));

            visualElement = objectField;
        }
    }

    public class AddAndSearchStatesSection : AnimationPlayerUINode
    {
        public AddAndSearchStatesSection(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");
            visualElement.AddToClassList("animationLayer__statesHeader");

            var label = new Label("States");
            label.AddToClassList("label");
            visualElement.Add(label);

            var search = new ToolbarSearchField();
            search.AddToClassList("animationLayer__statesHeader__search");
            search.Q("unity-text-input").AddToClassList("animationLayer__statesHeader__search__textInput");
            visualElement.Add(search);

            search.RegisterValueChangedCallback(SearchUpdated);

            var addStateButton = new AddStateButton(editor);
            visualElement.Add(addStateButton.visualElement);
        }

        private void SearchUpdated(ChangeEvent<string> evt)
        {
            root.layersContainer.SelectedLayer.editStatesSection.SetSearchTerm(evt.newValue);
        }
    }

    public class AddStateButton : AnimationPlayerUINode
    {
        private static List<Type> allStateTypes = new List<Type>
        {
            typeof(SingleClip),
            typeof(BlendTree1D),
            typeof(BlendTree2D),
            typeof(Sequence),
            typeof(PlayRandomClip)
        };

        public AddStateButton(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            Button button;
            visualElement = button = new Button();

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
            HandleGUIDOfNewState();

            layerProp.serializedObject.ApplyModifiedProperties();

            root.layersContainer.layers[root.selectedLayer].editStatesSection.OnStateAdded(stateType);

            SerializedProperty AppendToArray(string arrayPropName)
            {
                return layerProp.FindPropertyRelative(arrayPropName).AppendToArray();
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

    public class EditStatesSection : AnimationPlayerUINode
    {
        private readonly NoStatesLabel      noStatesLabel;
        public readonly SingleClipSection  singleClipSection;
        public readonly BlendTree1DSection blendTree1DSection;
        public readonly BlendTree2DSection blendTree2DSection;
        public readonly SequenceSection    sequenceSection;
        public readonly RandomClipSection  randomClipSection;

        public EditStatesSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");
            visualElement.AddToClassList("animationLayer__editStatesSection");

            singleClipSection  = new SingleClipSection (editor, layerProperty);
            blendTree1DSection = new BlendTree1DSection(editor, layerProperty);
            blendTree2DSection = new BlendTree2DSection(editor, layerProperty);
            sequenceSection    = new SequenceSection   (editor, layerProperty);
            randomClipSection  = new RandomClipSection (editor, layerProperty);
            noStatesLabel      = new NoStatesLabel     (editor, layerProperty, this);

            visualElement.Add(noStatesLabel     .visualElement);
            visualElement.Add(singleClipSection .visualElement);
            visualElement.Add(blendTree1DSection.visualElement);
            visualElement.Add(blendTree2DSection.visualElement);
            visualElement.Add(sequenceSection   .visualElement);
            visualElement.Add(randomClipSection .visualElement);
        }

        public void OnStateAdded(Type stateType)
        {
            noStatesLabel.visualElement.SetDisplayed(false);
            if (stateType == typeof(SingleClip))
                singleClipSection.OnStateAdded();
            else if (stateType == typeof(BlendTree1D))
                blendTree1DSection.OnStateAdded();
            else if (stateType == typeof(BlendTree2D))
                blendTree2DSection.OnStateAdded();
            else if (stateType == typeof(PlayRandomClip))
                randomClipSection.OnStateAdded();
            else if (stateType == typeof(Sequence))
                sequenceSection.OnStateAdded();
            else
                Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
        }

        public void SetSearchTerm(string searchTerm)
        {
            singleClipSection .SetSearchTerm(searchTerm);
            blendTree1DSection.SetSearchTerm(searchTerm);
            blendTree2DSection.SetSearchTerm(searchTerm);
            sequenceSection   .SetSearchTerm(searchTerm);
            randomClipSection .SetSearchTerm(searchTerm);

            noStatesLabel.OnSearchTermUpdated();
        }
    }

    public class NoStatesLabel : AnimationPlayerUINode
    {
        private Label label;
        private EditStatesSection parent;
        private SerializedProperty layerProperty;

        public NoStatesLabel( NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty, EditStatesSection parent) : base(editor)
        {
            this.parent = parent;
            this.layerProperty = layerProperty;
            visualElement = label = new Label();

            OnSearchTermUpdated();
        }

        public void OnSearchTermUpdated()
        {
            var shouldBeHidden = parent.singleClipSection .visualElement.IsDisplayed() ||
                                 parent.blendTree1DSection.visualElement.IsDisplayed() ||
                                 parent.blendTree2DSection.visualElement.IsDisplayed() ||
                                 parent.sequenceSection   .visualElement.IsDisplayed() ||
                                 parent.randomClipSection .visualElement.IsDisplayed();

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

    public abstract class StateSection<TStateDisplay> : AnimationPlayerUINode where TStateDisplay : StateDisplay
    {
        private SerializedProperty stateListProp;
        private List<TStateDisplay> childDisplays = new List<TStateDisplay>();

        protected StateSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__editStatesSection__clipSet");

            var label = new Label(LabelText);
            label.AddToClassList("animationLayer__editStatesSection__clipSet__header");
            visualElement.Add(label);

            stateListProp = layerProperty.FindPropertyRelative(ListPropName);
            visualElement.SetDisplayed(stateListProp.arraySize > 0);

            for (int i = 0; i < stateListProp.arraySize; i++)
            {
                var display = CreateDisplayForState(stateListProp.GetArrayElementAtIndex(i));
                childDisplays.Add(display);
                visualElement.Add(display.visualElement);
            }
        }

        protected abstract TStateDisplay CreateDisplayForState(SerializedProperty stateProp);

        protected abstract string LabelText { get ; }
        protected abstract string ListPropName { get ; }

        public void OnStateAdded()
        {
            visualElement.SetDisplayed(stateListProp.arraySize > 0);

            var display = CreateDisplayForState(stateListProp.GetArrayElementAtIndex(stateListProp.arraySize - 1));
            childDisplays.Add(display);
            visualElement.Add(display.visualElement);
        }

        public void SetSearchTerm(string searchTerm)
        {
            var regex = new Regex(searchTerm);
            foreach (var stateDisplay in childDisplays)
            {
                var stateName = stateDisplay.stateProp.FindPropertyRelative("name").stringValue;
                stateDisplay.visualElement.SetDisplayed(regex.IsMatch(stateName));
            }

            visualElement.SetDisplayed(childDisplays.Any(cd => cd.visualElement.IsDisplayed()));
        }
    }

    public class SingleClipSection : StateSection<SingleClipDisplay>
    {
        public SingleClipSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override SingleClipDisplay CreateDisplayForState(SerializedProperty stateProp) => new SingleClipDisplay(editor, stateProp);
        protected override string LabelText { get; } = "Single Clips States";
        protected override string ListPropName { get; } = "serializedSingleClipStates";
    }

    public class BlendTree1DSection : StateSection<BlendTree1DDisplay>
    {
        public BlendTree1DSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override BlendTree1DDisplay CreateDisplayForState(SerializedProperty stateProp) => new BlendTree1DDisplay(editor, stateProp);
        protected override string LabelText { get; } = "1D Blend Trees";
        protected override string ListPropName { get; } = "serializedBlendTree1Ds";
    }

    public class BlendTree2DSection : StateSection<BlendTree2DDisplay>
    {
        public BlendTree2DSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override BlendTree2DDisplay CreateDisplayForState(SerializedProperty stateProp) => new BlendTree2DDisplay(editor, stateProp);
        protected override string LabelText { get; } = "2D Blend Trees";
        protected override string ListPropName { get; } = "serializedBlendTree2Ds";
    }

    public class SequenceSection : StateSection<SequenceDisplay>
    {
        public SequenceSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override SequenceDisplay CreateDisplayForState(SerializedProperty stateProp) => new SequenceDisplay(editor, stateProp);
        protected override string LabelText { get; } = "Sequences";
        protected override string ListPropName { get; } = "serializedSequences";
    }

    public class RandomClipSection : StateSection<RandomClipDisplay>
    {
        public RandomClipSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override RandomClipDisplay CreateDisplayForState(SerializedProperty stateProp)
        {
            return new RandomClipDisplay(editor, stateProp);
        }

        protected override string LabelText { get; } = "Random Clip States";
        protected override string ListPropName { get; } = "serializedSelectRandomStates";
    }

    public abstract class StateDisplay : AnimationPlayerUINode
    {
        public SerializedProperty stateProp;

        protected StateDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor)
        {
            this.stateProp = stateProp;
            visualElement = new VisualElement();

            var textField = new TextField("State Name");
            var doubleField = new DoubleField("State Speed");

            textField.BindProperty(stateProp.FindPropertyRelative("name"));
            doubleField.BindProperty(stateProp.FindPropertyRelative(nameof(AnimationPlayerState.speed)));

            visualElement.Add(textField);
            visualElement.Add(doubleField);
        }
    }

    public class SingleClipDisplay : StateDisplay
    {
        public SingleClipDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty singleClipProp) : base(editor, singleClipProp)
        {
            var clipField = new ObjectField("Clip")
            {
                objectType = typeof(AnimationClip)
            };
            clipField.BindProperty(singleClipProp.FindPropertyRelative(nameof(SingleClip.clip)));
            visualElement.Add(clipField);
        }
    }

    public class BlendTree1DDisplay : StateDisplay
    {
        private SerializedProperty entriesProp;
        private Button addEntryButton;

        public BlendTree1DDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            var blendVariableField = new TextField("Blend Variable");
            blendVariableField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree1D.blendVariable)));
            visualElement.Add(blendVariableField);

            var compensateForDurationsField = new Toggle("Compensate For Different Durations");
            compensateForDurationsField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree1D.compensateForDifferentDurations)));
            visualElement.Add(blendVariableField);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree1D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree1DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                visualElement.Add(entryElement.visualElement);
            }

            addEntryButton = new Button
            {
                text = "Add Entry",
                clickable = new Clickable(AddEntry)
            };
            visualElement.Add(addEntryButton);
        }

        private void AddEntry()
        {
            var entryProp = entriesProp.AppendToArray();
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.clip)).objectReferenceValue = null;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.threshold)).floatValue = 0f;

            serializedObject.ApplyModifiedProperties();

            var entryElement = new BlendTree1DEntry(editor, entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1));
            visualElement.Add(entryElement.visualElement);

            visualElement.Remove(addEntryButton);
            visualElement.Add(addEntryButton);
        }
    }

    public class BlendTree1DEntry : AnimationPlayerUINode
    {
        public BlendTree1DEntry(NewNewAnimationPlayerEditor editor, SerializedProperty entryProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("blendTreeEntry");

            var clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            var thresholdField = new FloatField("threshold");
            thresholdField.AddToClassList("blendTreeEntry__threshold");
            thresholdField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.threshold)));

            visualElement.Add(clipField);
            visualElement.Add(thresholdField);
        }
    }

    public class BlendTree2DDisplay : StateDisplay
    {
        private SerializedProperty entriesProp;
        private Button addEntryButton;

        public BlendTree2DDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            var blendVariableField = new TextField("Blend Variable");
            blendVariableField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable)));
            visualElement.Add(blendVariableField);

            var blendVariable2Field = new TextField("Blend Variable 2");
            blendVariable2Field.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable2)));
            visualElement.Add(blendVariable2Field);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree2D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree2DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                visualElement.Add(entryElement.visualElement);
            }

            addEntryButton = new Button
            {
                text = "Add Entry",
                clickable = new Clickable(AddEntry)
            };
            visualElement.Add(addEntryButton);
        }

        private void AddEntry()
        {
            var entryProp = entriesProp.AppendToArray();
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.clip)).objectReferenceValue = null;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold1)).floatValue = 0f;
            entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold2)).floatValue = 0f;

            serializedObject.ApplyModifiedProperties();

            var entryElement = new BlendTree2DEntry(editor, entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1));
            visualElement.Add(entryElement.visualElement);

            visualElement.Remove(addEntryButton);
            visualElement.Add(addEntryButton);
        }
    }

    public class BlendTree2DEntry : AnimationPlayerUINode
    {
        public BlendTree2DEntry(NewNewAnimationPlayerEditor editor, SerializedProperty entryProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("blendTreeEntry");

            var clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            var threshold1Field = new FloatField("threshold");
            threshold1Field.AddToClassList("blendTreeEntry__threshold");
            threshold1Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold1)));

            var threshold2Field = new FloatField("threshold 2");
            threshold2Field.AddToClassList("blendTreeEntry__threshold");
            threshold2Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold2)));

            visualElement.Add(clipField);
            visualElement.Add(threshold1Field);
            visualElement.Add(threshold2Field);
        }
    }

    public class SequenceDisplay : StateDisplay
    {
        private SerializedProperty clipsProp;

        public SequenceDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            var loopModeField = new EnumField("Loop Mode");
            loopModeField.BindProperty(stateProp.FindPropertyRelative(nameof(Sequence.loopMode)));
            visualElement.Add(loopModeField);

            clipsProp = stateProp.FindPropertyRelative(nameof(Sequence.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));

            var addClipButton = new Button
            {
                text = "Add Clip",
                clickable = new Clickable(AddClip)
            };
            visualElement.Add(addClipButton);
        }

        private void AddClipVisualElement(SerializedProperty clipProp)
        {
            var clipField = new ObjectField("Clip");
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(clipProp);
            visualElement.Add(clipField);
        }

        private void AddClip()
        {
            var clipProp = clipsProp.AppendToArray();
            clipProp.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
            AddClipVisualElement(clipProp);
        }
    }

    public class RandomClipDisplay : StateDisplay
    {
        private SerializedProperty clipsProp;

        public RandomClipDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            clipsProp = stateProp.FindPropertyRelative(nameof(PlayRandomClip.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));

            var addClipButton = new Button
            {
                text = "Add Clip",
                clickable = new Clickable(AddClip)
            };
            visualElement.Add(addClipButton);
        }

        private void AddClipVisualElement(SerializedProperty clipProp)
        {
            var clipField = new ObjectField("Clip");
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(clipProp);
            visualElement.Add(clipField);
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