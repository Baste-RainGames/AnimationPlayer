using UnityEngine;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    public abstract class IKAnimationPlayerConnection : MonoBehaviour {
        public abstract AnimationScriptPlayable GeneratePlayable(Animator outputAnimator, PlayableGraph graph);
    }
}