using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{

    [Serializable]
    public class AnimationLayer : ISerializationCallbackReceiver
    {
        //Serialized through ISerializationCallbackReceiver
        public List<AnimationState> animationStates;
        public List<StateTransition> transitions;

        public float startWeight;
        public AvatarMask mask;
        public AnimationLayerType type = AnimationLayerType.Override;

        public AnimationMixerPlayable stateMixer { get; private set; }
        public int NextListIndex => animationStates.Count == 0 ? 0 : animationStates[animationStates.Count - 1].ListIndex + 1;
        private int currentPlayedState;

        //blend info:
        private bool transitioning;
        private TransitionData currentTransitionData;
        private float transitionStartTime;
        private bool[] activeWhenBlendStarted;
        private float[] valueWhenBlendStarted;

        //transitionLookup[a, b] contains the index of the transition from a to b in transitions
        private int[,] transitionLookup;
        private Playable[] runtimePlayables;

        private readonly Dictionary<string, int> stateNameToIdx = new Dictionary<string, int>();

        //@TODO: string key is slow
        private readonly Dictionary<string, float> blendVars = new Dictionary<string, float>();
        private readonly Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers = new Dictionary<string, List<BlendTreeController1D>>();
        private readonly Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers = new Dictionary<string, List<BlendTreeController2D>>();

        public void InitializeSelf(PlayableGraph graph)
        {
            if (animationStates.Count == 0)
            {
                stateMixer = AnimationMixerPlayable.Create(graph, 0, false);
                return;
            }

            foreach (var transition in transitions)
            {
                transition.FetchStates(animationStates);
            }

            runtimePlayables = new Playable[animationStates.Count];

            stateMixer = AnimationMixerPlayable.Create(graph, animationStates.Count, false);
            stateMixer.SetInputWeight(0, 1f);
            currentPlayedState = 0;

            // Add the statess to the graph
            for (int i = 0; i < animationStates.Count; i++)
            {
                var state = animationStates[i];
                stateNameToIdx[state.Name] = i;

                var playable = state.GeneratePlayable(graph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);
                runtimePlayables[i] = playable;
                graph.Connect(playable, 0, stateMixer, i);
            }

            activeWhenBlendStarted = new bool[animationStates.Count];
            valueWhenBlendStarted = new float[animationStates.Count];

            transitionLookup = new int[animationStates.Count, animationStates.Count];
            for (int i = 0; i < animationStates.Count; i++)
            for (int j = 0; j < animationStates.Count; j++)
                transitionLookup[i, j] = -1;

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                var fromState = animationStates.IndexOf(transition.FromState);
                var toState = animationStates.IndexOf(transition.ToState);
                if (fromState == -1 || toState == -1)
                {
                    //TODO: fixme
                }
                else
                {
                    if (transitionLookup[fromState, toState] != -1)
                        Debug.LogWarning("Found two transitions from " + animationStates[fromState] + " to " + animationStates[toState]);

                    transitionLookup[fromState, toState] = i;
                }
            }
        }

        public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
        {
            graph.Connect(stateMixer, 0, layerMixer, layerIndex);

            layerMixer.SetInputWeight(layerIndex, startWeight);
            layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
            if (mask != null)
                layerMixer.SetLayerMaskFromAvatarMask((uint) layerIndex, mask);
        }

        public bool HasState(string stateName)
        {
            foreach (var state in animationStates)
            {
                if (state.Name == stateName)
                    return true;
            }

            return false;
        }

        public void PlayUsingExternalTransition(int state, TransitionData transitionData)
        {
            Play(state, transitionData);
        }

        public void PlayUsingInternalTransition(int state, TransitionData defaultTransition)
        {
            var transitionToUse = transitionLookup[currentPlayedState, state];
            var transition = transitionToUse == -1 ? defaultTransition : transitions[transitionToUse].transitionData;
            Play(state, transition);
        }

        private void Play(int state, TransitionData transitionData)
        {
            if (state < 0 || state >= animationStates.Count)
            {
                Debug.LogError($"Trying to play out of bounds clip {state}! There are {animationStates.Count} clips in the animation player");
                return;
            }

            if (transitionData.type == TransitionType.Curve && transitionData.curve == null)
            {
                Debug.LogError("Trying to play an animationCurve based transition, but the transition curve is null!");
                return;
            }

            var isCurrentlyPlaying = stateMixer.GetInputWeight(state) > 0f;
            if (!isCurrentlyPlaying)
            {
                runtimePlayables[state].SetTime(0f);
            }

            if (transitionData.duration <= 0f)
            {
                for (int i = 0; i < animationStates.Count; i++)
                {
                    stateMixer.SetInputWeight(i, i == state ? 1f : 0f);
                }

                currentPlayedState = state;
                transitioning = false;
            }
            else if (state != currentPlayedState)
            {
                for (int i = 0; i < animationStates.Count; i++)
                {
                    var currentMixVal = stateMixer.GetInputWeight(i);
                    activeWhenBlendStarted[i] = currentMixVal > 0f;
                    valueWhenBlendStarted[i] = currentMixVal;
                }

                transitioning = true;
                currentPlayedState = state;
                currentTransitionData = transitionData;
                transitionStartTime = Time.time;
            }
        }

        public void Update()
        {
            if (!transitioning)
                return;

            var lerpVal = (Time.time - transitionStartTime) / currentTransitionData.duration;
            if (currentTransitionData.type == TransitionType.Curve)
            {
                lerpVal = currentTransitionData.curve.Evaluate(lerpVal);
            }

            for (int i = 0; i < animationStates.Count; i++)
            {
                var isTargetClip = i == currentPlayedState;
                if (isTargetClip || activeWhenBlendStarted[i])
                {
                    var target = isTargetClip ? 1f : 0f;
                    stateMixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
                }
            }

            if (lerpVal >= 1)
                transitioning = false;
        }

        public float GetStateWeight(int state)
        {
            if (state < 0 || state >= animationStates.Count)
            {
                Debug.LogError($"Trying to get the state weight for {state}, which is out of bounds! There are {animationStates.Count} states!");
                return 0f;
            }

            return stateMixer.GetInputWeight(state);
        }

        public int GetStateIdx(string stateName)
        {
            int idx;
            if (stateNameToIdx.TryGetValue(stateName, out idx))
                return idx;
            return -1;
        }

        public bool IsBlending()
        {
            return transitioning;
        }

        public static AnimationLayer CreateLayer()
        {
            var layer = new AnimationLayer
            {
                animationStates = new List<AnimationState>(),
                transitions = new List<StateTransition>(),
                startWeight = 1f
            };
            return layer;
        }

        public void SetBlendVar(string var, float value)
        {
            blendVars[var] = value;

            List<BlendTreeController1D> blendControllers1D;
            if (varTo1DBlendControllers.TryGetValue(var, out blendControllers1D))
                foreach (var controller in blendControllers1D)
                    controller.SetValue(value);

            List<BlendTreeController2D> blendControllers2D;
            if (varTo2DBlendControllers.TryGetValue(var, out blendControllers2D))
                foreach (var controller in blendControllers2D)
                    controller.SetValue(var, value);
        }

        public float GetBlendVar(string var)
        {
            float result = 0f;
            blendVars.TryGetValue(var, out result);
            return result;
        }

        public void AddAllPlayingStatesTo(List<AnimationState> results)
        {
            results.Add(animationStates[currentPlayedState]);

            for (var i = 0; i < animationStates.Count; i++)
            {
                if (i == currentPlayedState)
                    return;
                var state = animationStates[i];
                if (stateMixer.GetInputWeight(i) > 0f)
                {
                    results.Add(state);
                }
            }
        }

        public void AddTreesMatchingBlendVar(BlendVarController aggregateController, string blendVar)
        {
            List<BlendTreeController1D> blendControllers1D;
            if (varTo1DBlendControllers.TryGetValue(blendVar, out blendControllers1D))
                aggregateController.AddControllers(blendControllers1D);

            List<BlendTreeController2D> blendControllers2D;
            if (varTo2DBlendControllers.TryGetValue(blendVar, out blendControllers2D))
                aggregateController.AddControllers(blendControllers2D);
        }

        public AnimationState GetCurrentPlayingState()
        {
            if (animationStates.Count == 0)
                return null;
            return animationStates[currentPlayedState];
        }

        public void AddAllBlendVarsTo(List<string> result)
        {
            foreach (var key in blendVars.Keys)
            {
                result.Add(key);
            }
        }

#if UNITY_EDITOR
        public GUIContent[] layersForEditor;
#endif

        [SerializeField]
        private List<SingleClipState> serializedSingleClipStates = new List<SingleClipState>();
        [SerializeField]
        private List<BlendTree1D> serializedBlendTree1Ds = new List<BlendTree1D>();
        [SerializeField]
        private List<BlendTree2D> serializedBlendTree2Ds = new List<BlendTree2D>();

        public void OnBeforeSerialize()
        {
            if (serializedSingleClipStates == null)
                serializedSingleClipStates = new List<SingleClipState>();
            else
                serializedSingleClipStates.Clear();

            if (serializedBlendTree1Ds == null)
                serializedBlendTree1Ds = new List<BlendTree1D>();
            else
                serializedBlendTree1Ds.Clear();

            if (serializedBlendTree2Ds == null)
                serializedBlendTree2Ds = new List<BlendTree2D>();
            else
                serializedBlendTree2Ds.Clear();

            //@TODO: Once Unity hits C# 7.0, this can be done through pattern matching. And oh how glorious it will be! 
            foreach (var state in animationStates)
            {
                var asSingleClip = state as SingleClipState;
                if (asSingleClip != null)
                {
                    serializedSingleClipStates.Add(asSingleClip);
                    continue;
                }

                var as1DBlendTree = state as BlendTree1D;
                if (as1DBlendTree != null)
                {
                    serializedBlendTree1Ds.Add(as1DBlendTree);
                    continue;
                }

                var as2DBlendTree = state as BlendTree2D;
                if (as2DBlendTree != null)
                {
                    serializedBlendTree2Ds.Add(as2DBlendTree);
                    continue;
                }

                if (state != null)
                    Debug.LogError($"Found state in AnimationLayer's states that's of an unknown type, " +
                                   $"({state.GetType().Name})! Did you forget to implement the serialization?");
            }
        }

        public void OnAfterDeserialize()
        {
            if (animationStates == null)
                animationStates = new List<AnimationState>();
            else
                animationStates.Clear();

            foreach (var state in serializedSingleClipStates)
                animationStates.Add(state);

            foreach (var state in serializedBlendTree1Ds)
                animationStates.Add(state);

            foreach (var state in serializedBlendTree2Ds)
                animationStates.Add(state);

            serializedSingleClipStates.Clear();
            serializedBlendTree1Ds.Clear();
            serializedBlendTree2Ds.Clear();

            animationStates.Sort(CompareListIndices);
        }

        private int CompareListIndices(AnimationState x, AnimationState y)
        {
            return x.ListIndex.CompareTo(y.ListIndex);
        }
    }

}