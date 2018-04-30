using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class SingleClip : AnimationState
    {
        public const string DefaultName = "New State";
        public AnimationClip clip;

        private SingleClip() { }

        public static SingleClip Create(string name, AnimationClip clip = null)
        {
            var state = new SingleClip();
            state.Initialize(name, DefaultName);
            state.clip = clip;
            return state;
        }

        public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars)
        {
            if(clip == null)
                clip = new AnimationClip();
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetSpeed(speed);
            return clipPlayable;
        }

        public override float Duration => clip != null ? clip.length : 0f;
        public override bool Loops => clip != null && clip.isLooping;
    }
}