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
        public List<AnimationClip> clips = new List<AnimationClip>();
        private int playedClip;
        private Playable runtimePlayable;

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
                if (clips == null || clips.Count == 0 || clips[0] == null)
                    return 0f;
                return clips[0].length;
            }
        }

        public override bool Loops
        {
            get
            {
                if (clips == null || clips.Count == 0 || clips[0] == null)
                    return false;
                return clips[0].isLooping;
            }
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars)
        {
            playedClip = clips.GetRandomIdx();
            return GeneratePlayableFor(graph, playedClip);
        }


        private Playable GeneratePlayableFor(PlayableGraph graph, int clipIdx)
        {
            playedClip = clipIdx;
            var clip = clips[playedClip];
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        internal override void SetRuntimePlayable(Playable runtimePlayable)
        {
            this.runtimePlayable = runtimePlayable;
        }

        public override void OnWillStartPlaying(PlayableGraph graph, AnimationMixerPlayable stateMixer, int ownIndex, ref Playable ownPlayable)
        {
            //this happens if we're looping, and were already partially playing when the state were started. In that case, don't snap to a different random choice.
            if (ownPlayable.GetTime() > 0f)
                return;

            var wantedClip = clips.GetRandomIdx();
            if (wantedClip == playedClip)
                return;

            playedClip = wantedClip;

            var clipPlayable = (AnimationClipPlayable) ownPlayable;
            PlayableUtilities.ReplaceClipInPlace(ref clipPlayable, clips[wantedClip]);
            ownPlayable = clipPlayable;
        }

        public override void JumpToRelativeTime(float time, AnimationMixerPlayable stateMixer)
        {
            runtimePlayable.SetTime(time * Duration);
        }
    }
}