using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class BlendTree1D : AnimationPlayerState
    {
        public const string DefaultName = "New Blend Tree";

        public string blendVariable;
        public List<BlendTreeEntry1D> blendTree;
        public bool compensateForDifferentDurations = true;
        private AnimationMixerPlayable runtimePlayable;

        private BlendTree1D() { }

        public static BlendTree1D Create(string name)
        {
            var blendTree = new BlendTree1D();
            blendTree.Initialize(name, DefaultName);
            blendTree.blendVariable = "blend";
            blendTree.blendTree = new List<BlendTreeEntry1D>();

            return blendTree;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars)
        {
            var treeMixer = AnimationMixerPlayable.Create(graph, blendTree.Count, true);
            if (blendTree.Count == 0)
                return treeMixer;

            float[] thresholds = new float[blendTree.Count];

            var innerPlayables = new AnimationClipPlayable[blendTree.Count];
            for (int i = 0; i < blendTree.Count; i++)
            {
                var blendTreeEntry = blendTree[i];
                var clip = GetClipToUseFor(blendTreeEntry.clip);
                if (clip == null)
                    clip = new AnimationClip();
                var clipPlayable = AnimationClipPlayable.Create(graph, clip);
                clipPlayable.SetApplyFootIK(true);
                clipPlayable.SetSpeed(speed);
                graph.Connect(clipPlayable, 0, treeMixer, i);
                thresholds[i] = blendTreeEntry.threshold;
                innerPlayables[i] = clipPlayable;
            }

            var blendController = new BlendTreeController1D(treeMixer, innerPlayables, thresholds, compensateForDifferentDurations,
                                                            val => blendVars[blendVariable] = val);
            varTo1DBlendControllers.GetOrAdd(blendVariable).Add(blendController);
            blendController.SetInitialValue(0);

            return treeMixer;
        }

        protected override void SetRuntimePlayable(Playable runtimePlayable)
        {
            this.runtimePlayable = (AnimationMixerPlayable) runtimePlayable;
        }

        public override float Duration
        {
            get
            {
                var longest = 0f;
                foreach (var blendTreeEntry in blendTree)
                {
                    var clip = GetClipToUseFor(blendTreeEntry.clip);
                    var clipLength = clip == null ? 0f : clip.length;
                    if (clipLength > longest)
                        longest = clipLength;
                }

                return longest;
            }
        }

        public override bool Loops
        {
            get
            {
                foreach (var entry in blendTree) {
                    var clip = GetClipToUseFor(entry.clip);
                    if (clip != null && clip.isLooping)
                        return true;
                }
                return false;
            }
        }

        public override void JumpToRelativeTime(float time)
        {
            float unNormalizedTime = time * Duration;
            runtimePlayable.SetTime(unNormalizedTime);
            for (int i = 0; i < runtimePlayable.GetInputCount(); i++)
            {
                runtimePlayable.GetInput(i).SetTime(unNormalizedTime);
            }
        }
    }
}