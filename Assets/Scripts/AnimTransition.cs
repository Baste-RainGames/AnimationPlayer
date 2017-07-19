using System;
using UnityEngine;

[Serializable]
public struct AnimTransition
{
    public float duration;
    public TransitionType type;
    public AnimationCurve curve;

    public static AnimTransition Linear(float duration)
    {
        return new AnimTransition
        {
            duration = Mathf.Max(0f, duration),
            type = TransitionType.Linear,
            curve = null
        };
    }

    public static AnimTransition Instant()
    {
        return new AnimTransition
        {
            duration = 0f,
            type = TransitionType.Linear,
            curve = null
        };
    }
}

public enum TransitionType
{
    Linear,
    Curve
}