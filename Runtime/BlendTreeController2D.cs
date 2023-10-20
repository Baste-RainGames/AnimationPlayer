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

    private float changeThreshold1Pos, changeThreshold2Pos;
    private float changeThreshold1Neg, changeThreshold2Neg;

    /* The currently set value is whatever value was last set for the controller.
     * The currently blend vector is the value actually used, and is only updated when the current set value has moved more than 1% of the
     * total range of the blend var threshold.
     */
    private float currentSetValue1, currentSetValue2;
    private Vector2 currentBlendVector;

    private readonly AnimationMixerPlayable treeMixer;

    private readonly Vector2[] thresholds;
    private readonly float[] motionInfluences;

    private float[,] squareDistanceBetweenThresholds;
    private Vector2[,] vectorsBetweenThresholds;

    private bool shouldRecalculate;

    public BlendTreeController2D(string blendVar1, string blendVar2, AnimationMixerPlayable treeMixer, int numClips)
    {
        this.blendVar1 = blendVar1;
        this.blendVar2 = blendVar2;
        this.treeMixer = treeMixer;
        thresholds = new Vector2[numClips];
        motionInfluences = new float[numClips];
        squareDistanceBetweenThresholds = new float[numClips, numClips];
        vectorsBetweenThresholds = new Vector2[numClips, numClips];
    }

    public void AddThresholdsForClip(int clipIdx, float threshold, float threshold2)
    {
        thresholds[clipIdx] = new (threshold, threshold2);

        minVal1 = Mathf.Min(threshold, minVal1);
        minVal2 = Mathf.Min(threshold2, minVal2);
        maxVal1 = Mathf.Max(threshold, maxVal1);
        maxVal2 = Mathf.Max(threshold2, maxVal2);

        const float thresholdForChange = .01f;
        changeThreshold1Pos = Mathf.Abs(maxVal1 - minVal1) * thresholdForChange;
        changeThreshold2Pos = Mathf.Abs(maxVal2 - minVal2) * thresholdForChange;
        changeThreshold1Neg = -changeThreshold1Pos;
        changeThreshold2Neg = -changeThreshold2Pos;
    }

    public void OnAllThresholdsAdded()
    {
        for (int i = 0; i < thresholds.Length; i++)
        for (int j = i + 1; j < thresholds.Length; j++)
        {
            var fromJToI = thresholds[i] - thresholds[j];
            var fromIToJ = -fromJToI;
            vectorsBetweenThresholds[i, j] = fromIToJ;
            vectorsBetweenThresholds[j, i] = fromJToI;

            var squareDist = Mathf.Pow(fromJToI.magnitude, 2);
            squareDistanceBetweenThresholds[i, j] = squareDistanceBetweenThresholds[j, i] = squareDist;
        }
    }

    public void SetValue(string blendVar, float value)
    {
        if (blendVar == blendVar1)
            SetValue1(value);
        else if (blendVar == blendVar2)
            SetValue2(value);
        else
            Debug.LogError($"Setting blendVar {blendVar}, but that's unknown! Known blend vars are: \"{blendVar1}\" and \"{blendVar2}\"");
    }

    public void SetInitialValues(float value1, float value2)
    {
        currentBlendVector = new (float.MaxValue, float.MaxValue);
        SetValue1(value1);
        SetValue2(value2);
    }

    public void SetValue1(float value)
    {
        currentSetValue1 = Mathf.Clamp(value, minVal1, maxVal1);

        var diff = currentSetValue1 - currentBlendVector.x;
        shouldRecalculate |= diff < changeThreshold1Neg || diff > changeThreshold1Pos;
    }

    public void SetValue2(float value)
    {
        currentSetValue2 = Mathf.Clamp(value, minVal2, maxVal2);

        var diff = currentSetValue2 - currentBlendVector.y;
        shouldRecalculate |= diff < changeThreshold2Neg || diff > changeThreshold2Pos;
    }

    public void Update()
    {
        if (shouldRecalculate)
        {
            currentBlendVector.x = currentSetValue1;
            currentBlendVector.y = currentSetValue2;
            Recalculate();
            shouldRecalculate = false;
        }
    }

    private void Recalculate()
    {
        //Recalculate weights, based on Rune Skovbo Johansen's thesis (Docs/rune_skovbo_johansen_thesis.pdf)
        //For now, using the version without polar coordinates
        //@TODO: use the polar coordinate version, looks better
        float influenceSum = 0f;
        for (int i = 0; i < thresholds.Length; i++)
        {
            var influence = GetInfluenceForPoint(currentBlendVector, i);
            motionInfluences[i] = influence;
            influenceSum += influence;
        }

        for (int i = 0; i < thresholds.Length; i++)
        {
            treeMixer.SetInputWeight(i, motionInfluences[i] / influenceSum);
        }
    }

    //See chapter 6.3 in Docs/rune_skovbo_johansen_thesis.pdf
    private float GetInfluenceForPoint(Vector2 inputPoint, int referencePointIdx)
    {
        if (thresholds.Length == 1)
            return 1f;

        var minVal = Mathf.Infinity;
        for (int i = 0; i < thresholds.Length; i++)
        {
            // Note that we will get infinity values if there are two motions with the same thresholdPoint.
            // But having two motions at the same point should error further up, as it's kinda meaningless.
            if (i == referencePointIdx)
                continue;
            var val = WeightFunc(i, inputPoint, referencePointIdx);
            if (val < minVal)
                minVal = val;

            //This is not mentioned in the thesis, but seems to be neccessary.
            if (minVal < 0f)
                minVal = 0f;
        }

        return minVal;
    }

    private float WeightFunc(int idx, Vector2 inputPoint, int referencePointIdx)
    {
        var toPointAtIdx = vectorsBetweenThresholds[referencePointIdx, idx];
        var dotProd = Vector2.Dot(inputPoint - thresholds[referencePointIdx], toPointAtIdx);
        var magSqr = squareDistanceBetweenThresholds[referencePointIdx, idx];

        var val = dotProd / magSqr;
        return 1f - val;
    }
}
}