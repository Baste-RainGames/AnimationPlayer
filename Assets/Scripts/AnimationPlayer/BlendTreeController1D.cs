using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    public class BlendTreeController1D
    {
        private readonly Action<float> UpdateValueOnMainController;

        private AnimationMixerPlayable mixer;
        private readonly float[] thresholds;
        private float lastValue;
        public float CurrentValue => lastValue;

        public BlendTreeController1D(AnimationMixerPlayable mixer, float[] thresholds, Action<float> UpdateValueOnMainController)
        {
            for (int i = 0; i < thresholds.Length - 2; i++)
                //@TODO: should reorder these!
                if (thresholds[i] >= thresholds[i + 1])
                    throw new UnityException($"The thresholds on the blend tree should be be strictly increasing!");

            this.mixer = mixer;
            this.thresholds = thresholds;
            this.UpdateValueOnMainController = UpdateValueOnMainController;
        }

        public void SetValue(float value)
        {
            if (value == lastValue)
                return;
            UpdateValueOnMainController(value);
            lastValue = value;

            int idxOfLastLowerThanVal = -1;
            for (int i = 0; i < thresholds.Length; i++)
            {
                var threshold = thresholds[i];
                if (threshold <= value)
                    idxOfLastLowerThanVal = i;
                else
                    break;
            }

            int idxBefore = idxOfLastLowerThanVal;
            int idxAfter = idxOfLastLowerThanVal + 1;

            float fractionTowardsAfter;
            if (idxBefore == -1)
                fractionTowardsAfter = 1f; //now after is 0
            else if (idxAfter == thresholds.Length)
                fractionTowardsAfter = 0f; //now before is the last
            else
            {
                var range = (thresholds[idxAfter] - thresholds[idxBefore]);
                var distFromStart = (value - thresholds[idxBefore]);
                fractionTowardsAfter = distFromStart / range;
            }

            for (int i = 0; i < thresholds.Length; i++)
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
        }

        public float GetMinThreshold() => thresholds[0];
        public float GetMaxThreshold() => thresholds[thresholds.Length - 1];
    }
}