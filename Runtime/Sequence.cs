using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player {
    [Serializable]
    public class Sequence : AnimationState {
        public const string DefaultName = "New Sequence";

        public List<AnimationClip> clips;
        private AnimationClipPlayable clipPlayable;

        private Sequence() { }

        public static Sequence Create(string name) {
            var state = new Sequence();
            state.Name = name;
            state.Initialize(name, DefaultName);
            state.clips = new List<AnimationClip>();
            return state;
        }

        public override float Duration {
            get {
                var duration = 0f;
                foreach (var clip in clips)
                    if (clip != null)
                        duration += clip.length;
                return duration;
            }
        }

        public override bool Loops => clips.Count > 0 && clips[clips.Count - 1] != null && clips[clips.Count - 1].isLooping;

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

            return GeneratePlayable(graph, clips[0]);
        }

        private AnimationClipPlayable GeneratePlayable(PlayableGraph graph, AnimationClip clip) {
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        public override void AddAllClipsTo(List<AnimationClip> list) {
            list.AddRange(clips);
        }

        public override IEnumerable<AnimationClip> GetClips() {
            return clips;
        }

        internal void ProgressThroughSequence(ref Playable playable, AnimationMixerPlayable stateMixer) {
            var currentClipIndex = clips.IndexOf(clipPlayable.GetAnimationClip());

            ProgressThroughSequenceFrom(currentClipIndex, ref playable, stateMixer);
        }

        private void ProgressThroughSequenceFrom(int currentClipIndex, ref Playable playable, AnimationMixerPlayable stateMixer) {
            if (currentClipIndex == clips.Count - 1)
                return; // has to change if we start supporting the entire sequence looping instead of just the last clip.

            var currentClipTime = clipPlayable.GetTime();
            var currentClipDuration = clips[currentClipIndex].length;

            if (currentClipTime < currentClipDuration)
                return;

            var timeToPlayNextClipAt = currentClipTime - currentClipDuration;
            SwapPlayedClipTo(clips[currentClipIndex + 1], timeToPlayNextClipAt, stateMixer);
            playable = clipPlayable;

            // recurse in case we've got a really long delta time or a really short clip, and have to jump past two clips.
            ProgressThroughSequenceFrom(currentClipIndex + 1, ref playable, stateMixer);
        }

        public override void OnWillStartPlaying(PlayableGraph graph, AnimationMixerPlayable stateMixer, int ownIndex, ref Playable ownPlayable) {
            if (clipPlayable.GetAnimationClip() != clips[0]) {
                // woo side effects!
                JumpToRelativeTime(0f, stateMixer);
                ownPlayable = clipPlayable;
            }
        }

        public override void JumpToRelativeTime(float time, AnimationMixerPlayable stateMixer) {
            var (clipToUse, timeToPlayClipAt) = FindClipAndTimeAtRelativeTime(time);

            if (clipToUse == null) {
                Debug.LogError("Couldn't play at relative time " + time);
                return;
            }

            if (clipPlayable.GetAnimationClip() != clipToUse) {
                SwapPlayedClipTo(clipToUse, timeToPlayClipAt, stateMixer);
            }
            else {
                clipPlayable.SetTime(timeToPlayClipAt);
            }
        }

        private void SwapPlayedClipTo(AnimationClip clipToUse, double timeToPlayClipAt, AnimationMixerPlayable stateMixer) {
            // We can't swap a clip on an animation clip playable, so we have to hot-swap with a new animation clip playable.
            var graph      = clipPlayable.GetGraph();

            var inputIndex = -1;
            for (int i = 0; i < stateMixer.GetInputCount(); i++) {
                if (stateMixer.GetInput(i).Equals(clipPlayable)) {
                    inputIndex = i;
                    break;
                }
            }

            if (inputIndex == -1) {
                Debug.LogError("This doesn't work like I think it does!");
                return;
            }

            var oldWeight = stateMixer.GetInputWeight(inputIndex);
            stateMixer.DisconnectInput(inputIndex);
            clipPlayable.Destroy();
            clipPlayable = GeneratePlayable(graph, clipToUse);
            clipPlayable.SetTime(timeToPlayClipAt);

            stateMixer.ConnectInput(inputIndex, clipPlayable, 0);
            stateMixer.SetInputWeight(inputIndex, oldWeight);
        }

        private (AnimationClip clip, double timeToPlayClipAt) FindClipAndTimeAtRelativeTime(float time) {
            var targetTime = time * (double) Duration;
            var durationOfEarlierClips = 0d;

            foreach (var clip in clips) {
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