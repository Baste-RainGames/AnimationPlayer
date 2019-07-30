using UnityEngine;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    public interface IIKAnimationPlayerConnection {
        AnimationScriptPlayable GeneratePlayable(Animator outputAnimator, PlayableGraph graph);
    }
}