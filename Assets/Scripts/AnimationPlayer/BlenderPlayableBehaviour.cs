using UnityEngine.Animations;
using UnityEngine.Playables;

// Comment from Baste: Okay, this is funky. This doesn't actually blend between the two other clips.
// what's happening is that the graph is calling PrepareFrame, and this is setting the weighting between
// the other clips.
// so the 0 and 1 args to SetInputWeight are hard-coded references to two other clips
public class BlenderPlayableBehaviour : PlayableBehaviour
{
    public AnimationMixerPlayable mixerPlayable;
    public float blendVal;

    public override void PrepareFrame(Playable playable, FrameData info)
    {
        mixerPlayable.SetInputWeight(0, 1 - blendVal);
        mixerPlayable.SetInputWeight(1, blendVal);

        base.PrepareFrame(playable, info);
    }
}