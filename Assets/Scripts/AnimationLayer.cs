using System.Collections.Generic;
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
    public int startClip;

    [Header("Layer blending data")]
    public float startWeight;
    public AvatarMask mask;
    public AnimationLayerType type = AnimationLayerType.Override;

    public AnimationMixerPlayable layerMixer { get; private set; }
    public int currentPlayedClip { get; private set; }

    //blend info:
    private bool blending;
    private TransitionData blendTransitionData;
    private float blendStartTime;
    private bool[] activeWhenBlendStarted;
    private float[] valueWhenBlendStarted;

    //transitionLookup[a, b] contains the index of the transition from a to b in transitions
    private int[,] transitionLookup;
    private int layerIndexForDebug;

    //@TODO: string key: is slow
    private Dictionary<string, float> blendVars = new Dictionary<string, float>();
    
    private Dictionary<string, AnimationMixerPlayable> varToMixer = new Dictionary<string, AnimationMixerPlayable>();

    public void InitializeSelf(PlayableGraph graph, int layerIndexForDebug)
    {
        this.layerIndexForDebug = layerIndexForDebug;
        if (states.Count == 0)
            return;
        
        layerMixer = AnimationMixerPlayable.Create(graph, states.Count, false);
        layerMixer.SetInputWeight(startClip, 1f);
        currentPlayedClip = startClip;

        // Add the clips to the graph
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.type == AnimationStateType.SingleClip)
            {
                var clipPlayable = AnimationClipPlayable.Create(graph, state.clip);
                clipPlayable.SetSpeed(state.speed);
                graph.Connect(clipPlayable, 0, layerMixer, i);
            }
            else if (state.type == AnimationStateType.BlendTree1D)
            {
                var treeMixer = AnimationMixerPlayable.Create(graph, state.blendTree.Count, true);
                varToMixer[state.blendVariable] = treeMixer;
                for (int j = 0; j < state.blendTree.Count; j++)
                {
                    var clipPlayable = AnimationClipPlayable.Create(graph, state.blendTree[j].clip);
                    clipPlayable.SetSpeed(state.speed);
                    graph.Connect(clipPlayable, 0, treeMixer, j);
                }
                treeMixer.SetInputWeight(0, 1f);
                graph.Connect(treeMixer, 0, layerMixer, i);
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
        graph.Connect(this.layerMixer, 0, layerMixer, layerIndex);

        layerMixer.SetInputWeight(layerIndex, startWeight);
        layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
        if (mask != null)
            layerMixer.SetLayerMaskFromAvatarMask((uint) layerIndex, mask);
    }

    public void PlayUsingExternalTransition(int clip, TransitionData transitionData)
    {
        Play(clip, transitionData);
    }

    public void PlayUsingInternalTransition(int clip, TransitionData defaultTransition)
    {
        var transitionToUse = transitionLookup[currentPlayedClip, clip];
        var transition = transitionToUse == -1 ? defaultTransition : transitions[transitionToUse].transitionData;
        Play(clip, transition);
    }

    private void Play(int clip, TransitionData transitionData)
    {
        Debug.Assert(clip >= 0 && clip < states.Count,
                     $"Trying to play out of bounds clip {clip}! There are {states.Count} clips in the animation player");
        Debug.Assert(transitionData.type != TransitionType.Curve || transitionData.curve != null,
                     "Trying to play an animationCurve based transition, but the transition curve is null!");

        if (transitionData.duration <= 0f)
        {
            for (int i = 0; i < states.Count; i++)
            {
                layerMixer.SetInputWeight(i, i == clip ? 1f : 0f);
            }
            currentPlayedClip = clip;
            blending = false;
        }
        else if (clip != currentPlayedClip)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var currentMixVal = layerMixer.GetInputWeight(i);
                activeWhenBlendStarted[i] = currentMixVal > 0f;
                valueWhenBlendStarted[i] = currentMixVal;
            }

            blending = true;
            currentPlayedClip = clip;
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
            var isTargetClip = i == currentPlayedClip;
            if (isTargetClip || activeWhenBlendStarted[i])
            {
                var target = isTargetClip ? 1f : 0f;
                layerMixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
            }
        }

        if (lerpVal < 1)
            return;

        blending = false;
    }

    public float GetClipWeight(int clip)
    {
        Debug.Assert(clip >= 0 && clip < states.Count,
                     $"Trying to get the clip weight for {clip}, which is out of bounds! There are {states.Count} clips!");
        return layerMixer.GetInputWeight(clip);
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
        Assert.IsTrue(varToMixer.ContainsKey(var), 
                      $"Trying to set the blend var {var} on layer {layerIndexForDebug} but no blend tree exists with that blend var!");
        
        blendVars[var] = value;
        var treeMixer = varToMixer[var];
        treeMixer.SetInputWeight(0, 1f - value);
        treeMixer.SetInputWeight(1, value);
    }

    public float GetBlendVar(string var)
    {
        float result = 0f;
        blendVars.TryGetValue(var, out result);
        return result;
    }
}

public enum AnimationLayerType
{
    Override,
    Additive
}