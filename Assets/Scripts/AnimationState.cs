using System;
using UnityEngine;

[Serializable]
public class AnimationState
{
    public string name;
    public AnimationClip clip;
    public double speed = 1d;
}