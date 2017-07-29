using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimationState
{
    public const string DefaultSingleClipName = "New State";
    public const string DefaultBlendTreeName = "New Blend Tree";

    [SerializeField]
    private string name;
    [SerializeField]
    private bool hasUpdatedName;
    public double speed;
    public AnimationClip clip;

    public AnimationStateType type;

    public string blendVariable;
    public List<BlendTreeEntry> blendTree;

    public string Name
    {
        get { return name; }
        set
        {
            if (name == value)
                return;

            hasUpdatedName = true;
            name = value;
        }
    }

    public static AnimationState SingleClip(string name)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.SingleClip,
            hasUpdatedName = !name.StartsWith(DefaultSingleClipName)
        };
    }

    public static AnimationState BlendTree1D(string name)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.BlendTree1D,
            blendVariable = "blend",
            blendTree = new List<BlendTreeEntry>(),
            hasUpdatedName = !name.StartsWith(DefaultBlendTreeName)
        };
    }

    public void OnClipAssigned()
    {
        if (type != AnimationStateType.SingleClip)
            return;

        if (string.IsNullOrEmpty(name))
            name = clip.name;
        else if (!hasUpdatedName)
            name = clip.name;
    }

    public override string ToString()
    {
        return $"{name} ({type})";
    }

}

[Serializable]
public class BlendTreeEntry
{
    public float threshold;
    public AnimationClip clip;
}

//@TODO: This could be done with inheritance + custom serialization. That'd reduce size and complexity... maybe?
public enum AnimationStateType
{
    SingleClip,
    BlendTree1D,
}