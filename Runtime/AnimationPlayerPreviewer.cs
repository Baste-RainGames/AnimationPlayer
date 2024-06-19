#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UIElements;

namespace Animation_Player
{
public class AnimationPlayerPreviewer
{
    private AnimationPlayer animationPlayer;
    private PlayableGraph previewGraph;
    private Slider playbackSlider;
    private int layer;
    private int state;
    private float lastTime;

    public AnimationPlayerPreviewer(AnimationPlayer animationPlayer)
    {
        this.animationPlayer = animationPlayer;
    }

    public bool IsPreviewing => previewGraph.IsValid();
    public bool AutomaticPlayback { get; set; }

    public void StartPreview(int layer, int state, bool automaticPlayback, Slider playbackSlider, Slider blendVar1Slider, Slider blendVar2Slider)
    {
        this.playbackSlider = playbackSlider;
        AutomaticPlayback = automaticPlayback;
        this.layer = layer;
        this.state = state;

        animationPlayer.ExitPreview();
        animationPlayer.EnterPreview();
        var playedState = animationPlayer.Play(state, layer);
        previewGraph = animationPlayer.Graph;
        previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        
        if (playedState is BlendTree1D blendTree1D)
        {
            animationPlayer.SetBlendVar(blendTree1D.blendVariable, blendVar1Slider.value);
        }
        else if (playedState is BlendTree2D blendTree2D)
        {
            animationPlayer.SetBlendVar(blendTree2D.blendVariable,  blendVar1Slider.value);
            animationPlayer.SetBlendVar(blendTree2D.blendVariable2, blendVar2Slider.value);
        }
        
        lastTime = Time.realtimeSinceStartup;

        EditorApplication.update += Update;
    }

    public void Update()
    {
        if (AutomaticPlayback)
        {
            var now = Time.realtimeSinceStartup;
            previewGraph.Evaluate(now - lastTime);
            lastTime = now;

            animationPlayer.UpdateSelf();
            var normalizedStateProgress = animationPlayer.GetNormalizedStateProgress(state, layer);
            playbackSlider.value = (float) normalizedStateProgress;

            var playedState = animationPlayer.GetState(state, layer);
            if (!playedState.Loops && normalizedStateProgress == 1d)
                StopPreview();
        }
    }
    
    public void Resample()
    {
        animationPlayer.UpdateSelf();
        previewGraph.Evaluate();
    }

    public void StopPreview()
    {
        if (!IsPreviewing)
            return;

        animationPlayer.SnapTo(state);
        playbackSlider.value = 0f;
        animationPlayer.ExitPreview();
        Cleanup();
        EditorApplication.update -= Update;
    }

    private void Cleanup()
    {
        // Reset the object to the first state in the first layer.
        // A solution where we play an empty clip worked ay one point, but broke. I really just want to get the model into the bind pose,
        // but Unity really resists that idea.
        var resetGraph = PlayableGraph.Create();
        try
        {
            var animOutput = AnimationPlayableOutput.Create(resetGraph, "Cleanup Graph", animationPlayer.OutputAnimator);
            var initialState = animationPlayer.layers[0].states[0];

            var clip = initialState switch
            {
                BlendTree1D blendTree1D => blendTree1D.entries[0].clip,
                BlendTree2D blendTree2D => blendTree2D.entries[0].clip,
                PlayRandomClip randomClip => randomClip.clips[0],
                Sequence sequence => sequence.clips[0],
                SingleClip singleClip => singleClip.clip,
                _ => throw new System.Exception("Unknown type")
            };

            var clipPlayable = AnimationClipPlayable.Create(resetGraph, clip);
            clipPlayable.SetApplyFootIK(false);
            animOutput.SetSourcePlayable(clipPlayable);
            resetGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            resetGraph.GetRootPlayable(0).SetTime(0);
            resetGraph.Evaluate();
        }
        finally
        {
            resetGraph.Destroy();
        }
    }
}
}
#endif