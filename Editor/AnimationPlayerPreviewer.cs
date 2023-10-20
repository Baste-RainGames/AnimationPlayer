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

    public void StartPreview(int layer, int state, bool automaticPlayback, Slider playbackSlider)
    {
        this.playbackSlider = playbackSlider;
        AutomaticPlayback = automaticPlayback;
        this.layer = layer;
        this.state = state;

        animationPlayer.ExitPreview();
        animationPlayer.EnterPreview();
        animationPlayer.Play(state, layer);
        previewGraph = animationPlayer.Graph;
        previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        lastTime = Time.realtimeSinceStartup;

        EditorApplication.update += Update;
    }

    public void Update()
    {
        if (AutomaticPlayback)
        {
            var now = Time.realtimeSinceStartup;
            animationPlayer.Graph.Evaluate(now - lastTime);
            lastTime = now;

            animationPlayer.UpdateSelf();
            var normalizedStateProgress = (float) animationPlayer.GetNormalizedStateProgress(state, layer);
            playbackSlider.value = normalizedStateProgress;
        }
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
            var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
            var animOutput = AnimationPlayableOutput.Create(resetGraph, "Cleanup Graph", animator);
            var state = animationPlayer.layers[0].states[0];

            AnimationClip clip;
            if (state is BlendTree1D blendTree1D)
                clip = blendTree1D.entries[0].clip;
            else if (state is BlendTree2D blendTree2D)
                clip = blendTree2D.entries[0].clip;
            else if (state is PlayRandomClip randomClip)
                clip = randomClip.clips[0];
            else if (state is SingleClip singleClip)
                clip = singleClip.clip;
            else
                throw new System.Exception("Unknown type");

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