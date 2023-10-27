using System;

using UnityEditor;

using UnityEngine.UIElements;

namespace Animation_Player
{
public abstract class AnimationStateEditor
{
    public abstract VisualElement RootVisualElement { get; }

    public abstract void GenerateUI();
    public abstract void BindUI(SerializedProperty stateProperty);
    public abstract void ClearBindings(SerializedProperty stateProperty);

    public abstract Type GetEditedType();
    public abstract AnimationPlayerState CreateNewState(int stateIndex);
}
}