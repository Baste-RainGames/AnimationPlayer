using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

[System.Serializable]
public class AnimationLayer
{
    [Header("Animation data")]
    public List<AnimationState> states;
    public List<StateTransition> transitions;
    //public int startState;

    [Header("Layer blending data")]
    public float startWeight;
    public AvatarMask mask;
    public AnimationLayerType type = AnimationLayerType.Override;

    public AnimationMixerPlayable stateMixer { get; private set; }
    public int currentPlayedState { get; private set; }

    //blend info:
    private bool blending;
    private TransitionData blendTransitionData;
    private float blendStartTime;
    private bool[] activeWhenBlendStarted;
    private float[] valueWhenBlendStarted;

    //transitionLookup[a, b] contains the index of the transition from a to b in transitions
    private int[,] transitionLookup;
    private int layerIndexForDebug;
    private Playable[] runtimePlayables;

    private readonly Dictionary<string, int> stateNameToIdx = new Dictionary<string, int>();

    //@TODO: string key is slow
    private readonly Dictionary<string, float> blendVars = new Dictionary<string, float>();
    private readonly Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers = new Dictionary<string, List<BlendTreeController1D>>();
    private readonly Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers = new Dictionary<string, List<BlendTreeController2D>>();

    public void InitializeSelf(PlayableGraph graph, int layerIndexForDebug)
    {
        this.layerIndexForDebug = layerIndexForDebug;
        if (states.Count == 0)
            return;

        runtimePlayables = new Playable[states.Count];

        stateMixer = AnimationMixerPlayable.Create(graph, states.Count, false);
        stateMixer.SetInputWeight(0, 1f);
        currentPlayedState = 0;

        // Add the statess to the graph
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            stateNameToIdx[state.Name] = i;
            switch (state.type)
            {
                case AnimationStateType.SingleClip:
                    var clipPlayable = AnimationClipPlayable.Create(graph, state.clip);
                    runtimePlayables[i] = clipPlayable;
                    clipPlayable.SetSpeed(state.speed);
                    graph.Connect(clipPlayable, 0, stateMixer, i);
                    break;
                case AnimationStateType.BlendTree1D:
                    var treeMixer = AnimationMixerPlayable.Create(graph, state.blendTree.Count, true);
                    runtimePlayables[i] = treeMixer;
                    if (state.blendTree.Count == 0)
                        break;

                    float[] thresholds = new float[state.blendTree.Count];

                    for (int j = 0; j < state.blendTree.Count; j++)
                    {
                        var blendTreeEntry = state.blendTree[j];
                        clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                        clipPlayable.SetSpeed(state.speed);
                        graph.Connect(clipPlayable, 0, treeMixer, j);
                        thresholds[j] = blendTreeEntry.threshold;
                    }

                    graph.Connect(treeMixer, 0, stateMixer, i);

                    treeMixer.SetInputWeight(0, 1f);
                    varTo1DBlendControllers.GetOrAdd(state.blendVariable).Add(new BlendTreeController1D(treeMixer, thresholds));

                    break;
                case AnimationStateType.BlendTree2D:
                    treeMixer = AnimationMixerPlayable.Create(graph, state.blendTree.Count, true);
                    runtimePlayables[i] = treeMixer;
                    if (state.blendTree.Count == 0)
                        break;

                    var controller = new BlendTreeController2D(state.blendVariable, state.blendVariable2, treeMixer, state.blendTree.Count);
                    varTo2DBlendControllers.GetOrAdd(state.blendVariable).Add(controller);
                    varTo2DBlendControllers.GetOrAdd(state.blendVariable2).Add(controller);

                    for (int j = 0; j < state.blendTree.Count; j++)
                    {
                        var blendTreeEntry = state.blendTree[j];
                        clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                        clipPlayable.SetSpeed(state.speed);
                        graph.Connect(clipPlayable, 0, treeMixer, j);

                        controller.Add(j, blendTreeEntry.threshold, blendTreeEntry.threshold2);
                    }

                    graph.Connect(treeMixer, 0, stateMixer, i);
                    treeMixer.SetInputWeight(0, 1f);

                    break;
            }
        }

        activeWhenBlendStarted = new bool[states.Count];
        valueWhenBlendStarted = new float[states.Count];

        transitionLookup = new int[states.Count, states.Count];
        for (int i = 0; i < states.Count; i++)
        for (int j = 0; j < states.Count; j++)
            transitionLookup[i, j] = -1;

        for (var i = 0; i < transitions.Count; i++)
        {
            var transition = transitions[i];
            if (transition.fromState < 0 || transition.toState < 0 || transition.fromState > states.Count - 1 || transition.toState > states.Count - 1)
            {
                Debug.LogError(
                    $"Got a transition from state number {transition.fromState} to state number {transition.toState}, but there's {states.Count} states!");
                continue;
            }

            if (transitionLookup[transition.fromState, transition.toState] != -1)
                Debug.LogWarning("Found two transitions from " + states[transition.fromState] + " to " + states[transition.toState]);

            transitionLookup[transition.fromState, transition.toState] = i;
        }
    }

    public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
    {
        graph.Connect(this.stateMixer, 0, layerMixer, layerIndex);

        layerMixer.SetInputWeight(layerIndex, startWeight);
        layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
        if (mask != null)
            layerMixer.SetLayerMaskFromAvatarMask((uint) layerIndex, mask);
    }

    public void PlayUsingExternalTransition(int state, TransitionData transitionData)
    {
        Play(state, transitionData);
    }

    public void PlayUsingInternalTransition(int state, TransitionData defaultTransition)
    {
        var transitionToUse = transitionLookup[currentPlayedState, state];
        var transition = transitionToUse == -1 ? defaultTransition : transitions[transitionToUse].transitionData;
        Play(state, transition);
    }

    private void Play(int state, TransitionData transitionData)
    {
        Debug.Assert(state >= 0 && state < states.Count,
            $"Trying to play out of bounds clip {state}! There are {states.Count} clips in the animation player");
        Debug.Assert(transitionData.type != TransitionType.Curve || transitionData.curve != null,
            "Trying to play an animationCurve based transition, but the transition curve is null!");

        var isCurrentlyPlaying = stateMixer.GetInputWeight(state) > 0f;
        if (!isCurrentlyPlaying)
        {
            runtimePlayables[state].SetTime(0f);
        }

        if (transitionData.duration <= 0f)
        {
            for (int i = 0; i < states.Count; i++)
            {
                stateMixer.SetInputWeight(i, i == state ? 1f : 0f);
            }
            currentPlayedState = state;
            blending = false;
        }
        else if (state != currentPlayedState)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var currentMixVal = stateMixer.GetInputWeight(i);
                activeWhenBlendStarted[i] = currentMixVal > 0f;
                valueWhenBlendStarted[i] = currentMixVal;
            }

            blending = true;
            currentPlayedState = state;
            blendTransitionData = transitionData;
            blendStartTime = Time.time;
        }
    }

    public void Update()
    {
        if (!blending)
            return;

        var lerpVal = (Time.time - blendStartTime) / blendTransitionData.duration;
        if (blendTransitionData.type == TransitionType.Curve)
        {
            lerpVal = blendTransitionData.curve.Evaluate(lerpVal);
        }

        for (int i = 0; i < states.Count; i++)
        {
            var isTargetClip = i == currentPlayedState;
            if (isTargetClip || activeWhenBlendStarted[i])
            {
                var target = isTargetClip ? 1f : 0f;
                stateMixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
            }
        }

        if (lerpVal < 1)
            return;

        blending = false;
    }

    public float GetStateWeight(int state)
    {
        Debug.Assert(state >= 0 && state < states.Count,
            $"Trying to get the state weight for {state}, which is out of bounds! There are {states.Count} states!");
        return stateMixer.GetInputWeight(state);
    }

    public int GetStateIdx(string stateName)
    {
        int idx;
        if (stateNameToIdx.TryGetValue(stateName, out idx))
            return idx;
        return -1;
    }

    public bool IsBlending()
    {
        return blending;
    }

    public static AnimationLayer CreateLayer()
    {
        var layer = new AnimationLayer
        {
            states = new List<AnimationState>(),
            transitions = new List<StateTransition>(),
            startWeight = 1f
        };
        return layer;
    }

    public void SetBlendVar(string var, float value)
    {
        Assert.IsTrue(varTo1DBlendControllers.ContainsKey(var) || varTo2DBlendControllers.ContainsKey(var),
            $"Trying to set the blend var {var} on layer {layerIndexForDebug} but no blend tree exists with that blend var!");

        blendVars[var] = value;

        List<BlendTreeController1D> blendControllers1D;
        if (varTo1DBlendControllers.TryGetValue(var, out blendControllers1D))
            foreach (var controller in blendControllers1D)
                controller.SetValue(value);
        
        List<BlendTreeController2D> blendControllers2D;
        if (varTo2DBlendControllers.TryGetValue(var, out blendControllers2D))
            foreach (var controller in blendControllers2D)
                controller.SetValue(var, value);
        
        /*var blendControllers = varTo1DBlendControllers[var];
        foreach (var controller in blendControllers)
            controller.SetValue(value);

        var blendControllers2D = varTo2DBlendControllers[var];
        foreach (var controller in blendControllers2D)
            controller.SetValue(var, value);*/
    }

    public float GetBlendVar(string var)
    {
        float result = 0f;
        blendVars.TryGetValue(var, out result);
        return result;
    }

    public void AddAllPlayingStatesTo(List<AnimationState> results)
    {
        results.Add(states[currentPlayedState]);

        for (var i = 0; i < states.Count; i++)
        {
            if (i == currentPlayedState)
                return;
            var state = states[i];
            if (stateMixer.GetInputWeight(i) > 0f)
            {
                results.Add(state);
            }
        }
    }

    private class BlendTreeController1D
    {
        private AnimationMixerPlayable mixer;
        private readonly float[] thresholds;

        public BlendTreeController1D(AnimationMixerPlayable mixer, float[] thresholds)
        {
            for (int i = 0; i < thresholds.Length - 2; i++)
                //@TODO: should reorder these!
                Assert.IsTrue(thresholds[i] < thresholds[i + 1], "The thresholds should be strictly increasing!");

            this.mixer = mixer;
            this.thresholds = thresholds;
        }

        public void SetValue(float value)
        {
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
    }

    private class BlendTreeController2D
    {
        private readonly string blendVar1;
        private readonly string blendVar2;
        private float minVal1, minVal2, maxVal1, maxVal2;
        private Vector2 currentBlendVector;

        private readonly AnimationMixerPlayable treeMixer;
        private readonly BlendTree2DMotion[] motions;
        private readonly float[] motionInfluences;

        public BlendTreeController2D(string blendVar1, string blendVar2, AnimationMixerPlayable treeMixer, int numClips)
        {
            this.blendVar1 = blendVar1;
            this.blendVar2 = blendVar2;
            this.treeMixer = treeMixer;
            motions = new BlendTree2DMotion[numClips];
            motionInfluences = new float[numClips];
        }

        public void Add(int clipIdx, float threshold, float threshold2)
        {
            motions[clipIdx] = new BlendTree2DMotion(new Vector2(threshold, threshold2));

            minVal1 = Mathf.Min(threshold, minVal1);
            minVal2 = Mathf.Min(threshold2, minVal2);
            maxVal1 = Mathf.Max(threshold, maxVal1);
            maxVal2 = Mathf.Max(threshold2, maxVal2);
        }

        public void SetValue(string blendVar, float value)
        {
            Debug.Assert(blendVar == blendVar1 || blendVar == blendVar2,
                $"Blend tree controller got the value for {blendVar}, but it only cares about {blendVar1} and {blendVar2}");

            if (blendVar == blendVar1)
                currentBlendVector.x = Mathf.Clamp(value, minVal1, maxVal1);
            else
                currentBlendVector.y = Mathf.Clamp(value, minVal2, maxVal2);
            
            //Recalculate weights, based on Rune Skovbo Johansen's thesis (http://runevision.com/thesis/rune_skovbo_johansen_thesis.pdf)
            //For now, using the very simplified, bad version from 6.2.1
            //@TODO: use the better solutions from the same chapter

            float influenceSum = 0f;
            for (int i = 0; i < motions.Length; i++)
            {
                var influence = Influence(currentBlendVector, motions[i].thresholdPoint);
                if (float.IsInfinity(influence))
                    influence = 10000f; //Lazy, but we're ditching this algorithm soon
                motionInfluences[i] = influence;
                influenceSum += influence;
            }

            for (int i = 0; i < motions.Length; i++)
            {
                treeMixer.SetInputWeight(i, motionInfluences[i] / influenceSum);
            }
        }

        //The "influence function" h(p) from the paper
        private float Influence(Vector2 inputPoint, Vector2 referencePoint)
        {
            return 1f / (Mathf.Pow(Vector2.Distance(inputPoint, referencePoint), 2f));
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

public enum AnimationLayerType
{
    Override,
    Additive
}