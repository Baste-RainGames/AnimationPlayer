using System;
using UnityEngine;

/// <summary>
/// Information about how to transition between two given states
/// </summary>
[Serializable]
public class StateTransition
{
    //@TODO: These should not be array indices, that makes removing a state brittle as FU.
    //       should also not be strings/object references, as the first makes renaming hard
    //       and the second doesn't play nice with Unity's serializer. Solution: GUID
    public int fromState, toState;
    public TransitionData transitionData;
}

/// <summary>
/// Information about how the AnimationPlayer should transition from one state to another
/// </summary>
[Serializable]
public struct TransitionData
{
    public float duration;
    public TransitionType type;
    public AnimationCurve curve;

    public static TransitionData Linear(float duration)
    {
        return new TransitionData
        {
            duration = Mathf.Max(0f, duration),
            type = TransitionType.Linear,
            curve = new AnimationCurve()
        };
    }

    public static TransitionData Instant()
    {
        return new TransitionData
        {
            duration = 0f,
            type = TransitionType.Linear,
            curve = new AnimationCurve()
        };
    }
}

public enum TransitionType
{
    Linear,
    Curve
}