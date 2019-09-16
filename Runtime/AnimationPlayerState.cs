using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public abstract class AnimationPlayerState
    {
        [SerializeField]
        private string name;

        [SerializeField]
        private bool hasUpdatedName;

        [SerializeField]
        private SerializedGUID guid;

        public SerializedGUID GUID => guid;

        public List<AnimationEvent> animationEvents = new List<AnimationEvent>();
        public double               speed           = 1d;

        //pseudo-constructor
        protected void Initialize(string name, string defaultName)
        {
            this.name = name;
            speed = 1d;
            hasUpdatedName = !name.StartsWith(defaultName);
            guid = SerializedGUID.Create();
        }

        public string Name
        {
            get => name;
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

        public void HandleAnimationEvents(double timeLastFrame, double timeCurrentFrame, float currentWeight, bool firstFrame, bool isActiveState)
        {
            if (Loops)
            {
                var delta = timeCurrentFrame - timeLastFrame;
                // @TODO, bug This means that clips set to play on the last frame of the state won't play.
                timeCurrentFrame %= Duration;

                if (firstFrame)
                {
                    //Otherwise animation events set to time 0 wold not fire when the AnimationPlayer starts.
                    timeLastFrame = -1f;
                }
                else
                {
                    //This moves the time last frame to before time 0 if we looped, which makes the code under easier.
                    timeLastFrame = timeCurrentFrame - delta;
                }
            }

            foreach (var animationEvent in animationEvents)
            {
                if (currentWeight < animationEvent.minWeight)
                    continue;
                if (animationEvent.mustBeActiveState && !isActiveState)
                    continue;

                var lastFrameBefore = timeLastFrame < animationEvent.time;
                var currentFrameAfter = timeCurrentFrame >= animationEvent.time;
                if (lastFrameBefore && currentFrameAfter)
                {
                    animationEvent.InvokeRegisteredListeners();
                }
            }
        }

        public abstract float Duration { get; }

        public abstract bool Loops { get; }

        public override string ToString()
        {
            return $"{name} ({GetType().Name})";
        }

        public abstract Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                  Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                  List<BlendTreeController2D> all2DControllers, Dictionary<string, float> blendVars);

        internal abstract void SetRuntimePlayable(Playable runtimePlayable);

        public virtual void OnWillStartPlaying(ref Playable ownPlayable) { }

        public abstract void JumpToRelativeTime(float time);
    }
}