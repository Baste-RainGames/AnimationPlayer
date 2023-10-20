using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.Animations;
#else
using UnityEngine.Experimental.Animations;
#endif
using UnityEngine.Playables;

namespace Animation_Player
{
public interface IIKAnimationPlayerConnection
{
    AnimationScriptPlayable GeneratePlayable(Animator outputAnimator, PlayableGraph graph);
}
}