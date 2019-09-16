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
        public List<AnimationPlayerState> states;
        public List<StateTransition> transitions;

        public string name;
        public float startWeight;
        public AvatarMask mask;
        public AnimationLayerType type = AnimationLayerType.Override;

        private PlayableGraph containingGraph;
        public AnimationMixerPlayable stateMixer { get; private set; }
        private int currentPlayedState;
        private bool firstFrame = true;
        private bool anyStatesHasAnimationEvents;
        private TransitionData defaultTransition;
        private List<ClipSwapCollection> clipSwapCollections;

        private AnimationLayerMixerPlayable layerMixer; //only use for re-initting!
        private int layerIndex; //only use for re-initting!

        //blend info:
        private bool transitioning;
        private TransitionData currentTransitionData;
        private string currentTransitionName;
        private float transitionStartTime;
        private List<bool> activeWhenBlendStarted;
        private List<float> valueWhenBlendStarted;
        private List<double> timeLastFrame;

        // transitionLookup[a, b] contains the index of the transition from a to b in transitions
        // transitionLookup[x, y] == -1 means that there is no transition defined between the states.
        private int[,] transitionLookup;
        private Playable[] runtimePlayables;

        private readonly Dictionary<string, int> stateNameToIdx = new Dictionary<string, int>(StringComparer.InvariantCulture);

        //@TODO: string key is slow
        private readonly Dictionary<string, float> blendVars = new Dictionary<string, float>();
        private readonly Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers = new Dictionary<string, List<BlendTreeController1D>>();
        private readonly Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers = new Dictionary<string, List<BlendTreeController2D>>();
        private readonly List<BlendTreeController2D> all2DControllers = new List<BlendTreeController2D>();

        private PlayAtTimeInstructionQueue playInstructionQueue;

        public void InitializeSelf(PlayableGraph graph, TransitionData defaultTransition, List<ClipSwapCollection> clipSwapCollections)
        {
            this.defaultTransition = defaultTransition;
            this.clipSwapCollections = clipSwapCollections;
            playInstructionQueue = new PlayAtTimeInstructionQueue(this);

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
                if (state.animationEvents.Count > 0)
                    anyStatesHasAnimationEvents = true;

                stateNameToIdx[state.Name] = i;

                var playable = state.Initialize(graph, varTo1DBlendControllers, varTo2DBlendControllers, all2DControllers, blendVars, clipSwapCollections);
                runtimePlayables[i] = playable;
                graph.Connect(playable, 0, stateMixer, i);
            }

            activeWhenBlendStarted = new List<bool>(states.Count);
            valueWhenBlendStarted = new List<float>(states.Count);
            timeLastFrame = new List<double>();
            for (int i = 0; i < states.Count; i++)
            {
                activeWhenBlendStarted.Add(false);
                valueWhenBlendStarted.Add(0f);
                timeLastFrame.Add(0f);
            }

            transitionLookup = new int[states.Count, states.Count];
            for (int i = 0; i < states.Count; i++)
            for (int j = 0; j < states.Count; j++)
                transitionLookup[i, j] = -1;

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (!transition.isDefault)
                    continue;

                var fromState = states.IndexOf(transition.FromState);
                var toState = states.IndexOf(transition.ToState);
                if (fromState == -1 || toState == -1)
                {
                    //TODO: fixme
                }
                else
                {
                    if (transitionLookup[fromState, toState] != -1)
                        Debug.LogWarning($"Found two default transitions from {states[fromState]} to {states[toState]}");

                    transitionLookup[fromState, toState] = i;
                }
            }
        }

        public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
        {
            this.layerMixer = layerMixer;
            this.layerIndex = layerIndex;

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

        public AnimationPlayerState Play(int state)
        {
            var (transitionData, transitionName) = FindCorrectTransition(state);
            return Play(state, transitionData, transitionName, true);
        }

        public AnimationPlayerState Play(int state, string transition)
        {
            var transitionFrom = states[currentPlayedState];
            var transitionTo = states[state];

            foreach (var t in transitions)
            {
                if (t.FromState == transitionFrom && t.ToState == transitionTo && t.name == transition)
                {
                    return Play(state, t.transitionData, transition);
                }
            }

            Debug.LogError($"Couldn't find a transition from {transitionFrom.Name} to {transitionTo.Name} with the name {transition}. Using default transition");
            return Play(state);
        }

        public AnimationPlayerState Play(int state, TransitionData transition, string transitionName)
        {
            return Play(state, transition, transitionName, true);
        }

        private (TransitionData data, string name) FindCorrectTransition(int stateToPlay) {
            return GetDefaultTransitionFromTo(currentPlayedState, stateToPlay);
        }

        private AnimationPlayerState Play(int newState, TransitionData transitionData, string transitionName, bool clearQueuedPlayInstructions)
        {
            if (clearQueuedPlayInstructions)
                playInstructionQueue.Clear();

            if (newState < 0 || newState >= states.Count)
            {
                Debug.LogError($"Trying to play out of bounds clip {newState}! There are {states.Count} clips in the animation player");
                return null;
            }

            if (transitionData.type == TransitionType.Curve && transitionData.curve == null)
            {
                Debug.LogError("Trying to play an animationCurve based transition, but the transition curve is null!");
                return null;
            }

            if (transitionData.duration <= 0f)
                DoInstantTransition();
            else if (transitionData.type == TransitionType.Clip)
                StartClipTransition();
            else
                StartTimedTransition();

            states[newState].OnWillStartPlaying(ref runtimePlayables[newState]);

            return states[newState];

            // HELPERS:

            void DoInstantTransition() {
                for (int i = 0; i < stateMixer.GetInputCount(); i++)
                    stateMixer.SetInputWeight(i, 0);

                stateMixer.SetInputWeight(newState, 1);
                runtimePlayables[newState].SetTime(0);
                currentPlayedState = newState;
                transitioning      = false;

                ClearFinishedTransitionStates();
            }

            void StartClipTransition() {
                var transitionState = AnimationClipPlayable.Create(containingGraph, transitionData.clip);

                var transitionStateindex = stateMixer.GetInputCount();
                stateMixer.SetInputCount(transitionStateindex + 1);

                containingGraph.Connect(transitionState, 0, stateMixer, transitionStateindex);

                for (int i = 0; i < transitionStateindex; i++)
                    stateMixer.SetInputWeight(i, 0);
                stateMixer.SetInputWeight(transitionStateindex, 1);

                transitioning = true;
                transitionStartTime = Time.time;
                currentTransitionData = transitionData;
                currentTransitionName = transitionName;
                currentPlayedState = newState;

                activeWhenBlendStarted.Add(true);
                valueWhenBlendStarted.Add(1);
            }

            void StartTimedTransition() {
                var currentWeightOfState = stateMixer.GetInputWeight(newState);
                var isCurrentlyPlaying   = currentWeightOfState > 0f;

                if (!isCurrentlyPlaying) {
                    runtimePlayables[newState].SetTime(0f);
                    //This makes animation events set to time 0 play, by simulating that the frame jumped forwards a second.
                    timeLastFrame[newState] = -1f;
                }
                else if (!states[newState].Loops) {
                    if (transitionData.duration <= 0f) {
                        runtimePlayables[newState].SetTime(0);
                    }
                    else {
                        // We need to blend to a state currently playing, but since it's not looping, blending to a time different than 0 would look bad.
                        // So we do this:
                        // Move the old version of the state to a new spot in the mixer, copy over the time and weight to that new spot.
                        // Create a new version of the state at the old spot
                        // Blend to the new state
                        // Later, when the copy's weight hits zero, we discard it.

                        var original = runtimePlayables[newState];
                        var copy = states[newState].GeneratePlayable(containingGraph, varTo1DBlendControllers, varTo2DBlendControllers, all2DControllers, blendVars);
                        var copyIndex = stateMixer.GetInputCount();
                        stateMixer.SetInputCount(copyIndex + 1);

                        containingGraph.Connect(copy, 0, stateMixer, copyIndex);

                        activeWhenBlendStarted.Add(true);
                        valueWhenBlendStarted.Add(currentWeightOfState);

                        copy.SetTime(original.GetTime());

                        runtimePlayables[newState].SetTime(0);
                        stateMixer.SetInputWeight(copyIndex, currentWeightOfState);
                        stateMixer.SetInputWeight(newState,  0);
                    }
                }

                for (int i = 0; i < stateMixer.GetInputCount(); i++) {
                    var currentMixVal = stateMixer.GetInputWeight(i);
                    activeWhenBlendStarted[i] = currentMixVal > 0f;
                    valueWhenBlendStarted[i]  = currentMixVal;
                }

                transitioning         = true;
                currentPlayedState    = newState;
                currentTransitionData = transitionData;
                transitionStartTime   = Time.time;
            }
        }


        public void QueuePlayCommand(int stateToPlay, QueueInstruction instruction, TransitionData? transition, string transitionName)
        {
            playInstructionQueue.Enqueue(new PlayAtTimeInstruction(instruction, stateToPlay, transition, transitionName));
        }

        public void ClearQueuedPlayCommands()
        {
            playInstructionQueue.Clear();
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

            states[currentPlayedState].JumpToRelativeTime(ref runtimePlayables[currentPlayedState], time);
        }

        public void Update()
        {
            if (states.Count == 0)
                return;

            HandleAnimationSequences();

            if (anyStatesHasAnimationEvents)
                HandleAnimationEvents();
            HandleTransitions();
            HandleQueuedInstructions();
            for (int i = 0; i < all2DControllers.Count; i++)
                all2DControllers[i].Update();
            firstFrame = false;
        }

        private void HandleAnimationSequences() {
            for (int i = 0; i < states.Count; i++) {
                if (states[i] is Sequence sequence && stateMixer.GetInputWeight(i) > 0) {
                    var playable = runtimePlayables[i];
                    sequence.ProgressThroughSequence(ref playable);
                    runtimePlayables[i] = playable;
                }
            }
        }

        private void HandleAnimationEvents()
        {
            //@TODO This is kinda broken for sequences. For sequences we want events when (or just before) the sequence changes.

            for (int i = 0; i < states.Count; i++)
            {
                //This line eats about 1.2% of all cpu time, with 580+ calls per frame
                var time = stateMixer.GetInput(i).GetTime();
                if (currentPlayedState == i || stateMixer.GetInputWeight(i) > 0f)
                {
                    states[i].HandleAnimationEvents(timeLastFrame[i], time, stateMixer.GetInputWeight(i), firstFrame, currentPlayedState == i);
                }

                timeLastFrame[i] = time;
            }
        }

        private void HandleTransitions()
        {
            if (!transitioning)
                return;

            var lerpVal = (Time.time - transitionStartTime) / currentTransitionData.duration;

            if (currentTransitionData.type == TransitionType.Clip)
            {
                if (lerpVal < 1) {
                    return;
                }

                for (int i = 0; i < stateMixer.GetInputCount(); i++)
                    stateMixer.SetInputWeight(i, 0);
                stateMixer.SetInputWeight(currentPlayedState, 1);
                runtimePlayables[currentPlayedState].SetTime(0);
                ClearFinishedTransitionStates();
                transitioning = false;
                return;
            }

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
                    valueWhenBlendStarted .RemoveAt(i);

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
            if (playInstructionQueue.Count > 0)
            {
                var instruction = playInstructionQueue.Peek();
                if (instruction.ShouldPlay()) {
                    if (instruction.transition.HasValue)
                        Play(instruction.stateToPlay, instruction.transition.Value, instruction.transitionName, false);
                    else {
                        var (transitionData, transitionName) = FindCorrectTransition(instruction.stateToPlay);
                        Play(instruction.stateToPlay, transitionData, transitionName, false);
                    }

                    playInstructionQueue.Dequeue();
                }
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
            if (stateNameToIdx.TryGetValue(stateName, out var idx))
                return idx;
            return -1;
        }

        public bool IsTransitioning()
        {
            return transitioning;
        }

        public bool IsInTransition(string transition)
        {
            if (!IsTransitioning())
                return false;

            return currentTransitionName == transition;
        }

        public static AnimationLayer CreateLayer()
        {
            var layer = new AnimationLayer
            {
                states = new List<AnimationPlayerState>(),
                transitions = new List<StateTransition>(),
                startWeight = 1f
            };
            return layer;
        }

        public void SetBlendVar(string var, float value)
        {
            blendVars[var] = value;

            if (varTo1DBlendControllers.TryGetValue(var, out var blendControllers1D))
                foreach (var controller in blendControllers1D)
                    controller.SetValue(value);

            if (varTo2DBlendControllers.TryGetValue(var, out var blendControllers2D))
                foreach (var controller in blendControllers2D)
                    controller.SetValue(var, value);
        }

        public float GetBlendVar(string var)
        {
            blendVars.TryGetValue(var, out var result);
            return result;
        }

        public void AddAllPlayingStatesTo(List<AnimationPlayerState> results)
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

        public void AddAllPlayingStatesTo(List<int> results)
        {
            results.Add(currentPlayedState);

            for (var i = 0; i < states.Count; i++)
            {
                if (i == currentPlayedState)
                    return;
                if (stateMixer.GetInputWeight(i) > 0f)
                {
                    results.Add(i);
                }
            }
        }

        public void AddAllPlayingStatesTo(List<string> results)
        {
            results.Add(states[currentPlayedState].Name);

            for (var i = 0; i < states.Count; i++)
            {
                if (i == currentPlayedState)
                    return;
                var state = states[i];
                if (stateMixer.GetInputWeight(i) > 0f)
                {
                    results.Add(state.Name);
                }
            }
        }

        public void AddTreesMatchingBlendVar(BlendVarController aggregateController, string blendVar)
        {
            if (varTo1DBlendControllers.TryGetValue(blendVar, out var blendControllers1D))
                aggregateController.AddControllers(blendControllers1D);

            if (varTo2DBlendControllers.TryGetValue(blendVar, out var blendControllers2D))
                aggregateController.AddControllers(blendControllers2D);
        }

        public AnimationPlayerState GetCurrentPlayingState()
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

        public void SwapClipOnState(int state, AnimationClip clip) {
            var animationState = states[state];
            if (!(animationState is SingleClip singleClipState)) {
                Debug.LogError($"Trying to swap the clip on the state {animationState.Name}, " +
                               $"but it is a {animationState.GetType().Name}! Only SingleClipState is supported");
                return;
            }

            singleClipState.SwapClipTo(ref runtimePlayables[state], clip);
        }

        public int AddState(AnimationPlayerState state)
        {
            if (states.Count == 0) {
                HandleAddedFirstStateAfterStartup(state);
                return 0;
            }

            states.Add(state);
            if (state.animationEvents.Count > 0)
                anyStatesHasAnimationEvents = true;
            var playable = state.Initialize(containingGraph, varTo1DBlendControllers, varTo2DBlendControllers, all2DControllers, blendVars, clipSwapCollections);

            var indexOfNew = states.Count - 1;
            stateNameToIdx[state.Name] = indexOfNew;

            Array.Resize(ref runtimePlayables, runtimePlayables.Length + 1);
            runtimePlayables[runtimePlayables.Length - 1] = playable;

            activeWhenBlendStarted.Add(false);
            valueWhenBlendStarted.Add(0f);
            timeLastFrame.Add(0d);

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

            containingGraph.Connect(playable, 0, stateMixer, indexOfNew);
            return indexOfNew;
        }

        /// <summary>
        /// If the first state gets added after Initialize and InitializeLayerBlending has run, we disconnect and destroy the empty state mixer, and then
        /// re-initialize.
        /// </summary>
        private void HandleAddedFirstStateAfterStartup(AnimationPlayerState state)
        {
            states.Add(state);

            // layerMixer.IsValid => there's more than 1 layer.
            if(layerMixer.IsValid())
                containingGraph.Disconnect(layerMixer, layerIndex);

            stateMixer.Destroy();

            InitializeSelf(containingGraph, defaultTransition, clipSwapCollections);

            if(layerMixer.IsValid())
                InitializeLayerBlending(containingGraph, layerIndex, layerMixer);
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
        private List<Sequence> serializedSequences = new List<Sequence>();
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
            if (serializedSelectRandomStates == null)
                serializedSelectRandomStates = new List<PlayRandomClip>();
            else
                serializedSelectRandomStates.Clear();

            if (serializedSequences == null)
                serializedSequences = new List<Sequence>();
            else
                serializedSequences.Clear();

            foreach (var state in states)
            {
                switch (state) {
                    case SingleClip singleClip:
                        serializedSingleClipStates.Add(singleClip);
                        continue;
                    case BlendTree1D blendTree1D:
                        serializedBlendTree1Ds.Add(blendTree1D);
                        continue;
                    case BlendTree2D blendTree2D:
                        serializedBlendTree2Ds.Add(blendTree2D);
                        continue;
                    case PlayRandomClip playRandomClip:
                        serializedSelectRandomStates.Add(playRandomClip);
                        continue;
                    case Sequence sequence:
                        serializedSequences.Add(sequence);
                        continue;
                    default:
                        if (state != null)
                            Debug.LogError($"Found state in AnimationLayer's states that's of an unknown type, " +
                                           $"({state.GetType().Name})! Did you forget to implement the serialization?");
                        continue;
                }
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
                states = new List<AnimationPlayerState>();
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

            foreach (var sequence in serializedSequences) {
                states.Add(sequence);
            }

            serializedSingleClipStates.Clear();
            serializedBlendTree1Ds.Clear();
            serializedBlendTree2Ds.Clear();
            serializedSelectRandomStates.Clear();
            serializedSequences.Clear();

            states.Sort(CompareListIndices);
        }

        private int CompareListIndices(AnimationPlayerState x, AnimationPlayerState y)
        {
            var xIndex = Array.IndexOf(serializedStateOrder, x.GUID);
            var yIndex = Array.IndexOf(serializedStateOrder, y.GUID);
            if (xIndex < yIndex)
                return -1;
            return xIndex > yIndex ? 1 : 0;
        }

        private struct PlayAtTimeInstruction
        {
            internal int stateToPlay;
            internal TransitionData? transition;
            internal string transitionName;

            internal float isDoneTime;
            internal QueueStateType type;
            private bool boolParam;
            private float timeParam;

            internal bool CountFromQueued => boolParam;
            internal float Seconds => timeParam;
            internal float RelativeDuration => timeParam;

            public PlayAtTimeInstruction(QueueInstruction instruction, int stateToPlay, TransitionData? transition, string transitionName)
            {
                type = instruction.type;
                boolParam = instruction.boolParam;
                timeParam = instruction.timeParam;

                this.stateToPlay = stateToPlay;
                this.transition = transition;
                this.transitionName = transitionName;

                if (instruction.type == QueueStateType.AfterSeconds && instruction.CountFromQueued)
                    isDoneTime = Time.time + instruction.timeParam;
                else
                    isDoneTime = -1; //gets set when moved to the top of the queue
            }

            public bool ShouldPlay() {
                return Time.time >= isDoneTime;
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
            private AnimationLayer animationLayer;

            public PlayAtTimeInstructionQueue(AnimationLayer animationLayer)
            {
                this.animationLayer = animationLayer;
            }

            public void Enqueue(PlayAtTimeInstruction instruction)
            {
                if (Count == instructions.Length)
                {
                    Array.Resize(ref instructions, instructions.Length + bufferSizeIncrement);
                }

                if (Count == 0)
                    instruction = MovedToTopOfQueue(instruction, animationLayer.currentPlayedState, animationLayer.stateMixer, animationLayer.states);

                instructions[Count] = instruction;
                Count++;
            }

            public PlayAtTimeInstruction Peek()
            {
                Debug.Assert(Count > 0, "Trying to peek play at time instructions, but there's no instructions!");
                return instructions[0];
            }

            public void Dequeue()
            {
                for (int i = 0; i < Count - 1; i++)
                {
                    instructions[i] = instructions[i + 1];
                }

                Count--;

                if (Count > 0)
                {
                    instructions[Count - 1] = MovedToTopOfQueue(instructions[Count - 1], animationLayer.currentPlayedState, animationLayer.stateMixer,
                                                                animationLayer.states);
                }
            }

            public void Clear()
            {
                Count = 0;
            }

            private PlayAtTimeInstruction MovedToTopOfQueue(PlayAtTimeInstruction playAtTime, int currentState, AnimationMixerPlayable stateMixer,
                                                            List<AnimationPlayerState> states) {
                switch (playAtTime.type) {
                    case QueueStateType.WhenCurrentDone: {
                        var (_, currentStateIsDoneAt) = GetCurrentStateTimeInfo();

                        playAtTime.isDoneTime = currentStateIsDoneAt;
                        break;
                    }
                    case QueueStateType.AfterSeconds:
                        if (!playAtTime.CountFromQueued)
                        {
                            playAtTime.isDoneTime = Time.time + playAtTime.Seconds;
                        }
                        break;
                    case QueueStateType.BeforeCurrentDone_Seconds: {
                        var (_, currentStateIsDoneAt) = GetCurrentStateTimeInfo();

                        playAtTime.isDoneTime = currentStateIsDoneAt - playAtTime.Seconds;
                        break;
                    }
                    case QueueStateType.BeforeCurrentDone_Relative: {
                        var (currentStateDuration, currentStateIsDoneAt) = GetCurrentStateTimeInfo();
                        playAtTime.isDoneTime = currentStateIsDoneAt - playAtTime.RelativeDuration * currentStateDuration;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return playAtTime;

                (float currentStateDuration, float currentStateDoneAt) GetCurrentStateTimeInfo() {
                    var duration          = states[currentState].Duration;
                    var playedTime        = (float) stateMixer.GetInput(currentState).GetTime();
                    var currentIsDoneTime = Time.time + (duration - playedTime);
                    return (duration, currentIsDoneTime);
                }
            }
        }

        public double GetHowLongStateHasBeenPlaying(int stateIndex)
        {
            if (stateMixer.GetInputWeight(stateIndex) <= 0f)
                return 0f;

            return stateMixer.GetInput(stateIndex).GetTime();
        }

        public double GetNormalizedStateProgress(int stateIndex)
        {
            if (stateMixer.GetInputWeight(stateIndex) <= 0f)
                return 0f;

            return stateMixer.GetInput(stateIndex).GetTime() / states[stateIndex].Duration;
        }

        public (TransitionData transition, string name) GetDefaultTransitionFromTo(int from, int to)
        {
            var transitionIndex = transitionLookup[from, to];
            if (transitionIndex == -1)
                return (defaultTransition, "Default");

            var transition = transitions[transitionIndex];
            return (transition.transitionData, transition.name);
        }

        public void OnClipSwapsChanged()
        {
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                state.OnClipSwapsChanged(ref runtimePlayables[i]);
            }
        }

        public void AddAllClipsInStatesAndTransitionsTo(List<AnimationClip> list)
        {
            foreach (var state in states)
                AddAllClipsFromStateTo(state, list);
            foreach (var transition in transitions) {
                if (transition.transitionData.clip != null)
                    list.Add(transition.transitionData.clip);
            }
        }

        private void AddAllClipsFromStateTo(AnimationPlayerState state, List<AnimationClip> list)
        {
            switch (state)
            {
                case BlendTree1D blendTree1D:
                {
                    foreach (var clip in blendTree1D.blendTree.Select(entry => entry.clip))
                        AddClip(clip);
                    break;
                }
                case BlendTree2D blendTree2D:
                {
                    foreach (var clip in blendTree2D.blendTree.Select(entry => entry.clip))
                        AddClip(clip);
                    break;
                }
                case PlayRandomClip playRandomClip:
                {
                    foreach (var clip in playRandomClip.clips)
                        AddClip(clip);
                    break;
                }
                case Sequence sequence:
                {
                    foreach (var clip in sequence.clips)
                        AddClip(clip);
                    break;
                }
                case SingleClip singleClip:
                {
                    AddClip(singleClip.clip);
                    break;
                }
            }

            void AddClip(AnimationClip clip)
            {
                if (clip != null && !list.Contains(clip))
                    list.Add(clip);
            }
        }
    }
}