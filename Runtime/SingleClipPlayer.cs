using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
/// <summary>
/// A very simplified animation player, which only plays a single clip.
///
/// You'd want to use this over the animation player as it builds a simpler graph,
/// and because it destroys it's graph and disables it's animator when done.
/// </summary>
public class SingleClipPlayer : MonoBehaviour, IAnimationClipSource
{
    [SerializeField] private AnimationClip clip;
    [SerializeField] private bool playOnStart;

    private string graphName;
    private string outputName;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable clipPlayable;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        animator = gameObject.EnsureComponent<Animator>();
        graphName = $"Playable Graph {name}";
        outputName = $"Playable Graph {name} output";
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (clipPlayable.IsValid() && clipPlayable.GetPlayState() == PlayState.Paused)
        {
            clipPlayable.Play();
        }
        else if (!IsPlaying)
        {
            IsPlaying = true;
            animator.enabled = true;
            graph = PlayableGraph.Create(graphName);
            var animOutput = AnimationPlayableOutput.Create(graph, outputName , animator);
            clipPlayable = AnimationClipPlayable.Create(graph, clip != null ? clip : new ());
            clipPlayable.SetDuration(clip.length);

            animOutput.SetSourcePlayable(clipPlayable);

            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            graph.Play();
        }
        else
        {
            clipPlayable.SetTime(0);
        }
    }

    public async Task PlayAsync()
    {
        Play();
        while (IsPlaying)
            await Task.Yield();
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        if (clipPlayable.IsDone())
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (IsPlaying)
        {
            graph.Stop();
            graph.Destroy();
            animator.enabled = false;
            IsPlaying = false;
        }
    }

    public void Pause()
    {
        clipPlayable.Pause();
    }

    public void SetToNormalizedTime(float time)
    {
        if (IsPlaying)
        {
            var duration = clipPlayable.GetDuration();
            clipPlayable.SetTime(time * duration);
        }
        else
        {
            Debug.LogWarning("Calling SetToNormalizedTime on a SingleClipPlayer that's not playing. That's not neccessary, SingleClipPlayers " +
                             "always start their clips at time 0");
        }
    }

    private void OnDestroy()
    {
        Stop();
    }

    public void GetAnimationClips(List<AnimationClip> results)
    {
        results.EnsureContains(clip);
    }
}
}