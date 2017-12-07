using System;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class BlendTree1D : AnimationState
    {
        public const string DefaultName = "New Blend Tree";

        public string blendVariable;
        public List<BlendTreeEntry> blendTree;

        private BlendTree1D() { }

        public static BlendTree1D Create(string name, int listIndex)
        {
            var blendTree = new BlendTree1D();
            blendTree.Initialize(name, DefaultName, listIndex);
            blendTree.blendVariable = "blend";
            blendTree.blendTree = new List<BlendTreeEntry>();

            return blendTree;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers, Dictionary<string, float> blendVars)
        {
            var treeMixer = AnimationMixerPlayable.Create(graph, blendTree.Count, true);
            if (blendTree.Count == 0)
                return treeMixer;

            float[] thresholds = new float[blendTree.Count];

            for (int j = 0; j < blendTree.Count; j++)
            {
                var blendTreeEntry = blendTree[j];
                var clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                clipPlayable.SetSpeed(speed);
                graph.Connect(clipPlayable, 0, treeMixer, j);
                thresholds[j] = blendTreeEntry.threshold;
            }

            treeMixer.SetInputWeight(0, 1f);
            var blendController = new BlendTreeController1D(treeMixer, thresholds, val => blendVars[blendVariable] = val);
            varTo1DBlendControllers.GetOrAdd(blendVariable).Add(blendController);
            blendVars[blendVariable] = 0;

            return treeMixer;
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
    }
}