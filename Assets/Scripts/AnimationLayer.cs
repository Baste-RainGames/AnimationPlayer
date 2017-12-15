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
        public List<AnimationState> states;
        public List<StateTransition> transitions;

        public float startWeight;
        public AvatarMask mask;
        public AnimationLayerType type = AnimationLayerType.Override;

        private PlayableGraph containingGraph;
        public AnimationMixerPlayable stateMixer { get; private set; }
        private int currentPlayedState;

        //blend info:
        private bool transitioning;
        private TransitionData currentTransitionData;
        private float transitionStartTime;
        private List<bool> activeWhenBlendStarted;
        private List<float> valueWhenBlendStarted;

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
            containingGraph = graph;
            if (states.Count == 0)
            {
                stateMixer = AnimationMixerPlayable.Create(graph, 0, false);
                return;
            }

            foreach (var transition in transitions)
            {
                transition.FetchStates(states);
            }

            runtimePlayables = new Playable[states.Count];

            stateMixer = AnimationMixerPlayable.Create(graph, states.Count, false);
            stateMixer.SetInputWeight(0, 1f);
            currentPlayedState = 0;

            // Add the statess to the graph
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                stateNameToIdx[state.Name] = i;

                var playable = state.GeneratePlayable(graph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);
                runtimePlayables[i] = playable;
                graph.Connect(playable, 0, stateMixer, i);
            }

            activeWhenBlendStarted = new List<bool>();
            valueWhenBlendStarted = new List<float>();
            for (int i = 0; i < states.Count; i++)
            {
                activeWhenBlendStarted.Add(false);
                valueWhenBlendStarted.Add(0f);
            }

            transitionLookup = new int[states.Count, states.Count];
            for (int i = 0; i < states.Count; i++)
            for (int j = 0; j < states.Count; j++)
                transitionLookup[i, j] = -1;

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                var fromState = states.IndexOf(transition.FromState);
                var toState = states.IndexOf(transition.ToState);
                if (fromState == -1 || toState == -1)
                {
                    //TODO: fixme
                }
                else
                {
                    if (transitionLookup[fromState, toState] != -1)
                        Debug.LogWarning("Found two transitions from " + states[fromState] + " to " + states[toState]);

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
            foreach (var state in states)
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
            if (state < 0 || state >= states.Count)
            {
                Debug.LogError($"Trying to play out of bounds clip {state}! There are {states.Count} clips in the animation player");
                return;
            }

            if (transitionData.type == TransitionType.Curve && transitionData.curve == null)
            {
                Debug.LogError("Trying to play an animationCurve based transition, but the transition curve is null!");
                return;
            }

            var currentWeightOfState = stateMixer.GetInputWeight(state);
            var isCurrentlyPlaying = currentWeightOfState > 0f;
            if (isCurrentlyPlaying)
            {
                if (!states[state].Loops)
                {
                    // We need to blend to a state currently playing, but want to blend to it at time 0, as it's not looping.
                    // So we do this:
                    // Move the old version of the state to a new spot in the mixer, copy over the time and weight
                    // Create a new version of the state at the old spot
                    // Blend to the new state

                    var original = runtimePlayables[state];
                    var copy = states[state].GeneratePlayable(containingGraph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);
                    var copyIndex = stateMixer.GetInputCount();
                    stateMixer.SetInputCount(copyIndex + 1);

                    containingGraph.Connect(copy, 0, stateMixer, copyIndex);

                    activeWhenBlendStarted.Add(true);
                    valueWhenBlendStarted.Add(currentWeightOfState);

                    copy.SetTime(original.GetTime());
                    original.SetTime(0);

                    stateMixer.SetInputWeight(copyIndex, currentWeightOfState);
                    stateMixer.SetInputWeight(state, 0);
                }
            }
            else
            {
                runtimePlayables[state].SetTime(0f);
            }

            if (transitionData.duration <= 0f)
            {
                for (int i = 0; i < stateMixer.GetInputCount(); i++)
                {
                    stateMixer.SetInputWeight(i, i == state ? 1f : 0f);
                }

                currentPlayedState = state;
                transitioning = false;
            }
            else
            {
                for (int i = 0; i < stateMixer.GetInputCount(); i++)
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

            for (int i = 0; i < stateMixer.GetInputCount(); i++)
            {
                var isTargetClip = i == currentPlayedState;
                if (isTargetClip || activeWhenBlendStarted[i])
                {
                    var target = isTargetClip ? 1f : 0f;
                    stateMixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
                }
            }

            var currentInputCount = stateMixer.GetInputCount();
            if (currentInputCount > states.Count)
            {
                //Have added additional copies of states to handle transitions. Clean these up if they have too low weight, to avoid excessive playable counts.
                for (int i = currentInputCount - 1; i >= states.Count; i--)
                {
                    if (stateMixer.GetInputWeight(i) < 0.01f)
                    {
                        activeWhenBlendStarted.RemoveAt(i);
                        valueWhenBlendStarted.RemoveAt(i);

                        var removedPlayable = stateMixer.GetInput(i);
                        removedPlayable.Destroy();
    
                        //Shift all excess playables one index down.
                        for (int j = i + 1; j < stateMixer.GetInputCount(); j++)
                        {
                            var playable = stateMixer.GetInput(j);
                            var weight = stateMixer.GetInputWeight(j);
                            containingGraph.Disconnect(stateMixer, j);
                            containingGraph.Connect(playable, 0, stateMixer, j - 1);
                            stateMixer.SetInputWeight(j - 1, weight);
                        }

                        stateMixer.SetInputCount(stateMixer.GetInputCount() - 1);
                    }
                }
            }

            if (lerpVal >= 1)
            {
                transitioning = false;
                if(states.Count != stateMixer.GetInputCount())
                    throw new Exception($"{states.Count} != {stateMixer.GetInputCount()}");
            }
        }

        public float GetStateWeight(int state)
        {
            if (state < 0 || state >= states.Count)
            {
                Debug.LogError($"Trying to get the state weight for {state}, which is out of bounds! There are {states.Count} states!");
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
                states = new List<AnimationState>(),
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
            results.Add(states[currentPlayedState]);

            for (var i = 0; i < states.Count; i++)
            {
                if (i == currentPlayedState)
                    return;
                var state = states[i];
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
            if (states.Count == 0)
                return null;
            return states[currentPlayedState];
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
        [SerializeField]
        private SerializedGUID[] serializedStateOrder;

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
            foreach (var state in states)
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

            serializedStateOrder = new SerializedGUID[states.Count];
            for (int i = 0; i < states.Count; i++)
            {
                serializedStateOrder[i] = states[i].GUID;
            }
        }

        public void OnAfterDeserialize()
        {
            if (states == null)
                states = new List<AnimationState>();
            else
                states.Clear();

            foreach (var state in serializedSingleClipStates)
                states.Add(state);

            foreach (var state in serializedBlendTree1Ds)
                states.Add(state);

            foreach (var state in serializedBlendTree2Ds)
                states.Add(state);

            serializedSingleClipStates.Clear();
            serializedBlendTree1Ds.Clear();
            serializedBlendTree2Ds.Clear();

            states.Sort(CompareListIndices);
        }

        private int CompareListIndices(AnimationState x, AnimationState y)
        {
            var xIndex = Array.IndexOf(serializedStateOrder, x.GUID);
            var yIndex = Array.IndexOf(serializedStateOrder, y.GUID);
            if (xIndex < yIndex)
                return -1;
            return xIndex > yIndex ? 1 : 0;
        }
    }

}