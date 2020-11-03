using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;

namespace Animation_Player
{
public class AnimationPlayerPreviewer
{
    private AnimationPlayer animationPlayer;
    private PlayableGraph graph;
    private float lastTime;
    private Slider playbackSlider;
    private int layer;
    private int state;

    public AnimationPlayerPreviewer(AnimationPlayer animationPlayer)
    {
        this.animationPlayer = animationPlayer;
    }

    public bool IsPreviewing => graph.IsValid();

    public void StartPreview(int layer, int state, Slider playbackSlider)
    {
        this.playbackSlider = playbackSlider;
        this.layer = layer;
        this.state = state;

        animationPlayer.ExitPreview();
        animationPlayer.EnterPreview();
        animationPlayer.Play(state, layer);
        graph = animationPlayer.Graph;
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        lastTime = Time.realtimeSinceStartup;
    }

    public void Update()
    {
        animationPlayer.UpdateSelf();

        var normalizedStateProgress = (float) animationPlayer.GetNormalizedStateProgress(state, layer);

        playbackSlider.value = normalizedStateProgress;

    }

    public void StopPreview()
    {
        animationPlayer.ExitPreview();
    }
}
}