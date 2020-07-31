using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class PlayRandomClip : AnimationPlayerState
    {
        public const string DefaultName = "New Random State";
        private int playedClip;

        public  List<AnimationClip> clips = new List<AnimationClip>();
        private ClipSwapHandler _clipsToUse;
        private ClipSwapHandler ClipsToUse
        {
            get
            {
                if (_clipsToUse.clips != clips)
                    _clipsToUse = new ClipSwapHandler(this, clips);
                return _clipsToUse;
            }
        }

        private PlayRandomClip() { }

        public static PlayRandomClip Create(string name)
        {
            var state = new PlayRandomClip();
            state.Initialize(name, DefaultName);
            return state;
        }

        public override float Duration
        {
            get
            {
                if (clips.Count == 0)
                    return 0f;
                return ClipsToUse[playedClip].length;
            }
        }

        public override bool Loops
        {
            get
            {
                if (clips.Count == 0)
                    return false;
                return ClipsToUse[playedClip].isLooping;
            }
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers)
        {
            playedClip = clips.Count == 0 ? -1 : clips.GetRandomIdx();
            var clip = playedClip == -1 ? AnimationPlayer.dummyEmptyClip : ClipsToUse[playedClip];
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }


        public override void OnWillStartPlaying(ref Playable ownPlayable)
        {
            //this happens if we're looping, and were already partially playing when the state were started. In that case, don't snap to a different random choice.
            if (ownPlayable.GetTime() > 0f)
                return;

            var wantedClip = clips.GetRandomIdx();
            if (wantedClip == playedClip)
                return;

            playedClip = wantedClip;

            var asClipPlayable = (AnimationClipPlayable) ownPlayable;
            PlayableUtilities.ReplaceClipInPlace(ref asClipPlayable, ClipsToUse[wantedClip]);
            ownPlayable = asClipPlayable;
        }

        public override void JumpToRelativeTime(ref Playable ownPlayable, float time)
        {
            ownPlayable.SetTime(time * Duration);
        }

        public override void OnClipSwapsChanged(ref Playable ownPlayable)
        {
            var asClipPlayable = (AnimationClipPlayable) ownPlayable;

            var shouldBePlaying = ClipsToUse[playedClip];
            var isPlaying = asClipPlayable.GetAnimationClip();

            if (shouldBePlaying != isPlaying)
            {
                PlayableUtilities.ReplaceClipInPlace(ref asClipPlayable, shouldBePlaying);
                ownPlayable = asClipPlayable;
            }
        }

        public override void RegisterUsedBlendVarsIn(Dictionary<string, float> blendVariableValues) { }
    }
}