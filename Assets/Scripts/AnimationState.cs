﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using BlendTreeController1D = Animation_Player.AnimationLayer.BlendTreeController1D;
using BlendTreeController2D = Animation_Player.AnimationLayer.BlendTreeController2D;

namespace Animation_Player
{
    [Serializable]
    //@TODO: This should be done with inheritance and custom serialization, because we now end up with single-clip states having a lot of garbage data around,
    // and also BlendTreeEntries having 2D data even when it's a 1D state
    public class AnimationState
    {
        public const string DefaultSingleClipName = "New State";
        public const string Default1DBlendTreeName = "New Blend Tree";
        public const string Default2DBlendTreeName = "New 2D Blend Tree";

        [SerializeField]
        private string name;
        [SerializeField]
        private bool hasUpdatedName;
        public double speed;
        public AnimationClip clip;

        public AnimationStateType type;

        public string blendVariable;
        public string blendVariable2;
        public List<BlendTreeEntry> blendTree;

        public string Name
        {
            get { return name; }
            set
            {
                if (name == value)
                    return;

                hasUpdatedName = true;
                name = value;
            }
        }

        public static AnimationState SingleClip(string name, AnimationClip clip = null)
        {
            return new AnimationState
            {
                name = name,
                speed = 1d,
                type = AnimationStateType.SingleClip,
                hasUpdatedName = !name.StartsWith(DefaultSingleClipName),
                clip = clip
            };
        }

        public static AnimationState BlendTree1D(string name)
        {
            return new AnimationState
            {
                name = name,
                speed = 1d,
                type = AnimationStateType.BlendTree1D,
                blendVariable = "blend",
                blendTree = new List<BlendTreeEntry>(),
                hasUpdatedName = !name.StartsWith(Default1DBlendTreeName)
            };
        }

        public static AnimationState BlendTree2D(string name)
        {
            return new AnimationState
            {
                name = name,
                speed = 1d,
                type = AnimationStateType.BlendTree2D,
                blendVariable = "blend1",
                blendVariable2 = "blend1",
                blendTree = new List<BlendTreeEntry>(),
                hasUpdatedName = !name.StartsWith(Default1DBlendTreeName)
            };
        }

        public bool OnClipAssigned(AnimationClip clip)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = clip.name;
                return true;
            }
            else if (!hasUpdatedName)
            {
                name = clip.name;
                return true;
            }
            return false;
        }

        public float Duration
        {
            get
            {
                if (type == AnimationStateType.SingleClip)
                    return clip?.length ?? 0f;
                else
                {
                    var duration = 0f;
                    for (int i = 0; i < blendTree.Count; i++)
                    {
                        duration = Mathf.Max(duration, blendTree[i].clip?.length ?? 0f);
                    }
                    return duration;
                }
            }
        }

        public override string ToString()
        {
            return $"{name} ({type})";
        }

        public Playable GeneratePlayable(PlayableGraph graph,
                                         Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                         Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers)
        {
            switch (type)
            {
                case AnimationStateType.SingleClip:
                    var clipPlayable = AnimationClipPlayable.Create(graph, clip);
                    clipPlayable.SetSpeed(speed);
                    return clipPlayable;
                case AnimationStateType.BlendTree1D:
                    var treeMixer = AnimationMixerPlayable.Create(graph, blendTree.Count, true);
                    if (blendTree.Count == 0)
                        break;

                    float[] thresholds = new float[blendTree.Count];

                    for (int j = 0; j < blendTree.Count; j++)
                    {
                        var blendTreeEntry = blendTree[j];
                        clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                        clipPlayable.SetSpeed(speed);
                        graph.Connect(clipPlayable, 0, treeMixer, j);
                        thresholds[j] = blendTreeEntry.threshold;
                    }

                    treeMixer.SetInputWeight(0, 1f);
                    varTo1DBlendControllers?.GetOrAdd(blendVariable).Add(new BlendTreeController1D(treeMixer, thresholds));

                    return treeMixer;
                case AnimationStateType.BlendTree2D:
                    treeMixer = AnimationMixerPlayable.Create(graph, blendTree.Count, true);
                    if (blendTree.Count == 0)
                        break;

                    var controller = new BlendTreeController2D(blendVariable, blendVariable2, treeMixer, blendTree.Count);
                    varTo2DBlendControllers?.GetOrAdd(blendVariable).Add(controller);
                    varTo2DBlendControllers?.GetOrAdd(blendVariable2).Add(controller);

                    for (int j = 0; j < blendTree.Count; j++)
                    {
                        var blendTreeEntry = blendTree[j];
                        clipPlayable = AnimationClipPlayable.Create(graph, blendTreeEntry.clip);
                        clipPlayable.SetSpeed(speed);
                        graph.Connect(clipPlayable, 0, treeMixer, j);

                        controller.Add(j, blendTreeEntry.threshold, blendTreeEntry.threshold2);
                    }

                    treeMixer.SetInputWeight(0, 1f);
                    return treeMixer;
            }
            
            throw new Exception($"Unknown playable type {type}");
        }

        [Serializable]
        public class BlendTreeEntry
        {
            public float threshold;
            public float threshold2;
            public AnimationClip clip;
        }

        public enum AnimationStateType
        {
            SingleClip,
            BlendTree1D,
            BlendTree2D
        }
    }
}