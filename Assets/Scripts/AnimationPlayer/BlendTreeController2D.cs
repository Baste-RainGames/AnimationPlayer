using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    public class BlendTreeController2D
    {
        public readonly string blendVar1;
        public readonly string blendVar2;
        private float minVal1, minVal2, maxVal1, maxVal2;
        private Vector2 currentBlendVector;

        private readonly AnimationMixerPlayable treeMixer;
        private readonly BlendTree2DMotion[] motions;
        private readonly float[] motionInfluences;

        private Action<float> UpdateValue1OnMainController;
        private Action<float> UpdateValue2OnMainController;

        public BlendTreeController2D(string blendVar1, string blendVar2, AnimationMixerPlayable treeMixer, int numClips,
                                     Action<float> UpdateValue1OnMainController, Action<float> UpdateValue2OnMainController)
        {
            this.blendVar1 = blendVar1;
            this.blendVar2 = blendVar2;
            this.treeMixer = treeMixer;
            motions = new BlendTree2DMotion[numClips];
            motionInfluences = new float[numClips];
            this.UpdateValue1OnMainController = UpdateValue1OnMainController;
            this.UpdateValue2OnMainController = UpdateValue2OnMainController;
        }

        public void Add(int clipIdx, float threshold, float threshold2)
        {
            motions[clipIdx] = new BlendTree2DMotion(new Vector2(threshold, threshold2));

            minVal1 = Mathf.Min(threshold, minVal1);
            minVal2 = Mathf.Min(threshold2, minVal2);
            maxVal1 = Mathf.Max(threshold, maxVal1);
            maxVal2 = Mathf.Max(threshold2, maxVal2);
        }

        public void SetValue1(float value)
        {
            var last = currentBlendVector.x;
            currentBlendVector.x = Mathf.Clamp(value, minVal1, maxVal1);
            if (last != currentBlendVector.x)
            {
                UpdateValue1OnMainController(value);
                Recalculate();
            }
        }

        public void SetValue2(float value)
        {
            var last = currentBlendVector.y;
            currentBlendVector.y = Mathf.Clamp(value, minVal2, maxVal2);
            if (last != currentBlendVector.y)
                UpdateValue2OnMainController(value);
            Recalculate();
        }

        public void SetValue(string blendVar, float value)
        {
            if (blendVar == blendVar1)
                SetValue1(value);
            else
                SetValue2(value);
        }

        private void Recalculate()
        {
            //Recalculate weights, based on Rune Skovbo Johansen's thesis (Docs/rune_skovbo_johansen_thesis.pdf)
            //For now, using the version without polar coordinates
            //@TODO: use the polar coordinate version, looks better
            float influenceSum = 0f;
            for (int i = 0; i < motions.Length; i++)
            {
                var influence = GetInfluenceForPoint(currentBlendVector, i);
                motionInfluences[i] = influence;
                influenceSum += influence;
            }

            for (int i = 0; i < motions.Length; i++)
            {
                treeMixer.SetInputWeight(i, motionInfluences[i] / influenceSum);
            }
        }

        //See chapter 6.3 in Docs/rune_skovbo_johansen_thesis.pdf
        private float GetInfluenceForPoint(Vector2 inputPoint, int referencePointIdx)
        {
            if (motions.Length == 1)
                return 1f;

            Vector2 referencePoint = motions[referencePointIdx].thresholdPoint;

            var minVal = Mathf.Infinity;
            for (int i = 0; i < motions.Length; i++)
            {
                // Note that we will get infinity values if there are two motions with the same thresholdPoint.
                // But having two motions at the same point should error further up, as it's kinda meaningless.
                if (i == referencePointIdx)
                    continue;
                var val = WeightFunc(i, inputPoint, referencePoint);
                if (val < minVal)
                    minVal = val;

                //This is not mentioned in the thesis, but seems to be neccessary.
                if (minVal < 0f)
                    minVal = 0f;
            }

            return minVal;
        }

        private float WeightFunc(int idx, Vector2 inputPoint, Vector2 referencePoint)
        {
            var toPointAtIdx = motions[idx].thresholdPoint - referencePoint;

            var dotProd = Vector2.Dot(inputPoint - referencePoint, toPointAtIdx);
            var magSqr = Mathf.Pow(toPointAtIdx.magnitude, 2);

            var val = dotProd / magSqr;
            return 1f - val;
        }

        private class BlendTree2DMotion
        {
            public readonly Vector2 thresholdPoint;

            public BlendTree2DMotion(Vector2 thresholdPoint)
            {
                this.thresholdPoint = thresholdPoint;
            }
        }
    }
}