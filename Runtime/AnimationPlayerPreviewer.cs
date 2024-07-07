#if UNITY_EDITOR
using System.Linq;

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

    public bool AutomaticPlayback { get; set; }
    public bool IsPreviewing      { get; private set; }

    public void StartPreview(int layer, int state, bool automaticPlayback, Slider playbackSlider, float blendVar1Value, float blendVar2Value)
    {
        this.playbackSlider = playbackSlider;
        AutomaticPlayback = automaticPlayback;
        this.layer = layer;
        this.state = state;

        if (IsPreviewing)
            animationPlayer.ExitPreview();
        IsPreviewing = true;

        animationPlayer.EnterPreview();
        var playedState = animationPlayer.Play(state, layer);
        previewGraph = animationPlayer.Graph;
        previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        
        if (playedState is BlendTree1D blendTree1D)
        {
            animationPlayer.SetBlendVar(blendTree1D.blendVariable, blendVar1Value);
        }
        else if (playedState is BlendTree2D blendTree2D)
        {
            animationPlayer.SetBlendVar(blendTree2D.blendVariable,  blendVar1Value);
            animationPlayer.SetBlendVar(blendTree2D.blendVariable2, blendVar2Value);
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
            var stateProgress = animationPlayer.GetCurrentStateTime(state, true, layer);
            playbackSlider.SetValueWithoutNotify((float) stateProgress);

            var playedState = animationPlayer.GetState(state, layer);
            if (!playedState.Loops && animationPlayer.GetNormalizedStateProgress(state, layer) == 1d) // returns double clamped to [0, 1], so >= 1d is always exactly 1d
                StopPreview();
        }
    }
    
    public void Resample()
    {
        animationPlayer.UpdateSelf();
        previewGraph.Evaluate();
    }
    
    public void SetTime(float relativeTime)
    {
        animationPlayer.JumpToRelativeTime(relativeTime);
        animationPlayer.UpdateSelf();
        previewGraph.Evaluate();
    }

    public void StopPreview()
    {
        if (!IsPreviewing)
            return;

        IsPreviewing = false;
        playbackSlider.SetValueWithoutNotify(0f);
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
            var initialState = animationPlayer.editTimePreviewState ?? animationPlayer.layers[0].states[0];

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