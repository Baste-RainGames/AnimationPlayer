using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class SingleClipState : AnimationState
    {
        public const string DefaultName = "New State";
        public AnimationClip clip;

        private SingleClipState() { }

        public static SingleClipState Create(string name, AnimationClip clip = null)
        {
            var state = new SingleClipState();
            state.Initialize(name, DefaultName);
            state.clip = clip;
            return state;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers, Dictionary<string, float> blendVars)
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        public override float Duration => clip?.length ?? 0f;
        public override bool Loops => clip?.isLooping ?? false;
    }
}