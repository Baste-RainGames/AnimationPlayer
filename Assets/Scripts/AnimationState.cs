using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Animation_Player
{
    public abstract class AnimationState
    {
        [SerializeField]
        protected string name;
        [SerializeField]
        protected bool hasUpdatedName;

        //Index in the layer's state list. Since the AnimationStates are re-packed for serialization, this has to be stored seperately.
        [SerializeField]
        private int listIndex;
        public int ListIndex => listIndex;

        [SerializeField]
        protected SerializedGUID guid;
        public SerializedGUID GUID => guid;

        public double speed;

        //pseudo-constructor
        protected void Initialize(string name, string defaultName, int listIndex)
        {
            this.name = name;
            this.listIndex = listIndex;
            speed = 1d;
            hasUpdatedName = !name.StartsWith(defaultName);
            guid = SerializedGUID.Create();
        }

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

        public void EnsureHasGUID()
        {
            //Animation states used to not have guids!
            if (guid.GUID == Guid.Empty)
                guid = SerializedGUID.Create();
        }

        public bool OnClipAssigned(AnimationClip clip)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = clip.name;
                return true;
            }

            if (!hasUpdatedName)
            {
                name = clip.name;
                return true;
            }

            return false;
        }

        public abstract float Duration { get; }

        public override string ToString()
        {
            return $"{name} ({GetType().Name})";
        }

        public abstract Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers, Dictionary<string, float> blendVars);

        [Serializable]
        public class BlendTreeEntry
        {
            public float threshold;
            public float threshold2;
            public AnimationClip clip;
        }
    }
}