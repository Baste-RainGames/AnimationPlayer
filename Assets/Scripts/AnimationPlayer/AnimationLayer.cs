using System;
using System.Linq;
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
        private double timeOfPlayedStateLastFrame;
        private bool firstFrame = true;

        //blend info:
        private bool transitioning;
        private TransitionData currentTransitionData;
        private float transitionStartTime;
        private List<bool> activeWhenBlendStarted;
        private List<float> valueWhenBlendStarted;

        // transitionLookup[a, b] contains the index of the transition from a to b in transitions
        // transitionLookup[x, y] == -1 means that there is no transition defined between the states.
        private int[,] transitionLookup;
        private Playable[] runtimePlayables;

        private readonly Dictionary<string, int> stateNameToIdx = new Dictionary<string, int>();

        //@TODO: string key is slow
        private readonly Dictionary<string, float> blendVars = new Dictionary<string, float>();
        private readonly Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers = new Dictionary<string, List<BlendTreeController1D>>();
        private readonly Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers = new Dictionary<string, List<BlendTreeController2D>>();

        private PlayAtTimeInstructionQueue playInstructionQueue = new PlayAtTimeInstructionQueue();

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

            activeWhenBlendStarted = new List<bool>(states.Count);
            valueWhenBlendStarted = new List<float>(states.Count);
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

            if (layerIndex == 0)
            {
                //Doesn't make any sense for base layer to be additive!
                layerMixer.SetLayerAdditive((uint) layerIndex, false);
            }
            else
            {
                layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
            }
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

        private void Play(int newState, TransitionData transitionData, bool clearQueuedPlayInstructions = true)
        {
            if (clearQueuedPlayInstructions)
                playInstructionQueue.Clear();

            if (newState < 0 || newState >= states.Count)
            {
                Debug.LogError($"Trying to play out of bounds clip {newState}! There are {states.Count} clips in the animation player");
                return;
            }

            if (transitionData.type == TransitionType.Curve && transitionData.curve == null)
            {
                Debug.LogError("Trying to play an animationCurve based transition, but the transition curve is null!");
                return;
            }

            var currentWeightOfState = stateMixer.GetInputWeight(newState);
            var isCurrentlyPlaying = currentWeightOfState > 0f;

            if (!isCurrentlyPlaying)
            {
                runtimePlayables[newState].SetTime(0f);
            }
            else if (!states[newState].Loops)
            {
                // We need to blend to a state currently playing, but since it's not looping, blending to a time different than 0 would look bad. 
                // So we do this:
                // Move the old version of the state to a new spot in the mixer, copy over the time and weight to that new spot.
                // Create a new version of the state at the old spot
                // Blend to the new state
                // Later, when the copy's weight hits zero, we discard it.

                var original = runtimePlayables[newState];
                var copy = states[newState].GeneratePlayable(containingGraph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);
                var copyIndex = stateMixer.GetInputCount();
                stateMixer.SetInputCount(copyIndex + 1);

                containingGraph.Connect(copy, 0, stateMixer, copyIndex);

                activeWhenBlendStarted.Add(true);
                valueWhenBlendStarted.Add(currentWeightOfState);

                copy.SetTime(original.GetTime());

                stateMixer.SetInputWeight(copyIndex, currentWeightOfState);
                stateMixer.SetInputWeight(newState, 0);
            }

            states[newState].OnWillStartPlaying(containingGraph, stateMixer, newState, ref runtimePlayables[newState]);

            timeOfPlayedStateLastFrame = runtimePlayables[newState].GetTime();

            if (transitionData.duration <= 0f)
            {
                for (int i = 0; i < stateMixer.GetInputCount(); i++)
                {
                    stateMixer.SetInputWeight(i, i == newState ? 1f : 0f);
                }

                currentPlayedState = newState;
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
                currentPlayedState = newState;
                currentTransitionData = transitionData;
                transitionStartTime = Time.time;
            }
        }

        public void PlayAfterSeconds(float seconds, int state, TransitionData transition)
        {
            playInstructionQueue.Enqueue(new PlayAtTimeInstruction(Time.time + seconds, state, transition));
        }

        public void JumpToRelativeTime(float time) 
        {
            if (time > 1f) 
            {
                time = time % 1f;
            }
            else if (time < 0f) 
            {
                time = 1 - ((-time) % 1f);
            }

            for (int i = 0; i < stateMixer.GetInputCount(); i++) 
            {
                stateMixer.SetInputWeight(i, i == currentPlayedState ? 1f : 0f);
            }

            ClearFinishedTransitionStates();

            stateMixer.GetInput(currentPlayedState).SetTime(time * states[currentPlayedState].Duration);
        }

        public void Update()
        {
            if (states.Count == 0)
                return;
            HandleAnimationEvents();
            HandleTransitions();
            HandleQueuedInstructions();
            firstFrame = false;
        }

        private void HandleAnimationEvents()
        {
            var currentTime = stateMixer.GetInput(currentPlayedState).GetTime();
            states[currentPlayedState].HandleAnimationEvents(timeOfPlayedStateLastFrame, currentTime, firstFrame);
            timeOfPlayedStateLastFrame = currentTime;
        }

        private void HandleTransitions()
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
            if (currentInputCount > states.Count) {
                ClearFinishedTransitionStates();
            }

            if (lerpVal >= 1)
            {
                transitioning = false;
                if (states.Count != stateMixer.GetInputCount())
                    throw new Exception($"{states.Count} != {stateMixer.GetInputCount()}");
            }
        }

        /// <summary>
        /// We generate some extra states at times to handle blending properly. This cleans out the ones of those that are done blending out.
        /// </summary>
        private void ClearFinishedTransitionStates() 
        {
            var currentInputCount = stateMixer.GetInputCount();
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

        private void HandleQueuedInstructions()
        {
            while (playInstructionQueue.Count > 0 && playInstructionQueue.Peek().playAtTime < Time.time)
            {
                var instruction = playInstructionQueue.Peek();
                Play(instruction.stateToPlay, instruction.transition, false);
                playInstructionQueue.Pop();
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

        public int GetIndexOfPlayingState() {
            if (states.Count == 0)
                return -1;
            return currentPlayedState;
        }

        public void AddAllBlendVarsTo(List<string> result)
        {
            foreach (var key in blendVars.Keys)
            {
                result.Add(key);
            }
        }

        public bool HasBlendTreeUsingBlendVar(string blendVar)
        {
            return blendVars.Keys.Contains(blendVar);
        }

        public void SwapClipOnState(int state, AnimationClip clip, PlayableGraph graph) {
            var animationState = states[state];
            if (!(animationState is SingleClip)) {
                Debug.LogError($"Trying to swap the clip on the state {animationState.Name}, " +
                               $"but it is a {animationState.GetType().Name}! Only SingleClipState is supported");
            }

            var singleClipState = (SingleClip) animationState;
            singleClipState.clip = clip;
            var newPlayable = singleClipState.GeneratePlayable(graph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);
            var currentPlayable = (AnimationClipPlayable) stateMixer.GetInput(state);

            var oldWeight = stateMixer.GetInputWeight(state);
            graph.Disconnect(stateMixer, state);
            currentPlayable.Destroy();
            stateMixer.ConnectInput(state, newPlayable, 0);
            stateMixer.SetInputWeight(state, oldWeight);
        }

        public void AddState(AnimationState state)
        {
            states.Add(state);
            var playable = state.GeneratePlayable(containingGraph, varTo1DBlendControllers, varTo2DBlendControllers, blendVars);

            var indexOfNew = states.Count - 1;
            stateNameToIdx[state.Name] = indexOfNew;

            Array.Resize(ref runtimePlayables, runtimePlayables.Length + 1);
            runtimePlayables[runtimePlayables.Length - 1] = playable;

            activeWhenBlendStarted.Add(false);
            valueWhenBlendStarted.Add(0f);

            var newLookup = new int[states.Count, states.Count];
            for (int i = 0; i < transitionLookup.GetLength(0); i++)
            for (int j = 0; j < transitionLookup.GetLength(1); j++)
            {
                newLookup[i, j] = transitionLookup[i, j];
            }

            for (int i = 0; i < states.Count; i++)
            {
                newLookup[i, indexOfNew] = -1;
                newLookup[indexOfNew, i] = -1;
            }

            transitionLookup = newLookup;
            
            stateMixer.SetInputCount(stateMixer.GetInputCount() + 1);

            //Shift all excess playables (ie blend helpers) one index up. Since their order doesn't count, could just swap the first one to the last index?
            for (int i = stateMixer.GetInputCount() - 1; i > states.Count; i--)
            {
                var p = stateMixer.GetInput(i);
                var weight = stateMixer.GetInputWeight(i);
                containingGraph.Disconnect(stateMixer, i - 1);
                containingGraph.Connect(p, 0, stateMixer, i);
                stateMixer.SetInputWeight(i, weight);
            }

            containingGraph.Connect(playable, 0, stateMixer, states.Count - 1);
        }

#if UNITY_EDITOR
        public GUIContent[] layersForEditor;
#endif

        [SerializeField]
        private List<SingleClip> serializedSingleClipStates = new List<SingleClip>();
        [SerializeField]
        private List<BlendTree1D> serializedBlendTree1Ds = new List<BlendTree1D>();
        [SerializeField]
        private List<BlendTree2D> serializedBlendTree2Ds = new List<BlendTree2D>();
        [SerializeField]
        private List<PlayRandomClip> serializedSelectRandomStates = new List<PlayRandomClip>();
        [SerializeField]
        private SerializedGUID[] serializedStateOrder;

        public void OnBeforeSerialize()
        {
            if (serializedSingleClipStates == null)
                serializedSingleClipStates = new List<SingleClip>();
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
            if(serializedSelectRandomStates == null)
                serializedSelectRandomStates = new List<PlayRandomClip>();
            else 
                serializedSelectRandomStates.Clear();

            //@TODO: Once Unity hits C# 7.0, this can be done through pattern matching. And oh how glorious it will be! 
            foreach (var state in states)
            {
                var asSingleClip = state as SingleClip;
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

                var asRandom = state as PlayRandomClip;
                if (asRandom != null)
                {
                    serializedSelectRandomStates.Add(asRandom);
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

            //AddRangde allocates. No, really!
            foreach (var state in serializedSingleClipStates)
                states.Add(state);

            foreach (var state in serializedBlendTree1Ds)
                states.Add(state);

            foreach (var state in serializedBlendTree2Ds)
                states.Add(state);

            foreach (var state in serializedSelectRandomStates)
                states.Add(state);

            serializedSingleClipStates.Clear();
            serializedBlendTree1Ds.Clear();
            serializedBlendTree2Ds.Clear();
            serializedSelectRandomStates.Clear();

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

        private struct PlayAtTimeInstruction
        {
            public float playAtTime;
            public int stateToPlay;
            public TransitionData transition;

            public PlayAtTimeInstruction(float playAtTime, int stateToPlay, TransitionData transition)
            {
                this.playAtTime = playAtTime;
                this.stateToPlay = stateToPlay;
                this.transition = transition;
            }
        }

        /// <summary>
        /// Ordered queue of play at time instructions
        /// </summary>
        private class PlayAtTimeInstructionQueue
        {
            public int Count { get; private set; }
            private const int bufferSizeIncrement = 10;
            private PlayAtTimeInstruction[] instructions = new PlayAtTimeInstruction[bufferSizeIncrement];

            public void Enqueue(PlayAtTimeInstruction instruction)
            {
                if (Count == instructions.Length)
                {
                    Array.Resize(ref instructions, instructions.Length + bufferSizeIncrement);
                }

                var toInsert = instruction;
                for (int i = 0; i < Count - 1; i++)
                {
                    if (instruction.playAtTime < instructions[i].playAtTime)
                    {
                        var temp = toInsert;
                        toInsert = instructions[i];
                        instructions[i] = temp;
                    }
                }

                instructions[Count] = toInsert;
                Count++;
            }

            public PlayAtTimeInstruction Peek()
            {
                Debug.Assert(Count > 0, "Trying to peek play at time instructions, but there's no instructions!");
                return instructions[0];
            }

            public void Pop()
            {
                for (int i = 0; i < Count - 1; i++)
                {
                    instructions[i] = instructions[i + 1];
                }

                Count--;
            }

            public void Clear()
            {
                Count = 0;
            }
        }
    }
}