using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;

namespace Animation_Player
{
public class AnimationPlayerPreviewer
{
    private AnimationPlayer animationPlayer;
    private PlayableGraph graph;
    private Slider playbackSlider;
    private int layer;
    private int state;


    public AnimationPlayerPreviewer(AnimationPlayer animationPlayer)
    {
        this.animationPlayer = animationPlayer;
    }

    public bool IsPreviewing => graph.IsValid();
    public bool AutomaticPlayback { get; private set; }

    public void StartPreview(int layer, int state, bool automaticPlayback, Slider playbackSlider)
    {
        this.playbackSlider = playbackSlider;
        AutomaticPlayback = automaticPlayback;
        this.layer = layer;
        this.state = state;

        animationPlayer.ExitPreview();
        animationPlayer.EnterPreview();
        animationPlayer.Play(state, layer);
        graph = animationPlayer.Graph;
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        EditorApplication.update += Update;
    }

    public void Test()
    {
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
    }

    public void SetAutomaticPlayback(bool automaticPlayback)
    {
        Debug.Log(automaticPlayback + "/" + IsPreviewing);
        if (!IsPreviewing)
            return;

        AutomaticPlayback = automaticPlayback;
        graph.SetTimeUpdateMode(automaticPlayback ? DirectorUpdateMode.GameTime : DirectorUpdateMode.Manual);
        Debug.Log(graph.GetTimeUpdateMode());
    }

    public void Update()
    {
        if (AutomaticPlayback)
        {
            animationPlayer.UpdateSelf();
            var normalizedStateProgress = (float) animationPlayer.GetNormalizedStateProgress(state, layer);
            playbackSlider.value = normalizedStateProgress;
        }
    }

    public void StopPreview()
    {
        animationPlayer.ExitPreview();
        EditorApplication.update -= Update;
    }
}
}