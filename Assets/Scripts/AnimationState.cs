using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimationState
{
    public const string DefaultName = "new state";
    
    public string name;
    public double speed;
    public AnimationClip clip;
    
    public AnimationStateType type;

    public string blendVariable;
    public List<BlendTreeEntry> blendTree;

    public static AnimationState Normal()
    {
        return new AnimationState
        {
            name = DefaultName,
            speed = 1d,
            type = AnimationStateType.SingleClip
        };
    }

    public static AnimationState BlendTree1D()
    {
        return new AnimationState
        {
            name = DefaultName,
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