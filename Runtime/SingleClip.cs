using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace Animation_Player
{
    [Serializable]
    public class SingleClip : AnimationPlayerState
    {
        public const string DefaultName = "New State";
        [FormerlySerializedAs("clip")]
        public AnimationClip assignedClip;
        private AnimationClipPlayable runtimePlayable;

        private SingleClip() { }

        public static SingleClip Create(string name, AnimationClip clip = null)
        {
            var state = new SingleClip();
            state.Initialize(name, DefaultName);
            state.assignedClip = clip;
            return state;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars)
        {
            if (assignedClip == null)
                assignedClip = new AnimationClip();
            var clipPlayable = AnimationClipPlayable.Create(graph, GetClipToUseFor(assignedClip));
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        protected override void SetRuntimePlayable(Playable runtimePlayable)
        {
            this.runtimePlayable = (AnimationClipPlayable) runtimePlayable;
        }

        public override float Duration
        {
            get
            {
                var clip = GetClipToUseFor(assignedClip);
                return clip == null ? 0f : clip.length;
            }
        }

        public override bool Loops
        {
            get
            {
                var clip = GetClipToUseFor(assignedClip);
                return clip != null && clip.isLooping;
            }
        }

        public override void JumpToRelativeTime(float time)
        {
            runtimePlayable.SetTime(time * Duration);
        }

        public Playable SwapClipTo(AnimationClip animationClip)
        {
            assignedClip = animationClip;
            PlayableUtilities.ReplaceClipInPlace(ref runtimePlayable, GetClipToUseFor(animationClip));
            return runtimePlayable;
        }
    }
}