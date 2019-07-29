using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class BlendTree1D : AnimationState
    {
        public const string DefaultName = "New Blend Tree";

        public string blendVariable;
        public List<BlendTreeEntry1D> blendTree;
        public bool compensateForDifferentDurations = true;

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
                var clip = blendTreeEntry.clip;
                if(clip == null)
                    clip = new AnimationClip();
                var clipPlayable = AnimationClipPlayable.Create(graph, clip);
                clipPlayable.SetApplyFootIK(true);
                clipPlayable.SetSpeed(speed);
                graph.Connect(clipPlayable, 0, treeMixer, i);
                thresholds[i] = blendTreeEntry.threshold;
                innerPlayables[i] = clipPlayable;
            }

            treeMixer.SetInputWeight(0, 1f);
            var blendController = new BlendTreeController1D(treeMixer, innerPlayables, thresholds, compensateForDifferentDurations,
                                                            val => blendVars[blendVariable] = val);
            varTo1DBlendControllers.GetOrAdd(blendVariable).Add(blendController);
            blendVars[blendVariable] = 0;

            return treeMixer;
        }

        public override void AddAllClipsTo(List<AnimationClip> list) {
            foreach (var entry in blendTree) {
                if(entry.clip != null && !list.Contains(entry.clip))
                    list.Add(entry.clip);
            }
        }

        public override IEnumerable<AnimationClip> GetClips() {
            return blendTree.Select(entry => entry.clip);
        }

        public override float Duration
        {
            get
            {
                var longest = 0f;
                foreach (var blendTreeEntry in blendTree)
                {
                    var clipLength = blendTreeEntry.clip == null ? 0f : blendTreeEntry.clip.length;
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
                    if (entry != null && entry.clip != null && entry.clip.isLooping)
                        return true;
                }
                return false;
            }
        }
    }
}