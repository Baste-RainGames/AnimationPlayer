using UnityEngine;
using UnityEngine.Playables;

namespace Animation_Player {
public class AnimationPlayerPreviewer {

    private AnimationPlayer animationPlayer;
    private PlayableGraph graph;
    private float lastTime;

    public AnimationPlayerPreviewer(AnimationPlayer animationPlayer)
    {
        this.animationPlayer = animationPlayer;
    }

    public bool IsPreviewing => graph.IsValid();

    public void StartPreview(int layer, int state)
    {
        animationPlayer.EnterPreview();
        animationPlayer.Play(state, layer);
        graph = animationPlayer.Graph;
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        lastTime = Time.realtimeSinceStartup;
    }

    public void Update()
    {
        var currentTime = Time.realtimeSinceStartup;
        var deltaTime = currentTime - lastTime;
        animationPlayer.UpdateSelf();
        graph.Evaluate(deltaTime);

        lastTime = currentTime;
    }

    public void StopPreview()
    {
        animationPlayer.ExitPreview();
    }
}
}