using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
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

    public AnimationMixerPlayable mixer { get; private set; }
    public int currentPlayedClip { get; private set; }

    //blend info:
    private bool blending;
    private TransitionData blendTransitionData;
    private float blendStartTime;
    private bool[] activeWhenBlendStarted;
    private float[] valueWhenBlendStarted;

    //transitionLookup[a, b] contains the index of the transition from a to b in transitions
    private int[,] transitionLookup;

    public void InitializeSelf(PlayableGraph graph)
    {
        mixer = AnimationMixerPlayable.Create(graph, states.Count, false);
        mixer.SetInputWeight(startClip, 1f);
        currentPlayedClip = startClip;

        // Add the clips to the graph
        for (int i = 0; i < states.Count; i++)
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, states[i].clip);
            clipPlayable.SetSpeed(states[i].speed);
            graph.Connect(clipPlayable, 0, mixer, i);
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
                Debug.LogError($"Got a transition from state number {transition.fromState} to state number {transition.toState}, but there's {states.Count} states!");
                continue;
            }
            
            if (transitionLookup[transition.fromState, transition.toState] != -1)
                Debug.LogWarning("Found two transitions from " + states[transition.fromState] + " to " + states[transition.toState]);
            
            
            transitionLookup[transition.fromState, transition.toState] = i;
        }
    }

    public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
    {
        graph.Connect(mixer, 0, layerMixer, layerIndex);

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
                mixer.SetInputWeight(i, i == clip ? 1f : 0f);
            }
            currentPlayedClip = clip;
            blending = false;
        }
        else if (clip != currentPlayedClip)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var currentMixVal = mixer.GetInputWeight(i);
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
                mixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
            }
        }

        if (lerpVal < 1)
            return;

        blending = false;
    }

    public float GetClipWeight(int clip)
    {
        Debug.Assert(clip >= 0 && clip < states.Count,
            "Trying to get the clip weight for {clip}, which is out of bounds! There are {clips.Count} clips!");
        return mixer.GetInputWeight(clip);
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
}

public enum AnimationLayerType
{
    Override,
    Additive
}