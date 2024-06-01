using System;

using UnityEngine;

namespace Animation_Player
{
/// <summary>
/// Information about how to transition between two given states
/// </summary>
[Serializable]
public class StateTransition
{
    private const string DefaultName = "Transition";

    public bool isDefault;
    public string name = DefaultName;

    [SerializeReference] public AnimationPlayerState fromState;
    [SerializeReference] public AnimationPlayerState toState;

    public TransitionData transitionData;
}

/// <summary>
/// Type of the transition
/// </summary>
public enum TransitionType
{
    // UserDefined = 0 was a bad idea.

    /// <summary>
    /// Use a linear transition with a certain duration
    /// </summary>
    Linear = 1,
    /// <summary>
    /// Use an AnimationCurve for the transition.
    /// </summary>
    Curve = 2,
    /// <summary>
    /// Use an Animation clip to blend between the states.
    /// </summary>
    Clip = 3,
}

/// <summary>
/// Information about how the AnimationPlayer should transition from one state to another
/// </summary>
[Serializable]
public struct TransitionData
{
    public TransitionType type;
    public float          duration;
    public double         timeOffsetIntoNewState;
    public AnimationCurve curve;
    public AnimationClip  clip;

    public static TransitionData Linear(float duration)
    {
        return new()
        {
            duration = Mathf.Max(0f, duration),
            type = TransitionType.Linear,
            curve = new (),
            clip = null
        };
    }

    public static TransitionData FromCurve(AnimationCurve curve)
    {
        return new()
        {
            duration = curve.Duration(),
            type = TransitionType.Curve,
            curve = curve,
            clip = null
        };
    }

    public static TransitionData Instant()
    {
        return new()
        {
            duration = 0f,
            type = TransitionType.Linear,
            curve = new (),
            clip = null
        };
    }

    public static TransitionData Clip(AnimationClip clip)
    {
        return new()
        {
            duration = 0f,
            type     = TransitionType.Clip,
            curve    = new (),
            clip     = clip
        };
    }

    public static bool operator ==(TransitionData a, TransitionData b)
    {
        if (a.type != b.type)
            return false;
        if (a.duration != b.duration)
            return false;
        if (a.type == TransitionType.Linear)
            return a.duration == b.duration;

        if (a.curve == null)
            return b.curve == null;
        if (b.curve == null)
            return false;

        return a.curve.Equals(b.curve);
    }

    public static bool operator !=(TransitionData a, TransitionData b)
    {
        return !(a == b);
    }

    public bool Equals(TransitionData other)
    {
        return this == other;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        return obj is TransitionData td && Equals(td);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = duration.GetHashCode();
            hashCode = (hashCode * 397) ^ (int) type;
            hashCode = (hashCode * 397) ^ (curve != null ? curve.GetHashCode() : 0);
            return hashCode;
        }
    }
}
}