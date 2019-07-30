using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class BlendTree2D : AnimationState
    {
        public const string DefaultName = "New 2D Blend Tree";

        public string blendVariable;
        public string blendVariable2;
        public List<BlendTreeEntry2D> blendTree;
        private AnimationMixerPlayable treeMixer;

        private BlendTree2D() { }

        public static BlendTree2D Create(string name)
        {
            var blendTree = new BlendTree2D();
            blendTree.Initialize(name, DefaultName);
            blendTree.blendVariable = "blend1";
            blendTree.blendVariable2 = "blend2";
            blendTree.blendTree = new List<BlendTreeEntry2D>();
            return blendTree;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars)
        {
            treeMixer = AnimationMixerPlayable.Create(graph, blendTree.Count, true);
            if (blendTree.Count == 0)
                return treeMixer;

            Action<float> setVar1 = val => blendVars[blendVariable] = val;
            Action<float> setVar2 = val => blendVars[blendVariable2] = val;
            var controller = new BlendTreeController2D(blendVariable, blendVariable2, treeMixer, blendTree.Count, setVar1, setVar2);
            all2DControllers.Add(controller);
            varTo2DBlendControllers.GetOrAdd(blendVariable).Add(controller);
            varTo2DBlendControllers.GetOrAdd(blendVariable2).Add(controller);

            for (int j = 0; j < blendTree.Count; j++)
            {
                var blendTreeEntry = blendTree[j];
                var clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                clipPlayable.SetApplyFootIK(true);
                clipPlayable.SetSpeed(speed);
                graph.Connect(clipPlayable, 0, treeMixer, j);

                controller.AddThresholdsForClip(j, blendTreeEntry.threshold1, blendTreeEntry.threshold2);
            }

            controller.OnAllThresholdsAdded();

            controller.SetInitialValues(0f, 0f);
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
                    var clipLength = blendTreeEntry.clip?.length ?? 0f;
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
                foreach (var entry in blendTree)
                {
                    if (entry.clip?.isLooping ?? false)
                        return true;
                }
                return false;
            }
        }

        public override void JumpToRelativeTime(float time)
        {
            float unNormalizedTime = time * Duration;
            treeMixer.SetTime(unNormalizedTime);
            for (int i = 0; i < treeMixer.GetInputCount(); i++)
            {
                treeMixer.GetInput(i).SetTime(unNormalizedTime);
            }
        }
    }
}