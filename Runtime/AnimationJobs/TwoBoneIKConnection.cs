using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.Animations;
#else
using UnityEngine.Experimental.Animations;
#endif
using UnityEngine.Playables;

namespace Animation_Player
{
public class TwoBoneIKConnection : MonoBehaviour, IIKAnimationPlayerConnection
{
    [SerializeField] private Transform effector;
    [SerializeField] private Transform top;
    [SerializeField] private Transform mid;
    [SerializeField] private Transform low;


    public AnimationScriptPlayable GeneratePlayable(Animator outputAnimator, PlayableGraph graph)
    {
        var job = new TwoBoneIKJob();
        job.Setup(outputAnimator, top, mid, low, effector);
        return AnimationScriptPlayable.Create(graph, job);
    }
}
}