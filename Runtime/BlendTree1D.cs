using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace Animation_Player
{
    [Serializable]
    public class BlendTree1D : AnimationPlayerState
    {
        public const string DefaultName = "New Blend Tree";

        public string blendVariable;
        [FormerlySerializedAs("blendTree")]
        public List<BlendTreeEntry1D> entries;
        public bool compensateForDifferentDurations = true; // @TODO: this should be setable from the editor, no?

        private BlendTreeController1D controller;

        private BlendTree1D() { }

        public static BlendTree1D Create(string name)
        {
            var blendTree = new BlendTree1D();
            blendTree.Initialize(name, DefaultName);
            blendTree.blendVariable = "blend";
            blendTree.entries = new List<BlendTreeEntry1D>();

            return blendTree;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers)
        {
            var treeMixer = AnimationMixerPlayable.Create(graph, entries.Count, true);
            treeMixer.SetSpeed(speed);
            treeMixer.SetPropagateSetTime(true);

            if (entries.Count == 0)
                return treeMixer;


            float[] thresholds = new float[entries.Count];

            var innerPlayables = new AnimationClipPlayable[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var blendTreeEntry = entries[i];
                var clip = GetClipToUseFor(blendTreeEntry.clip);
                if (clip == null)
                    clip = new AnimationClip();
                var clipPlayable = AnimationClipPlayable.Create(graph, clip);
                clipPlayable.SetApplyFootIK(true);
                // clipPlayable.SetSpeed(speed);
                graph.Connect(clipPlayable, 0, treeMixer, i);
                thresholds[i] = blendTreeEntry.threshold;
                innerPlayables[i] = clipPlayable;

            }

            controller = new BlendTreeController1D(treeMixer, innerPlayables, thresholds, compensateForDifferentDurations);
            varTo1DBlendControllers.GetOrAdd(blendVariable).Add(controller);
            controller.SetInitialValue(0);

            return treeMixer;
        }

        public override float Duration
        {
            get
            {
                var longest = 0f;
                foreach (var blendTreeEntry in entries)
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
                foreach (var entry in entries) {
                    var clip = GetClipToUseFor(entry.clip);
                    if (clip != null && clip.isLooping)
                        return true;
                }
                return false;
            }
        }

        public override void JumpToRelativeTime(ref Playable ownPlayable, float time)
        {
            float unNormalizedTime = time * Duration;
            ownPlayable.SetTime(unNormalizedTime);
            for (int i = 0; i < ownPlayable.GetInputCount(); i++)
            {
                ownPlayable.GetInput(i).SetTime(unNormalizedTime);
            }
        }

        public override void OnClipSwapsChanged(ref Playable ownPlayable)
        {
            var asMixer = (AnimationMixerPlayable) ownPlayable;
            var inputCount = asMixer.GetInputCount();

            for (int i = 0; i < inputCount; i++)
            {
                var clipPlayable = (AnimationClipPlayable) asMixer.GetInput(i);
                var shouldPlay = GetClipToUseFor(entries[i].clip);
                var isPlaying = clipPlayable.GetAnimationClip();

                if (isPlaying != shouldPlay) {
                    PlayableUtilities.ReplaceClipInPlace(ref clipPlayable, shouldPlay);
                    controller.PlayableChanged(i, clipPlayable);
                }
            }
        }

        public override void RegisterUsedBlendVarsIn(Dictionary<string, float> blendVariableValues) {
            if (!blendVariableValues.ContainsKey(blendVariable))
                blendVariableValues[blendVariable] = 0;
        }
    }
}