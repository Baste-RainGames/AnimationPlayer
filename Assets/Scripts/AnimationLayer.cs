using System.Collections.Generic;
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
    public int startState;

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
    private readonly Dictionary<string, List<RuntimeBlendVarController>> varToBlendControllers = new Dictionary<string, List<RuntimeBlendVarController>>();

    public void InitializeSelf(PlayableGraph graph, int layerIndexForDebug)
    {
        this.layerIndexForDebug = layerIndexForDebug;
        if (states.Count == 0)
            return;

        runtimePlayables = new Playable[states.Count];

        stateMixer = AnimationMixerPlayable.Create(graph, states.Count, false);
        stateMixer.SetInputWeight(startState, 1f);
        currentPlayedState = startState;

        // Add the statess to the graph
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            stateNameToIdx[state.Name] = i;
            if (state.type == AnimationStateType.SingleClip)
            {
                var clipPlayable = AnimationClipPlayable.Create(graph, state.clip);
                runtimePlayables[i] = clipPlayable;
                clipPlayable.SetSpeed(state.speed);
                graph.Connect(clipPlayable, 0, stateMixer, i);
            }
            else if (state.type == AnimationStateType.BlendTree1D)
            {
                var treeMixer = AnimationMixerPlayable.Create(graph, state.blendTree.Count, true);
                runtimePlayables[i] = treeMixer;
                float[] thresholds = new float[state.blendTree.Count];

                for (int j = 0; j < state.blendTree.Count; j++)
                {
                    var blendTreeEntry = state.blendTree[j];
                    var clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                    clipPlayable.SetSpeed(state.speed);
                    graph.Connect(clipPlayable, 0, treeMixer, j);
                    thresholds[j] = blendTreeEntry.threshold;
                }

                graph.Connect(treeMixer, 0, stateMixer, i);

                if (state.blendTree.Count > 0)
                {
                    treeMixer.SetInputWeight(0, 1f);
                    if (!varToBlendControllers.ContainsKey(state.blendVariable))
                        varToBlendControllers[state.blendVariable] = new List<RuntimeBlendVarController>();
                    varToBlendControllers[state.blendVariable].Add(new RuntimeBlendVarController(treeMixer, thresholds));
                }
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
        Assert.IsTrue(varToBlendControllers.ContainsKey(var),
            $"Trying to set the blend var {var} on layer {layerIndexForDebug} but no blend tree exists with that blend var!");

        blendVars[var] = value;
        var blendControllers = varToBlendControllers[var];
        foreach (var controller in blendControllers)
        {
            controller.SetValue(value);
        }
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

    private class RuntimeBlendVarController
    {
        private AnimationMixerPlayable mixer;
        private readonly float[] thresholds;

        public RuntimeBlendVarController(AnimationMixerPlayable mixer, float[] thresholds)
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
}

public enum AnimationLayerType
{
    Override,
    Additive
}