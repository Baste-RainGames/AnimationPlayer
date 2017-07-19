using System;
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
    public StateTransition[] transitions;
    public int startClip;
    
    [Header("Layer blending data")]
    public float startWeight;
    public AvatarMask mask;
    public AnimationLayerType type = AnimationLayerType.Override;

    public AnimationMixerPlayable mixer { get; private set; }
    public int currentPlayedClip { get; private set; }

    //blend info:
    private bool blending;
    private int blendingFromClip; //for debug
    private AnimTransition blendTransition;
    private float blendStartTime;
    private bool[] activeWhenBlendStarted;
    private float[] valueWhenBlendStarted;

    public void InitializeSelf(PlayableGraph graph)
    {
        mixer = AnimationMixerPlayable.Create(graph, states.Count, false);
        mixer.SetInputWeight(startClip, 1f);
        currentPlayedClip = startClip;

        // Add the clips to the graph
        for (int i = 0; i < states.Count; i++)
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, states[i].clip);
            graph.Connect(clipPlayable, 0, mixer, i);
        }

        activeWhenBlendStarted = new bool[states.Count];
        valueWhenBlendStarted = new float[states.Count];
    }

    public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
    {
        Debug.Log("connects on " + layerIndex);
        graph.Connect(mixer, 0, layerMixer, layerIndex);

        layerMixer.SetInputWeight(layerIndex, startWeight);
        layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
        if (mask != null)
            layerMixer.SetLayerMaskFromAvatarMask((uint) layerIndex, mask);
    }

    public void Play(int clip, AnimTransition transition)
    {
        Debug.Assert(clip >= 0 && clip < states.Count,
                     $"Trying to play out of bounds clip {clip}! There are {states.Count} clips in the animation player");
        Debug.Assert(transition.type != TransitionType.Curve || transition.curve != null,
                     "Trying to play an animationCurve based transition, but the transition curve is null!");

        if (transition.duration <= 0f)
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
            blendingFromClip = currentPlayedClip;
            currentPlayedClip = clip;
            blendTransition = transition;
            blendStartTime = Time.time;
        }
    }

    public void Update()
    {
        if (!blending)
            return;

        var lerpVal = (Time.time - blendStartTime) / blendTransition.duration;
        if (blendTransition.type == TransitionType.Curve)
        {
            lerpVal = blendTransition.curve.Evaluate(lerpVal);
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

    [Serializable]
    public class StateTransition
    {
        public int fromState, toState;
        public AnimTransition transition;
    }
}

public enum AnimationLayerType
{
    Override,
    Additive
}