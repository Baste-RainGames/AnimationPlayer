using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    /// <summary>
    /// A very simplified animation player, which only plays a single clip.
    ///
    /// You'd want to use this over the animation player as it builds a simpler graph,
    /// and because it destroys it's graph and disables it's animator when done.
    /// </summary>
    public class SingleClipPlayer : MonoBehaviour, IAnimationClipSource {

        [SerializeField] private AnimationClip clip;
        [SerializeField] private bool playOnStart;

        private string graphName;
        private string outputName;
        private Animator animator;
        private bool isPlaying;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private float clipLength;

        private void Awake() {
            animator = gameObject.EnsureComponent<Animator>();
            clipLength = clip != null ? clip.length : 0f;
            graphName = $"Playable Graph {name}";
            outputName = $"Playable Graph {name} output";
        }

        private void Start() {
            if (playOnStart)
                Play();
        }

        public void Play() {
            if (!isPlaying) {
                isPlaying = true;
                animator.enabled = true;
                graph = PlayableGraph.Create(graphName);
                var animOutput = AnimationPlayableOutput.Create(graph, outputName , animator);
                clipPlayable = AnimationClipPlayable.Create(graph, clip != null ? clip : new AnimationClip());

                animOutput.SetSourcePlayable(clipPlayable);

                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                graph.Play();
            }
            else {
                clipPlayable.SetTime(0);
            }
        }

        public async Task PlayAsync() {
            Play();
            while (isPlaying)
                await Task.Yield();
        }

        private void Update() {
            if (!isPlaying)
                return;

            if (clipPlayable.GetTime() >= clipLength) {
                Stop();
            }
        }

        public void Stop() {
            if (isPlaying) {
                graph.Stop();
                graph.Destroy();
                animator.enabled = false;
                isPlaying = false;
            }
        }

        private void OnDestroy() {
            Stop();
        }

        public void GetAnimationClips(List<AnimationClip> results) {
            results.EnsureContains(clip);
        }
    }
}