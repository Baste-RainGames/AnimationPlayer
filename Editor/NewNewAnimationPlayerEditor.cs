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

        public AnimationPlayerUINode(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent)
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

        public UIRoot(SerializedObject serializedObject) : base(null, serializedObject, null)
        {
            root = this;
            visualElement = new VisualElement();
            topBar = new TopBar(this, serializedObject, this);
            layersContainer = new LayersContainer(this, serializedObject, this);

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

        public TopBar(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
        {
            visualElement = new VisualElement();
            visualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.baste.animationplayer/Editor/UXML/AnimationPlayer.uss"));
            visualElement.AddToClassList("topBar");

            layerDropdown     = new LayerDropdown    (root, serializedObject, parent);
            addLayerButton    = new AddLayerButton   (root, serializedObject, parent);
            removeLayerButton = new RemoveLayerButton(root, serializedObject, parent);

            visualElement.Add(layerDropdown    .visualElement);
            visualElement.Add(addLayerButton   .visualElement);
            visualElement.Add(removeLayerButton.visualElement);
        }
    }

    public class LayerDropdown : AnimationPlayerUINode
    {
        private PopupField<Layer> popupField;

        public LayerDropdown(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
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
        public RemoveLayerButton(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
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
        public AddLayerButton(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
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

        public LayersContainer(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
        {
            visualElement = new VisualElement();
            layers = new List<Layer>();

            layersProp = serializedObject.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                CreateLayer(root, serializedObject, this, i);
            }
        }

        public int NumLayers => layersProp.arraySize;

        private void CreateLayer(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent, int i)
        {
            var layer = new Layer(root, serializedObject, parent, i);
            layers.Add(layer);
            visualElement.Add(layer.visualElement);
        }

        public void SetLayerVisible(int index)
        {
            for (int i = 0; i < layers.Count; i++)
                layers[i].visualElement.visible = index == i;
        }

        public void AddNewLayer()
        {
            layersProp.arraySize++;
            CreateLayer(root, serializedObject, this, layersProp.arraySize - 1);
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
        public EditStatesSection editStateSection;

        public Layer(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent, int index) : base(root, serializedObject, parent)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer");

            this.index = index;
            serializedProperty = serializedObject.FindProperty("layers").GetArrayElementAtIndex(index);

            editLayerSection = new EditLayerSection(root, serializedObject, parent);
            editStateSection = new EditStatesSection(root, serializedObject, parent);

            visualElement.Add(editLayerSection.visualElement);
            visualElement.Add(editStateSection.visualElement);
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
            serializedProperty = serializedObject.FindProperty("layers").GetArrayElementAtIndex(newIndex);
            editLayerSection.ParentIndexChanged(newIndex);
            editStateSection.ParentIndexChanged(newIndex);
        }
    }

    public class EditLayerSection : AnimationPlayerUINode
    {
        public EditLayerSection(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent)
        {
            visualElement = new VisualElement();
            visualElement.AddToClassList("animationLayer__subSection");


        }

        public void ParentIndexChanged(int newIndex)
        {

        }
    }

    public class EditStatesSection : AnimationPlayerUINode
    {
        public EditStatesSection(UIRoot root, SerializedObject serializedObject, AnimationPlayerUINode parent) : base(root, serializedObject, parent) { }

        public void ParentIndexChanged(int newIndex)
        {
        }
    }
}

}