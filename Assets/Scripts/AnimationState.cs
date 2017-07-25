using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimationState
{
    public const string DefaultName = "New State";
    public const string DefaultTreeName = "New Blend Tree";
    
    public string name;
    public double speed;
    public AnimationClip clip;
    
    public AnimationStateType type;

    public string blendVariable;
    public List<BlendTreeEntry> blendTree;

    public static AnimationState Normal(string name = DefaultName)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.SingleClip
        };
    }

    public static AnimationState BlendTree1D(string name = DefaultTreeName)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.BlendTree1D,
            blendVariable = "blend",
            blendTree = new List<BlendTreeEntry>()
        };
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

//@TODO: This could be done with inheritance + custom serialization. That'd reduce size and complexity 
public enum AnimationStateType
{
    SingleClip,
    BlendTree1D,
}