using System;
using UnityEngine;

[Serializable]
public class AnimationState
{
    public string name;
    public double speed = 1d;
    public AnimationClip clip;
    
    public AnimationStateType type;

    public string blendVariable;
    public BlendTreeEntry[] blendTree = new BlendTreeEntry[0];
}

[Serializable]
public class BlendTreeEntry
{
    public float value;
    public AnimationClip clip;
}

//@TODO: This could be done with inheritance + custom serialization. That'd reduce size and complexity 
public enum AnimationStateType
{
    SingleClip,
    BlendTree1D,
}