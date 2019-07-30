using UnityEngine;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    public class TwoBoneIKConnection : MonoBehaviour, IIKAnimationPlayerConnection {
        [SerializeField] private Transform effector;
        [SerializeField] private Transform top;
        [SerializeField] private Transform mid;
        [SerializeField] private Transform low;


        public AnimationScriptPlayable GeneratePlayable(Animator outputAnimator, PlayableGraph graph) {
            var job = new TwoBoneIKJob();
            job.Setup(outputAnimator, top, mid, low, effector);
            return AnimationScriptPlayable.Create(graph, job);
        }
    }
}