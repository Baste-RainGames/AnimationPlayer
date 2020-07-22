using System;
using System.Collections.Generic;
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
            InitializeNewAnimationPlayer();

        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void InitializeNewAnimationPlayer()
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

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void HandleUndoRedo()
    {
        RebuildUI();
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
        public NewNewAnimationPlayerEditor editor;
        public VisualElement visualElement;

        protected SerializedObject serializedObject => editor.serializedObject;
        protected UIRoot root => editor.uiRoot;

        public AnimationPlayerUINode(NewNewAnimationPlayerEditor editor)
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
            visualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.baste.animationplayer/Editor/UXML/AnimationPlayer.uss"));

            topBar = new TopBar(editor);
            layersContainer = new LayersContainer(editor);

            visualElement.Add(topBar.visualElement);
            visualElement.Add(layersContainer.visualElement);

            topBar.layerDropdown.LayersReady();
            topBar.layerDropdown.SelectLayer(selectedLayer);

            layersContainer.SetLayerVisible(selectedLayer);
        }
    }

    public class TopBar : AnimationPlayerUINode
    {
        public LayerDropdown layerDropdown;
        public AddLayerButton addLayerButton;
        public RemoveLayerButton removeLayerButton;

        public TopBar(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("topBar");

            layerDropdown     = new LayerDropdown    (editor);
            addLayerButton    = new AddLayerButton   (editor);
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

        public void SelectLayer(int newLayer)
        {
            popupField.index = newLayer;
        }
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
        }
    }

    public class LayersContainer : AnimationPlayerUINode
    {
        public List<Layer> layers;
        private SerializedProperty layersProp;

        public LayersContainer(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            layers = new List<Layer>();

            layersProp = serializedObject.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                CreateLayer(editor, i);
            }
        }

        public int NumLayers => layersProp.arraySize;

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

            newLayer.FindPropertyRelative(nameof(AnimationLayer.name)).stringValue = layerName;
            newLayer.FindPropertyRelative(nameof(AnimationLayer.startWeight)).floatValue = 1f;

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
        public int index;
        public SerializedProperty serializedProperty;

        public EditLayerSection editLayerSection;
        public AddAndSearchStatesSection addAndSearchStatesSection;
        public EditStatesSection editStatesSection;

        public Layer(NewNewAnimationPlayerEditor editor, int index) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer");

            this.index = index;
            serializedProperty = serializedObject.FindProperty(nameof(AnimationPlayer.layers)).GetArrayElementAtIndex(index);

            editLayerSection          = new EditLayerSection         (editor, serializedProperty);
            addAndSearchStatesSection = new AddAndSearchStatesSection(editor);
            editStatesSection         = new EditStatesSection        (editor, serializedProperty);

            visualElement.Add(editLayerSection         .visualElement);
            visualElement.Add(addAndSearchStatesSection.visualElement);
            visualElement.Add(editStatesSection        .visualElement);
        }

        public static string GetNameOf(Layer layer)
        {
            return layer.GetName();
        }

        private string GetName()
        {
            return serializedProperty.FindPropertyRelative("name").stringValue;
        }

        public void IndexChanged(int newIndex)
        {
            if (index == newIndex)
                return;

            index = newIndex;
            serializedProperty = serializedObject.FindProperty(nameof(AnimationPlayer.layers)).GetArrayElementAtIndex(newIndex);
            editLayerSection.ParentSerializedPropertyChanged(serializedProperty);
            editStatesSection.ParentIndexChanged(newIndex);
        }
    }

    public class EditLayerSection : AnimationPlayerUINode
    {
        public LayerName        layerName;
        public LayerType        layerType;
        public LayerStartWeight LayerStartWeight;
        public AvatarMaskNode   avatarMask;

        public EditLayerSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");

            layerName        = new LayerName(editor, layerProp);
            layerType        = new LayerType(editor, layerProp);
            LayerStartWeight = new LayerStartWeight(editor, layerProp);
            avatarMask       = new AvatarMaskNode(editor, layerProp);

            visualElement.Add(layerName.visualElement);
            visualElement.Add(layerType.visualElement);
            visualElement.Add(LayerStartWeight.visualElement);
            visualElement.Add(avatarMask.visualElement);
        }

        public void ParentSerializedPropertyChanged(SerializedProperty newLayersProp)
        {
            layerName.Rebind(newLayersProp);
            layerType.Rebind(newLayersProp);
            LayerStartWeight.Rebind(newLayersProp);
            avatarMask.Rebind(newLayersProp);
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
        private EnumField enumField;

        public LayerType(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            visualElement = enumField = new EnumField("Layer Type");
            Rebind(layerProp);
        }

        public void Rebind(SerializedProperty newLayerProp)
        {
            enumField.BindProperty(newLayerProp.FindPropertyRelative(nameof(AnimationLayer.type)));
        }
    }

    public class LayerStartWeight : AnimationPlayerUINode
    {
        private Slider slider;

        public LayerStartWeight(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            visualElement = slider = new Slider("Layer Start Weight", 0f, 1f);
            Rebind(layerProp);
        }

        public void Rebind(SerializedProperty newLayersProp)
        {
            slider.BindProperty(newLayersProp.FindPropertyRelative(nameof(AnimationLayer.startWeight)));
        }
    }

    public class AvatarMaskNode : AnimationPlayerUINode
    {
        private ObjectField objectField;

        public AvatarMaskNode(NewNewAnimationPlayerEditor editor, SerializedProperty layerProp) : base(editor)
        {
            visualElement = objectField = new ObjectField("Avatar Mask");
            objectField.objectType = typeof(AvatarMask);
            Rebind(layerProp);
        }

        public void Rebind(SerializedProperty newLayersProp)
        {
            objectField.BindProperty(newLayersProp.FindPropertyRelative(nameof(AnimationLayer.mask)));
        }
    }

    public class AddAndSearchStatesSection : AnimationPlayerUINode
    {
        public AddStateButton addStateButton;

        public AddAndSearchStatesSection(NewNewAnimationPlayerEditor editor) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");
            visualElement.AddToClassList("animationLayer__addAndSearchStatesSection");

            addStateButton = new AddStateButton(editor);

            visualElement.Add(addStateButton.visualElement);
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
        public NoStatesLabel      noStatesLabel;
        public SingleClipSection  singleClipSection;
        public BlendTree1DSection blendTree1DSection;
        public BlendTree2DSection blendTree2DSection;
        public SequenceSection    sequenceSection;
        public RandomClipSection  randomClipSection;

        public EditStatesSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");
            visualElement.AddToClassList("animationLayer__editStatesSection");

            noStatesLabel      = new NoStatesLabel     (editor, layerProperty);
            singleClipSection  = new SingleClipSection (editor, layerProperty);
            blendTree1DSection = new BlendTree1DSection(editor, layerProperty);
            blendTree2DSection = new BlendTree2DSection(editor, layerProperty);
            sequenceSection    = new SequenceSection   (editor, layerProperty);
            randomClipSection  = new RandomClipSection (editor, layerProperty);

            visualElement.Add(noStatesLabel     .visualElement);
            visualElement.Add(singleClipSection .visualElement);
            visualElement.Add(blendTree1DSection.visualElement);
            visualElement.Add(blendTree2DSection.visualElement);
            visualElement.Add(sequenceSection   .visualElement);
            visualElement.Add(randomClipSection .visualElement);
        }

        public void ParentIndexChanged(int newIndex) { }

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
    }

    public class NoStatesLabel : AnimationPlayerUINode
    {
        public NoStatesLabel(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor)
        {
            visualElement = new Label("No states in layer");

            var countStates = layerProperty.FindPropertyRelative("serializedSingleClipStates").arraySize +
                              layerProperty.FindPropertyRelative("serializedBlendTree1Ds").arraySize +
                              layerProperty.FindPropertyRelative("serializedBlendTree2Ds").arraySize +
                              layerProperty.FindPropertyRelative("serializedSelectRandomStates").arraySize +
                              layerProperty.FindPropertyRelative("serializedSequences").arraySize;

            visualElement.SetDisplayed(countStates == 0);
        }
    }

    public abstract class StateSection<TStateDisplay> : AnimationPlayerUINode where TStateDisplay : StateDisplay
    {
        private Label label;
        private SerializedProperty stateListProp;
        private List<TStateDisplay> stateDisplays = new List<TStateDisplay>();

        protected StateSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__editStatesSection__clipSet");

            label = new Label(LabelText);
            label.AddToClassList("animationLayer__editStatesSection__clipSet__header");
            visualElement.Add(label);

            stateListProp = layerProperty.FindPropertyRelative(ListPropName);
            visualElement.SetDisplayed(stateListProp.arraySize > 0);

            for (int i = 0; i < stateListProp.arraySize; i++)
            {
                var display = CreateDisplayForState(stateListProp.GetArrayElementAtIndex(i));
                visualElement.Add(display.visualElement);
                stateDisplays.Add(display);
            }
        }

        protected abstract TStateDisplay CreateDisplayForState(SerializedProperty stateProp);

        public abstract string LabelText { get ; }
        public abstract string ListPropName { get ; }

        public void OnStateAdded()
        {
            visualElement.SetDisplayed(stateListProp.arraySize > 0);

            var display = CreateDisplayForState(stateListProp.GetArrayElementAtIndex(stateListProp.arraySize - 1));
            visualElement.Add(display.visualElement);
            stateDisplays.Add(display);
        }
    }

    public class SingleClipSection : StateSection<SingleClipDisplay>
    {
        public SingleClipSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override SingleClipDisplay CreateDisplayForState(SerializedProperty stateProp) => new SingleClipDisplay(editor, stateProp);
        public override string LabelText { get; } = "Single Clips States";
        public override string ListPropName { get; } = "serializedSingleClipStates";
    }

    public class BlendTree1DSection : StateSection<BlendTree1DDisplay>
    {
        public BlendTree1DSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override BlendTree1DDisplay CreateDisplayForState(SerializedProperty stateProp) => new BlendTree1DDisplay(editor, stateProp);
        public override string LabelText { get; } = "1D Blend Trees";
        public override string ListPropName { get; } = "serializedBlendTree1Ds";
    }

    public class BlendTree2DSection : StateSection<BlendTree2DDisplay>
    {
        public BlendTree2DSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override BlendTree2DDisplay CreateDisplayForState(SerializedProperty stateProp) => new BlendTree2DDisplay(editor, stateProp);
        public override string LabelText { get; } = "2D Blend Trees";
        public override string ListPropName { get; } = "serializedBlendTree2Ds";
    }

    public class SequenceSection : StateSection<SequenceDisplay>
    {
        public SequenceSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override SequenceDisplay CreateDisplayForState(SerializedProperty stateProp) => new SequenceDisplay(editor, stateProp);
        public override string LabelText { get; } = "Sequences";
        public override string ListPropName { get; } = "serializedSequences";
    }

    public class RandomClipSection : StateSection<RandomClipDisplay>
    {
        public RandomClipSection(NewNewAnimationPlayerEditor editor, SerializedProperty layerProperty) : base(editor, layerProperty) { }

        protected override RandomClipDisplay CreateDisplayForState(SerializedProperty stateProp)
        {
            return new RandomClipDisplay(editor, stateProp);
        }

        public override string LabelText { get; } = "Random Clip States";
        public override string ListPropName { get; } = "serializedSelectRandomStates";
    }

    public abstract class StateDisplay : AnimationPlayerUINode
    {
        public TextField textField;
        public DoubleField doubleField;

        protected StateDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor)
        {
            visualElement = new VisualElement();

            textField = new TextField("State Name");
            doubleField = new DoubleField("State Speed");

            textField.BindProperty(stateProp.FindPropertyRelative("name"));
            doubleField.BindProperty(stateProp.FindPropertyRelative(nameof(SingleClip.speed)));

            visualElement.Add(textField);
            visualElement.Add(doubleField);
        }
    }

    public class SingleClipDisplay : StateDisplay
    {
        private ObjectField clipField;

        public SingleClipDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty singleClipProp) : base(editor, singleClipProp)
        {
            clipField = new ObjectField("Clip")
            {
                objectType = typeof(AnimationClip)
            };
            clipField.BindProperty(singleClipProp.FindPropertyRelative(nameof(SingleClip.clip)));

            visualElement.Add(clipField);
        }
    }

    public class BlendTree1DDisplay : StateDisplay
    {
        private TextField blendVariableField;
        private Toggle compensateForDurationsField;
        private List<BlendTree1DEntry> entries = new List<BlendTree1DEntry>();
        private SerializedProperty entriesProp;
        private Button addEntryButton;

        public BlendTree1DDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            blendVariableField = new TextField("Blend Variable");
            blendVariableField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree1D.blendVariable)));
            visualElement.Add(blendVariableField);

            compensateForDurationsField = new Toggle("Compensate For Different Durations");
            compensateForDurationsField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree1D.compensateForDifferentDurations)));
            visualElement.Add(blendVariableField);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree1D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree1DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                visualElement.Add(entryElement.visualElement);
                entries.Add(entryElement);
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
            entries.Add(entryElement);

            visualElement.Remove(addEntryButton);
            visualElement.Add(addEntryButton);
        }
    }

    public class BlendTree1DEntry : AnimationPlayerUINode
    {
        public ObjectField clipField;
        public FloatField thresholdField;

        public BlendTree1DEntry(NewNewAnimationPlayerEditor editor, SerializedProperty entryProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("blendTreeEntry");

            clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            thresholdField = new FloatField("threshold");
            thresholdField.AddToClassList("blendTreeEntry__threshold");
            thresholdField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry1D.threshold)));

            visualElement.Add(clipField);
            visualElement.Add(thresholdField);
        }
    }

    public class BlendTree2DDisplay : StateDisplay
    {
        private TextField blendVariableField;
        private TextField blendVariable2Field;
        private List<BlendTree2DEntry> entries = new List<BlendTree2DEntry>();
        private SerializedProperty entriesProp;
        private Button addEntryButton;

        public BlendTree2DDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            blendVariableField = new TextField("Blend Variable");
            blendVariableField.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable)));
            visualElement.Add(blendVariableField);

            blendVariable2Field = new TextField("Blend Variable 2");
            blendVariable2Field.BindProperty(stateProp.FindPropertyRelative(nameof(BlendTree2D.blendVariable2)));
            visualElement.Add(blendVariable2Field);

            entriesProp = stateProp.FindPropertyRelative(nameof(BlendTree2D.entries));
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entryElement = new BlendTree2DEntry(editor, entriesProp.GetArrayElementAtIndex(i));
                visualElement.Add(entryElement.visualElement);
                entries.Add(entryElement);
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
            entries.Add(entryElement);

            visualElement.Remove(addEntryButton);
            visualElement.Add(addEntryButton);
        }
    }

    public class BlendTree2DEntry : AnimationPlayerUINode
    {
        public ObjectField clipField;
        public FloatField threshold1Field;
        public FloatField threshold2Field;

        public BlendTree2DEntry(NewNewAnimationPlayerEditor editor, SerializedProperty entryProp) : base(editor)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("blendTreeEntry");

            clipField = new ObjectField("Clip");
            clipField.Q<Label>().style.minWidth = 50f;
            clipField.objectType = typeof(AnimationClip);
            clipField.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry.clip)));

            threshold1Field = new FloatField("threshold");
            threshold1Field.AddToClassList("blendTreeEntry__threshold");
            threshold1Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold1)));

            threshold2Field = new FloatField("threshold 2");
            threshold2Field.AddToClassList("blendTreeEntry__threshold");
            threshold2Field.BindProperty(entryProp.FindPropertyRelative(nameof(BlendTreeEntry2D.threshold2)));

            visualElement.Add(clipField);
            visualElement.Add(threshold1Field);
            visualElement.Add(threshold2Field);
        }
    }

    public class SequenceDisplay : StateDisplay
    {
        private EnumField loopModeField;
        private SerializedProperty clipsProp;
        private List<ObjectField> clipFields = new List<ObjectField>();
        private Button addClipButton;

        public SequenceDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            loopModeField = new EnumField("Loop Mode");
            loopModeField.BindProperty(stateProp.FindPropertyRelative(nameof(Sequence.loopMode)));
            visualElement.Add(loopModeField);

            clipsProp = stateProp.FindPropertyRelative(nameof(Sequence.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
            {
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));
            }

            addClipButton = new Button
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
            clipFields.Add(clipField);
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
        private List<ObjectField> clipFields = new List<ObjectField>();
        private Button addClipButton;

        public RandomClipDisplay(NewNewAnimationPlayerEditor editor, SerializedProperty stateProp) : base(editor, stateProp)
        {
            clipsProp = stateProp.FindPropertyRelative(nameof(PlayRandomClip.clips));
            for (int i = 0; i < clipsProp.arraySize; i++)
                AddClipVisualElement(clipsProp.GetArrayElementAtIndex(i));

            addClipButton = new Button
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
            clipFields.Add(clipField);
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