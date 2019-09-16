using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    [Serializable]
    public class Sequence : AnimationPlayerState {
        public const string DefaultName = "New Sequence";

        public List<AnimationClip> clips = new List<AnimationClip>();
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

        private Sequence() { }

        public static Sequence Create(string name) {
            var state = new Sequence();
            state.Initialize(name, DefaultName);
            state.clips = new List<AnimationClip>();
            return state;
        }

        public override float Duration {
            get {
                var duration = 0f;
                foreach (var clip in ClipsToUse)
                    if (clip != null)
                        duration += clip.length;
                return duration;
            }
        }

        public override bool Loops
        {
            get
            {
                foreach (var clip in ClipsToUse)
                    if (clip != null && clip.isLooping)
                        return true;
                return false;
            }
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars) {
            if (clips.Count == 0) {
                clips.Add(new AnimationClip());
            }

            for (int i = 0; i < clips.Count; i++) {
                if (clips[i] == null)
                    clips[i] = new AnimationClip();
            }

            var clipPlayable = AnimationClipPlayable.Create(graph, ClipsToUse[0]);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        internal void ProgressThroughSequence(ref Playable playable)
        {
            var asACPlayable = (AnimationClipPlayable) playable;
            var currentClipIndex = ClipsToUse.IndexOf(asACPlayable.GetAnimationClip());

            if (currentClipIndex == -1)
            {
                Debug.LogError("Couldn't find the current played clip in a sequence's clips! Did a swap happen behind it's back?");
                return;
            }

            ProgressThroughSequenceFrom(currentClipIndex, ref asACPlayable);
            playable = asACPlayable;
        }

        private void ProgressThroughSequenceFrom(int currentClipIndex, ref AnimationClipPlayable runtimePlayable) {
            if (currentClipIndex == ClipsToUse.Count - 1)
                return; // has to change if we start supporting the entire sequence looping instead of just the last clip.

            var currentClipTime = runtimePlayable.GetTime();
            var currentClipDuration = ClipsToUse[currentClipIndex].length;

            if (currentClipTime < currentClipDuration)
                return;

            var timeToPlayNextClipAt = currentClipTime - currentClipDuration;
            SwapPlayedClipTo(ref runtimePlayable, ClipsToUse[currentClipIndex + 1], timeToPlayNextClipAt);

            // recurse in case we've got a really long delta time or a really short clip, and have to jump past two clips.
            ProgressThroughSequenceFrom(currentClipIndex + 1, ref runtimePlayable);
        }

        public override void OnWillStartPlaying(ref Playable ownPlayable) {
            var asACPlayable = (AnimationClipPlayable) ownPlayable;
            if (asACPlayable.GetAnimationClip() != ClipsToUse[0]) {
                // woo side effects!
                JumpToRelativeTime(ref ownPlayable, 0f);
            }
        }

        public override void JumpToRelativeTime(ref Playable runtimePlayable, float time) {
            var (clipToUse, timeToPlayClipAt) = FindClipAndTimeAtRelativeTime(time);

            if (clipToUse == null) {
                Debug.LogError("Couldn't play at relative time " + time);
                return;
            }

            var asACPlayable = (AnimationClipPlayable) runtimePlayable;
            if (asACPlayable.GetAnimationClip() != clipToUse) {
                SwapPlayedClipTo(ref asACPlayable, clipToUse, timeToPlayClipAt);
                runtimePlayable = asACPlayable;
            }
            else {
                runtimePlayable.SetTime(timeToPlayClipAt);
            }
        }

        private void SwapPlayedClipTo(ref AnimationClipPlayable runtimePlayable, AnimationClip clipToUse, double timeToPlayClipAt) {
            PlayableUtilities.ReplaceClipInPlace(ref runtimePlayable, clipToUse);
            runtimePlayable.SetTime(timeToPlayClipAt);
        }

        private (AnimationClip clip, double timeToPlayClipAt) FindClipAndTimeAtRelativeTime(float time) {
            var targetTime = time * (double) Duration;
            var durationOfEarlierClips = 0d;

            foreach (var clip in ClipsToUse) {
                if (durationOfEarlierClips + clip.length >= targetTime) {
                    var timeToPlayClipAt = durationOfEarlierClips - targetTime;
                    return (clip, timeToPlayClipAt);
                }

                durationOfEarlierClips += clip.length;
            }

            return (null, 0d);
        }
    }
}