using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    /// <summary>
    /// A very simplified animation player, which only plays a single clip.
    ///
    /// You'd want to use this over the animation player as it builds a simpler graph, and because it destroys it's graph and disables the animator when it's
    /// done.
    /// </summary>
    public class SingleClipPlayer : MonoBehaviour {

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

        private void Update() {
            if (!isPlaying)
                return;

            if (clipPlayable.GetTime() >= clipLength)
                Stop();
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
    }
}