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

        private List<ClipSwapCollection> clipSwapCollections;

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

        protected AnimationClip GetClipToUseFor(AnimationClip originalClip)
        {
            if (clipSwapCollections != null)
            {
                foreach (var clipSwapCollection in clipSwapCollections)
                {
                    if (clipSwapCollection.TryGetSwapFor(originalClip, out var swappedClip))
                        return swappedClip;
                }
            }

            return originalClip;
        }

        public abstract float Duration { get; }

        public abstract bool Loops { get; }

        public override string ToString()
        {
            return $"{name} ({GetType().Name})";
        }

        public Playable Initialize(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
            Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
            List<BlendTreeController2D> all2DControllers,
            List<ClipSwapCollection> clipSwapCollections)
        {
            this.clipSwapCollections = clipSwapCollections;
            return GeneratePlayable(graph, varTo1DBlendControllers, varTo2DBlendControllers, all2DControllers);
        }

        public abstract Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                                       Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                                       List<BlendTreeController2D> all2DControllers);

        public virtual void OnWillStartPlaying(ref Playable ownPlayable) { }

        public abstract void JumpToRelativeTime(ref Playable ownPlayable, float time);

        public abstract void OnClipSwapsChanged(ref Playable ownPlayable);

        protected struct ClipSwapHandler
        {
            public readonly List<AnimationClip>  clips;
            private readonly AnimationPlayerState state;

            public ClipSwapHandler(AnimationPlayerState state, List<AnimationClip> clips)
            {
                this.state = state;
                this.clips = clips;
            }

            public AnimationClip this[int index] => state.GetClipToUseFor(clips[index]);
            public int Count => clips.Count;

            public int IndexOf(AnimationClip animationClip)
            {
                for (int i = 0; i < clips.Count; i++)
                    if (state.GetClipToUseFor(clips[i]) == animationClip)
                        return i;
                return -1;
            }

            public ClipSwapEnumerator GetEnumerator()
            {
                return new ClipSwapEnumerator(clips, state);
            }

            public struct ClipSwapEnumerator
            {
                private List<AnimationClip> clips;
                private AnimationPlayerState state;

                private int index;

                public ClipSwapEnumerator(List<AnimationClip> clips, AnimationPlayerState state)
                {
                    this.state = state;
                    this.clips = clips;
                    index = 0;
                }

                public bool MoveNext()
                {
                    index++;
                    return index < clips.Count;
                }

                public AnimationClip Current => state.GetClipToUseFor(clips[index]);
            }
        }

        public abstract void RegisterUsedBlendVarsIn(Dictionary<string, float> blendVariableValues);
    }
}