using System;
using System.Collections.Generic;
using UnityEngine;

namespace Animation_Player
{
    /// <summary>
    /// Information about how to transition between two given states
    /// </summary>
    [Serializable]
    public class StateTransition
    {
        [SerializeField]
        private SerializedGUID fromStateGUID;
        [SerializeField]
        private SerializedGUID toStateGUID;

        public TransitionData transitionData;

        private AnimationState fromState, toState;
        public AnimationState FromState
        {
            get => fromState;
            set
            {
                fromState = value;
                fromStateGUID = fromState.GUID;
            }
        }

        public AnimationState ToState
        {
            get => toState;
            set
            {
                toState = value;
                toStateGUID = toState?.GUID ?? SerializedGUID.Empty;
            }
        }

        public void FetchStates(List<AnimationState> allStates)
        {
            fromState = allStates.Find(state => state.GUID == fromStateGUID);
            toState = allStates.Find(state => state.GUID == toStateGUID);
        }
    }

    /// <summary>
    /// Type of the transition
    /// </summary>
    public enum TransitionType
    {
        /// <summary>
        /// Use the transition defined for the two states being moved between, or the AnimationPlayer's default transition if no such transition
        /// is defined.
        /// </summary>
        UseDefined,
        /// <summary>
        /// Use a linear transition with a certain duration
        /// </summary>
        Linear,
        /// <summary>
        /// Use an AnimationCurve for the transition.
        /// </summary>
        Curve
    }

    /// <summary>
    /// Information about how the AnimationPlayer should transition from one state to another
    /// </summary>
    [Serializable]
    public struct TransitionData
    {
        public TransitionType type;
        public float duration;
        public AnimationCurve curve;

        public static TransitionData UseDefined() => default; // default for TransitionType is UseDefined.

        public static TransitionData Linear(float duration)
        {
            return new TransitionData
            {
                duration = Mathf.Max(0f, duration),
                type = TransitionType.Linear,
                curve = new AnimationCurve()
            };
        }

        public static TransitionData FromCurve(AnimationCurve curve)
        {
            return new TransitionData
            {
                duration = curve.Duration(),
                type = TransitionType.Curve,
                curve = curve
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