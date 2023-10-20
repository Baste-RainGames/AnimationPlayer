using System;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
public class BlendTreeController1D
{
    private readonly AnimationMixerPlayable mixer;
    private readonly BlendTreeData[] runtimeData;
    private readonly bool compensateForDifferentDurations;

    private float lastValue;

    public BlendTreeController1D(AnimationMixerPlayable mixer, AnimationClipPlayable[] playables, float[] thresholds, bool compensateForDifferentDurations)
    {
        if (thresholds.Length != playables.Length)
            throw new ("Thresholds and playables doesn't match!");
        for (int i = 0; i < thresholds.Length - 2; i++)
            if (thresholds[i] >= thresholds[i + 1])
                throw new ($"The thresholds on the blend tree should be be strictly increasing!");

        this.compensateForDifferentDurations = compensateForDifferentDurations;
        this.mixer = mixer;
        runtimeData = new BlendTreeData[thresholds.Length];
        for (int i = 0; i < runtimeData.Length; i++)
        {
            runtimeData[i] = new()
            {
                threshold = thresholds[i],
                playable = playables[i],
                duration = playables[i].GetAnimationClip().length
            };
        }

        if (compensateForDifferentDurations)
            CompensateForDurations(0, 0);
    }

    public void SetInitialValue(float value)
    {
        lastValue = float.MaxValue;
        SetValue(value);
    }

    public void SetValue(float value)
    {
        if (value == lastValue)
            return;
        lastValue = value;

        int idxOfLastLowerThanVal = -1;
        for (int i = 0; i < runtimeData.Length; i++)
        {
            var threshold = runtimeData[i].threshold;
            if (threshold <= value)
                idxOfLastLowerThanVal = i;
            else
                break;
        }

        int idxBefore = Mathf.Max(idxOfLastLowerThanVal, 0);
        int idxAfter  = Mathf.Min(idxOfLastLowerThanVal + 1, runtimeData.Length - 1);

        float fractionTowardsAfter;
        if (idxBefore == idxAfter) //first or last clip
        {
            fractionTowardsAfter = 0f;
        }
        else
        {
            var range = (runtimeData[idxAfter].threshold - runtimeData[idxBefore].threshold);
            var distFromStart = (value - runtimeData[idxBefore].threshold);
            fractionTowardsAfter = distFromStart / range;
        }

        for (int i = 0; i < runtimeData.Length; i++)
        {
            float inputWeight;

            if (i == idxBefore)
                inputWeight = 1f - fractionTowardsAfter;
            else if (i == idxAfter)
                inputWeight = fractionTowardsAfter;
            else
                inputWeight = 0f;

            mixer.SetInputWeight(i, inputWeight);
        }

        if (compensateForDifferentDurations)
            CompensateForDurations(idxBefore, idxAfter);
    }

    /// <summary>
    /// When blending between different clips that have different durations, it's often neccessary to speed the playables up or down so their durations
    /// match. Consider blending a walk and run animation with different lengths - unless the feet hit the ground at the same time every time the clips
    /// loop, the character's feet will be in a strange, in-between position.
    /// The way we handle this is to always have all of the clips - even ones with weight 0 - always playing at a speed such that their duration matches
    /// the currently played clip's duration.
    /// </summary>
    private void CompensateForDurations(int idxBefore, int idxAfter)
    {
        float durationBefore = runtimeData[idxBefore].duration;
        float durationAfter  = runtimeData[idxAfter ].duration;
        float loopDuration = Mathf.Lerp(durationBefore, durationAfter, mixer.GetInputWeight(idxAfter));

        for (int i = 0; i < runtimeData.Length; i++)
        {
            var clipDuration = runtimeData[i].duration;
            var requiredSpeed = clipDuration / loopDuration;
            runtimeData[i].playable.SetSpeed(requiredSpeed);
        }
    }

    public float GetMinThreshold() => runtimeData[0].threshold;
    public float GetMaxThreshold() => runtimeData[runtimeData.Length - 1].threshold;

    private struct BlendTreeData
    {
        public AnimationClipPlayable playable;
        public float duration;
        public float threshold;
//            public float defaultSpeed;
    }

    public void PlayableChanged(int index, AnimationClipPlayable newPlayable)
    {
        runtimeData[index].playable = newPlayable;
    }
}
}