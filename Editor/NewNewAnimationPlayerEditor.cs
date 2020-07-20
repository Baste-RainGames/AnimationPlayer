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
    public override VisualElement CreateInspectorGUI()
    {
        return new UIRoot(serializedObject).visualElement;
    }

    public class AnimationPlayerUINode
    {
        public UIRoot root;
        public VisualElement visualElement;
        public SerializedObject serializedObject;

        public AnimationPlayerUINode(UIRoot root, SerializedObject serializedObject)
        {
            this.root = root;
            this.serializedObject = serializedObject;
        }
    }

    public class UIRoot : AnimationPlayerUINode
    {
        public TopBar topBar;
        public LayersContainer layersContainer;
        public int selectedLayer;

        public UIRoot(SerializedObject serializedObject) : base(null, serializedObject)
        {
            root = this;
            visualElement = new VisualElement();
            topBar = new TopBar(this, serializedObject);
            layersContainer = new LayersContainer(this, serializedObject);

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

        public TopBar(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
        {
            visualElement = new VisualElement();
            visualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.baste.animationplayer/Editor/UXML/AnimationPlayer.uss"));
            visualElement.AddToClassList("topBar");

            layerDropdown     = new LayerDropdown    (root, serializedObject);
            addLayerButton    = new AddLayerButton   (root, serializedObject);
            removeLayerButton = new RemoveLayerButton(root, serializedObject);

            visualElement.Add(layerDropdown    .visualElement);
            visualElement.Add(addLayerButton   .visualElement);
            visualElement.Add(removeLayerButton.visualElement);
        }
    }

    public class LayerDropdown : AnimationPlayerUINode
    {
        private PopupField<Layer> popupField;

        public LayerDropdown(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
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
        public RemoveLayerButton(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
        {
            visualElement = new Button
            {
                text = "Remove Layer",
                clickable = new Clickable(RemoveLayerClicked)
            };
            visualElement.AddToClassList("topBar__element");
        }

        private void RemoveLayerClicked()
        {
            root.layersContainer.RemoveLayer(root.selectedLayer);
            root.topBar.layerDropdown.SelectLayer(Mathf.Min(root.selectedLayer, root.layersContainer.NumLayers - 1));
        }
    }

    public class AddLayerButton : AnimationPlayerUINode
    {
        public AddLayerButton(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
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

        public LayersContainer(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
        {
            visualElement = new VisualElement();
            layers = new List<Layer>();

            layersProp = serializedObject.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                CreateLayer(root, serializedObject, i);
            }
        }

        public int NumLayers => layersProp.arraySize;

        private void CreateLayer(UIRoot root, SerializedObject serializedObject, int layerIndex)
        {
            var layer = new Layer(root, serializedObject, layerIndex);
            layers.Add(layer);
            visualElement.Add(layer.visualElement);
        }

        public void SetLayerVisible(int index)
        {
            for (int i = 0; i < layers.Count; i++)
                layers[i].visualElement.style.display = index == i ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void AddNewLayer()
        {
            layersProp.arraySize++;
            CreateLayer(root, serializedObject, layersProp.arraySize - 1);
        }

        public void RemoveLayer(int layer)
        {
            layersProp.DeleteArrayElementAtIndex(layer);
            layers.RemoveAt(layer);

            for (int i = layer; i < layers.Count; i++)
                layers[i].IndexChanged(i);
        }
    }

    public class Layer : AnimationPlayerUINode
    {
        public int index;
        public SerializedProperty serializedProperty;

        public EditLayerSection editLayerSection;
        public AddAndSearchStatesSection addAndSearchStatesSection;
        public EditStatesSection editStatesSection;

        public Layer(UIRoot root, SerializedObject serializedObject, int index) : base(root, serializedObject)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer");

            this.index = index;
            serializedProperty = serializedObject.FindProperty(nameof(AnimationPlayer.layers)).GetArrayElementAtIndex(index);

            editLayerSection = new EditLayerSection(root, serializedObject, serializedProperty);
            addAndSearchStatesSection = new AddAndSearchStatesSection(root, serializedObject);
            editStatesSection = new EditStatesSection(root, serializedObject);

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
        public LayerName      layerName;
        public LayerType      layerType;
        public LayerStartWeight    LayerStartWeight;
        public AvatarMaskNode avatarMask;

        public EditLayerSection(UIRoot root, SerializedObject serializedObject, SerializedProperty layerProp) : base(
            root, serializedObject)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");

            layerName   = new LayerName(root, serializedObject, layerProp);
            layerType   = new LayerType(root, serializedObject, layerProp);
            LayerStartWeight = new LayerStartWeight(root, serializedObject, layerProp);
            avatarMask  = new AvatarMaskNode(root, serializedObject, layerProp);

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

        public LayerName(UIRoot root, SerializedObject serializedObject, SerializedProperty layerProp) :
            base(root, serializedObject)
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

        public LayerType(UIRoot root, SerializedObject serializedObject, SerializedProperty layerProp) :
            base(root, serializedObject)
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

        public LayerStartWeight(UIRoot root, SerializedObject serializedObject, SerializedProperty layerProp) :
            base(root, serializedObject)
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

        public AvatarMaskNode(UIRoot root, SerializedObject serializedObject, SerializedProperty layerProp) : base(root, serializedObject)
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

        public AddAndSearchStatesSection(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");
            visualElement.AddToClassList("animationLayer__addAndSearchStatesSection");

            addStateButton = new AddStateButton(root, serializedObject);

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

        public AddStateButton(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject)
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
            var serializedOrder = AppendArray(layerProp, "serializedStateOrder");

            if (stateType == typeof(SingleClip))
            {
                stateProp = AppendArray(layerProp, "serializedSingleClipStates");
            }
            else if (stateType == typeof(BlendTree1D))
            {
                stateProp = AppendArray(layerProp, "serializedBlendTree1Ds");
                stateProp.FindPropertyRelative("blendVariable").stringValue = "blend";
                stateProp.FindPropertyRelative("compensateForDifferentDurations").boolValue = true;
            }
            else if (stateType == typeof(BlendTree2D))
            {
                stateProp = AppendArray(layerProp, "serializedBlendTree2Ds");
                stateProp.FindPropertyRelative("blendVariable").stringValue = "blend";
                stateProp.FindPropertyRelative("blendVariable2").stringValue = "blend2";
                stateProp.FindPropertyRelative("compensateForDifferentDurations").boolValue = true;
            }
            else if (stateType == typeof(PlayRandomClip))
            {
                stateProp = AppendArray(layerProp, "serializedSelectRandomStates");
            }
            else if (stateType == typeof(Sequence))
            {
                stateProp = AppendArray(layerProp, "serializedSequences");
            }
            else
            {
                Debug.LogError($"Adding state of type {stateType.Name} not implemented!");
                return;
            }

            HandleSharedProps();
            HandleGUIDOfNewState();

            layerProp.serializedObject.ApplyModifiedProperties();

            root.layersContainer.layers[root.selectedLayer].editStatesSection.OnStateAdded();

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

    public class EditStatesSection : AnimationPlayerUINode
    {
        public EditStatesSection(UIRoot root, SerializedObject serializedObject) : base(root, serializedObject) { }

        public void ParentIndexChanged(int newIndex) { }

        public void OnStateAdded()
        {

        }
    }
}
}