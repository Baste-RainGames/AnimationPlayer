using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Debug = UnityEngine.Debug;

namespace Animation_Player
{
    public class AnimationPlayer : MonoBehaviour
    {
        // Serialized data:
        private const int lastVersion = 1;
        [SerializeField, HideInInspector]
        private int versionNumber;

        public AnimationLayer[] layers;
        public TransitionData defaultTransition;

        //Runtime fields:
        private PlayableGraph graph;
        private Playable rootPlayable;
        private string visualizerClientName;

        //IK
        public Animator OutputAnimator { get; private set; }
        private float currentIKLookAtWeight;
        private Vector3 currentIKLookAtPosition;
        //@TODO: It would be nice to figure out if IK is available at runtime, but currently that's not possible!
        // This is because we currently do IK through having an AnimatorController with an IK layer on it on the animator, which works, 
        // but it's not possible to check if IK is turned on on an AnimatorController at runtime:  
        // https://forum.unity.com/threads/check-if-ik-pass-is-enabled-at-runtime.505892/#post-3299087
        // There are two good solutions:
        // 1: Wait until IK Playables are implemented, at some point
        // 2: Ship AnimationPlayer with an AnimatorController that's set up correctly, and which we set as the runtime animator
        // controller on startup
        //        public bool IKAvailable { get; private set; }

        private bool hasAwoken;

        private void Awake()
        {
            EnsureVersionUpgraded();
            hasAwoken = true;

            if (layers.Length == 0)
                return;
            bool anyLayersWithStates = false;
            for (int i = 0; i < layers.Length; i++) {
                if (layers[i].states.Count > 0) {
                    anyLayersWithStates = true;
                    break;
                }
            }

            if (!anyLayersWithStates)
                return;

            //The playable graph is a directed graph of Playables.
            graph = PlayableGraph.Create();

            // The AnimationPlayableOutput links the graph with an animator that plays the graph.
            // I think we can ditch the animator, but the documentation is kinda sparse!
            OutputAnimator = gameObject.EnsureComponent<Animator>();
            AnimationPlayableOutput animOutput = AnimationPlayableOutput.Create(graph, $"{name}_animation_player", OutputAnimator);

            for (var i = 0; i < layers.Length; i++)
                layers[i].InitializeSelf(graph);

            if (layers.Length <= 1)
            {
                rootPlayable = layers[0].stateMixer;
            }
            else
            {
                var layerMixer = AnimationLayerMixerPlayable.Create(graph, layers.Length);

                for (var i = 0; i < layers.Length; i++)
                    layers[i].InitializeLayerBlending(graph, i, layerMixer);

                rootPlayable = layerMixer;
            }

            animOutput.SetSourcePlayable(rootPlayable);

            //fun fact: default is DSPClock!
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            graph.Play();

            visualizerClientName = name + " AnimationPlayer";
            GraphVisualizerClient.Show(graph, visualizerClientName);
        }

        private void Update()
        {
            foreach (var layer in layers)
                layer.Update();
        }

        private void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        /// <summary>
        /// Play a state, using an instant transition. The state will immediately be the current played state. 
        /// </summary>
        /// <param name="state">Name of the state to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void SnapTo(string state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "Snap to state");
            Play(state, TransitionData.Instant(), layer);
        }

        /// <summary>
        /// Play a state, using an instant transition. The state will immediately be the current played state. 
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void SnapTo(int state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "Snap to state");
            Play(state, TransitionData.Instant(), layer);
        }

        /// <summary>
        /// Play a state, using the defined transition between the current state and that state if it exists,
        /// or the player's default transition if it doesn't.
        /// The state will immediately be the current played state. 
        /// </summary>
        /// <param name="state">Name of the state to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void Play(string state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "play a state");
            int stateIdx = GetStateIdxFromName(state, layer);
            if (stateIdx == -1)
                return;
            Play(stateIdx, layer);
        }

        /// <summary>
        /// Plays the default state of the state machine
        /// </summary>
        /// <param name="layer">Layer to play the default state on</param>
        public void PlayDefaultState(int layer = 0)
        {
            AssertLayerInBounds(layer, "play the default state");
            Play(0, layer);
        }

        /// <summary>
        /// Return to playing the default state if the named state is the current played state.
        /// This is usefull if you want to play an animation, and then return to idle, but don't want
        /// to intervene if something else has changed the currently played state. 
        /// </summary>
        /// <param name="state">State to check if is playing</param>
        /// <param name="layer">Layer this is happening on</param>
        public void PlayDefaultStateIfPlaying(string state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "return to default state");
            if (layers[layer].GetCurrentPlayingState().Name == state)
                Play(0, layer);
        }

        /// <summary>
        /// Play a state, using the defined transition between the current state and that state if it exists,
        /// or the player's default transition if it doesn't.
        /// The state will immediately be the current played state.
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void Play(int state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "play a state");
            AssertStateInBounds(layer, state, "play a state");
            layers[layer].PlayUsingInternalTransition(state, defaultTransition);
        }

        /// <summary>
        /// Play a state. The state will immediately be the current played state. 
        /// </summary>
        /// <param name="state">Name of the state to play</param>
        /// <param name="transitionData">How to transition into the state</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void Play(string state, TransitionData transitionData, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "play a state");
            int stateIdx = GetStateIdxFromName(state, layer);
            Play(stateIdx, transitionData, layer);
        }

        /// <summary>
        /// Play a state. The state will immediately be the current played state. 
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="transitionData">How to transition into the state</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void Play(int state, TransitionData transitionData, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "play a state");
            AssertStateInBounds(layer, state, "play a state");
            AssertTransitionDataFine(transitionData);
            layers[layer].PlayUsingExternalTransition(state, transitionData);
        }

        /// <summary>
        /// Plays a state after a certain amount of seconds. If any overload of
        /// Play or SnapTo are called before that many seconds has passed, this
        /// instruction gets discarded. Several PlayAfterSeconds in a row will
        /// work as intended:
        /// 
        /// Example:
        /// Play("State1");
        /// PlayAfterSeconds(1, "State2");
        /// Play("State3"); //Cancels the instruction to play State 2 after 1 second
        /// //After 1 seconds has passed, State 3 will still be playing
        ///
        /// Play("State1");
        /// PlayAfterSeconds(1, "State2");
        /// SnapTo("State3") //Cancels the instruction to play State 2 after 1 second
        /// //After 1 seconds has passed, State 3 will still be playing
        /// 
        /// Play("State1");
        /// PlayAfterSeconds(1, "State2");
        /// PlayAfterSeconds(2, "State3");
        /// //After 1 seconds has passed, State 2 will start playing
        /// //After 2 seconds has passed, State 3 will start playing
        /// 
        /// </summary>
        /// <param name="seconds">How long to wait before playing the state.</param>
        /// <param name="state">State to play.</param>
        /// <param name="layer">Layer to play the state on.</param>
        public void PlayAfterSeconds(float seconds, string state, int layer = 0)
        {
            AssertLayerInBounds(layer, "Playing animation after seconds");
            int stateIdx = GetStateIdxFromName(state, layer);
            PlayAfterSeconds(seconds, stateIdx, layer);
        }

        /// <summary>
        /// Plays a state after a certain amount of seconds. See overload with string state for details.
        /// <param name="seconds">How long to wait before playing the state.</param>
        /// <param name="state">Index of the state to play.</param>
        /// <param name="layer">Layer to play the state on.</param>
        /// </summary>
        public void PlayAfterSeconds(float seconds, int state, int layer = 0)
        {
            AssertLayerInBounds(layer, "Playing a state after seconds");
            AssertStateInBounds(layer, state, "Playing a state after seconds");
            layers[layer].PlayAfterSeconds(seconds, state, defaultTransition);
        }

        /// <summary>
        /// Jumps the current played state to a certain time between 0 and 1.
        /// Input time will be smartly modulated; 1.3 will be interpreted as 0.3, and -0.2 will be interpreted as 0.8
        /// Animation events will be skipped, and any ongoing blends will be cleared. @TODO: maybe have a parameter for these?
        /// </summary>
        /// <param name="time">The time to set.</param>
        /// <param name="layer">Layer to set the time of a state on.</param>
        public void JumpToRelativeTime(float time, int layer = 0) {
            AssertLayerInBounds(layer, "Jumping to a relative time");
            layers[layer].JumpToRelativeTime(time);
        }

        /// <summary>
        /// Checks if the animation player has a state with the specified name.
        /// </summary>
        /// <param name="stateName">Name to check on</param>
        /// <param name="layer">Layer to check (default 0)</param>
        /// <returns>true if there is a state with the name on the layer</returns>
        public bool HasState(string stateName, int layer = 0)
        {
            AssertLayerInBounds(layer, "Check if state exists");
            return layers[layer].HasState(stateName);
        }

        /// <summary>
        /// Finds the weight of a state in the layer's blend, eg. how much the state is playing.
        /// This is a number between 0 and 1, with 0 for "not playing" and 1 for "playing completely"
        /// These do not neccessarilly sum to 1.
        /// </summary>
        /// <param name="state">State to check for</param>
        /// <param name="layer">Layer to check in</param>
        /// <returns>The weight for state in layer</returns>
        public float GetStateWeight(string state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "get a state weight");
            int stateIdx = GetStateIdxFromName(state, layer);
            return layers[layer].GetStateWeight(stateIdx);
        }

        /// <summary>
        /// Finds the weight of a state in the layer's blend, eg. how much the state is playing.
        /// This is a number between 0 and 1, with 0 for "not playing" and 1 for "playing completely"
        /// These do not neccessarilly sum to 1.
        /// </summary>
        /// <param name="state">State to check for</param>
        /// <param name="layer">Layer to check in</param>
        /// <returns>The weight for state in layer</returns>
        public float GetStateWeight(int state, int layer = 0)
        {
            AssertLayerInBounds(layer, state, "get a state weight");
            return layers[layer].GetStateWeight(state);
        }

        public float GetLayerWeight(int layer) {
            AssertLayerInBounds(layer, "Getting weight of a layer");
            if (layers.Length < 2)
                return 1;
            return rootPlayable.GetInputWeight(layer);
        }

        public void SetLayerWeigth(int layer, float weight) {
            AssertLayerInBounds(layer, "Getting weight of a layer");
            rootPlayable.SetInputWeight(layer, weight);
        }

        /// <summary>
        /// Get a state by name.
        /// </summary>
        /// <param name="state">State to get</param>
        /// <param name="layer">Layer to Look in</param>
        /// <returns></returns>
        public AnimationState GetState(string state, int layer = 0) 
        {
            AssertLayerInBounds(layer, "getting a state");
            int stateIdx = GetStateIdxFromName(state, layer);
            AssertStateInBounds(layer, stateIdx, "getting a state");
            return layers[layer].states[stateIdx];
        }

        /// <summary>
        /// Get a state by index.
        /// </summary>
        /// <param name="state">Index of the state</param>
        /// <param name="layer">Layer to look in.</param>
        /// <returns></returns>
        public AnimationState GetState(int state, int layer = 0)
        {
            AssertLayerInBounds(layer, "getting a state");
            AssertStateInBounds(layer, state, "getting a state");
            return layers[layer].states[state];
        }

        /// <summary>
        /// Gets the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
        /// </summary>
        /// <param name="layer">Layer to check in</param>
        public AnimationState GetPlayingState(int layer = 0)
        {
            AssertLayerInBounds(layer, "get the current playing state");
            return layers[layer].GetCurrentPlayingState();
        }

        public int GetIndexOfPlayingState(int layer = 0) 
        {
            AssertLayerInBounds(layer, "get the index of the current playing state");
            return layers[layer].GetIndexOfPlayingState();
        }

        /// <summary>
        /// Checks if the AnimationPlayer is playing a named state.
        /// </summary>
        /// <param name="state">State to check if is being player</param>
        /// <param name="layer">Layer to check on</param>
        /// <returns></returns>
        public bool IsPlaying(string state, int layer = 0) 
        {
            AssertLayerInBounds(layer, "Checking if state is playing");
            return GetPlayingState(layer).Name == state;
        }

        /// <summary>
        /// Checks if the AnimationPlayer is playing a state.
        /// </summary>
        /// <param name="state">State to check if is being player</param>
        /// <param name="layer">Layer to check on</param>
        /// <returns></returns>
        public bool IsPlaying(int state, int layer = 0) 
        {
            AssertLayerInBounds(layer, "Checking if state is playing");
            return GetIndexOfPlayingState(layer) == state;
        }

        /// <summary>
        /// Retrives all of the currently playing states. The first element in the list will be the currently played state. All
        /// other results will be states that are not finished blending out
        /// </summary>
        /// <param name="results">result container</param>
        /// <param name="layer">Layer to chedck in</param>
        public void GetAllPlayingStates(List<AnimationState> results, int layer = 0)
        {
            AssertLayerInBounds(layer, "get all playing states");
            results.Clear();
            layers[layer].AddAllPlayingStatesTo(results);
        }

        public void GetAllStates(List<AnimationState> result, int layer = 0) 
        {
            AssertLayerInBounds(layer, "get all states");
            result.Clear();
            for (int i = 0; i < layers[layer].states.Count; i++) {
                result.Add(layers[layer].states[i]);
            }
        }

        /// <summary>
        /// Gets how many states there are in a layer
        /// </summary>
        /// <param name="layer">Layer to check in (default 0)</param>
        /// <returns>The number of states in the layer</returns>
        public int GetStateCount(int layer = 0)
        {
            AssertLayerInBounds(layer, "get the state count");
            return layers[layer].states.Count;
        }

        /// <summary>
        /// Sets a blend var to a value. This will affect every state that blend variable controls.
        /// If you're setting a blend variable a lot - like setting "Speed" every frame based on a rigidbody's velocity, consider getting a BlendVarController
        /// instead, as that's faster. 
        /// </summary>
        /// <param name="var">The blend variable to set.</param>
        /// <param name="value">The value to set it to.</param>
        /// <param name="layer">The layer to set the blend var on @TODO: Stop having blend variables be layer-based, that's crazy, I think</param>
        public void SetBlendVar(string var, float value, int layer = 0)
        {
            AssertLayerInBounds(layer, "Set blend var");
            layers[layer].SetBlendVar(var, value);
        }

        public float GetBlendVar(string var, int layer = 0)
        {
            AssertLayerInBounds(layer, "Get blend var");
            return layers[layer].GetBlendVar(var);
        }

        public List<string> GetBlendVariables()
        {
            List<string> result = new List<string>();
            GetBlendVariables(result);
            return result;
        }

        private void GetBlendVariables(List<string> result)
        {
            result.Clear();
            foreach (var layer in layers)
            {
                layer.AddAllBlendVarsTo(result);
            }
        }

        /// <summary>
        /// Gets the index of a state from it's name.
        /// This method is used internally whenever you send in a string Play("Idle"), so it's recommended to cache the result
        /// of this method instead of sending in strings.
        /// </summary>
        public int GetStateIdxFromName(string state, int layer = 0)
        {
            int stateIdx = layers[layer].GetStateIdx(state);
            if (stateIdx == -1)
            {
                if (!hasAwoken)
                {
                    Debug.LogError("Trying to get index of a state before AnimationPlayer has had the time to Awake yet! Please don't poll states before Start!");
                    return -1;
                }
                
                Debug.LogError($"Trying to get index of state \"{state}\" on layer {layer}, but that doesn't exist! States that exist are:" +
                               $"\n{layers[layer].states.PrettyPrint(s => s.Name)}", gameObject);
                return -1;
            }

            return stateIdx;
        }

        /// <summary>
        /// Register a listener for an animation event
        /// </summary>
        /// <param name="targetEvent">Event to listen to</param>
        /// <param name="listener">Action to be called when targetEvent happens</param>
        public void RegisterAnimationEventListener(string targetEvent, Action listener)
        {
            foreach (var layer in layers)
                foreach (var state in layer.states)
                    foreach (var animationEvent in state.animationEvents)
                        if(animationEvent.name == targetEvent)
                            animationEvent.RegisterListener(listener);
        }

        /// <summary>
        /// Swaps the currently playing clip on a state.
        /// Haven't quite figured out how this should work yet. Probably will get more options
        /// </summary>
        /// <param name="state">Index of the state to swap the clip on. This state must be a SingleClipState (for now)</param>
        /// <param name="clip">The new clip to use on the state</param>
        /// <param name="layer">Layer of the state</param>
        public void SwapClipOnState(int state, AnimationClip clip, int layer = 0) 
        {
            AssertLayerInBounds(layer, state, "Swapping the clip on a state");
            AssertStateInBounds(layer, state, "Swapping the clip on a state");
            if (!Application.isPlaying) 
            {
                Debug.LogError("In edit mode, just set the clip on a state directly.");
                return;
            }

            layers[layer].SwapClipOnState(state, clip, graph);
        }

        /// <summary>
        /// Equivalent to Animator.SetIKHintPosition
        /// Sets the position of an IK hint.
        /// </summary>
        /// <param name="hint">The AvatarIKHint that is set.</param>
        /// <param name="hintPosition">The position in world space.</param>
        public void SetIKHintPosition(AvatarIKHint hint, Vector3 hintPosition)
            => OutputAnimator.SetIKHintPosition(hint, hintPosition);

        /// <summary>
        /// Equivalent to Animator.SetIKHintPositionWeight
        /// Sets the translative weight of an IK hint (0 = at the original animation before IK, 1 = at the hint).
        /// </summary>
        /// <param name="hint">The AvatarIKHint that is set.</param>
        /// <param name="weight">The translative weight.</param>
        public void SetIKHintPositionWeight(AvatarIKHint hint, float weight)
            => OutputAnimator.SetIKHintPositionWeight(hint, weight);

        /// <summary>
        /// Equivalent to Animator.SetIKPosition
        /// Sets the position of an IK goal.
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="goalPosition">The position in world space.</param>
        public void SetIKPosition(AvatarIKGoal goal, Vector3 goalPosition)
            => OutputAnimator.SetIKPosition(goal, goalPosition);

        /// <summary>
        /// Equivalent to Animator.SetIKPositionWeight
        /// Sets the translative weight of an IK goal (0 = at the original animation before IK, 1 = at the goal).
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="weight">The translative weight.</param>
        public void SetIKPositionWeight(AvatarIKGoal goal, float weight)
            => OutputAnimator.SetIKPositionWeight(goal, weight);

        /// <summary>
        /// Equivalent to Animator.SetIKRotation
        /// Sets the rotation of an IK goal.
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="goalRotation">The rotation in world space.</param>
        public void SetIKRotation(AvatarIKGoal goal, Quaternion goalRotation)
            => OutputAnimator.SetIKRotation(goal, goalRotation);

        /// <summary>
        /// Equivalent to Animator.SetIKRotationWeight
        /// Sets the rotational weight of an IK goal (0 = rotation before IK, 1 = rotation at the IK goal).
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="weight">The rotational weight.</param>
        public void SetIKRotationWeight(AvatarIKGoal goal, float weight)
            => OutputAnimator.SetIKRotationWeight(goal, weight);

        /// <summary>
        /// Equivalent to Animator.SetLookAtPosition
        /// Sets the look at position.
        /// </summary>
        /// <param name="position">The position to lookAt.</param>
        public void SetIKLookAtPosition(Vector3 position) {
            currentIKLookAtPosition = position;
            OutputAnimator.SetLookAtPosition(position);
        }

        /// <summary>
        /// Equivalent to Animator.SetIKLookAtWeight
        /// Set look at weights.
        /// </summary>
        /// <param name="weight">(0-1) the global weight of the LookAt, multiplier for other parameters.</param>
        /// <param name="bodyWeight">(0-1) determines how much the body is involved in the LookAt.</param>
        /// <param name="headWeight">(0-1) determines how much the head is involved in the LookAt.</param>
        /// <param name="eyesWeight">(0-1) determines how much the eyes are involved in the LookAt.</param>
        /// <param name="clampWeight">(0-1) 0.0 means the character is completely unrestrained in motion, 1.0 means it's completely clamped (look at becomes 
        /// impossible), and 0.5 means it'll be able to move on half of the possible range (180 degrees).</param>
        public void SetIKLookAtWeight(float weight, float bodyWeight = 0f, float headWeight = 1f, float eyesWeight = 0f, float clampWeight = .5f)
        {
            currentIKLookAtWeight = weight; //animator has a getter for all the other IK things, but not this one!
            OutputAnimator.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight);
        }

        /// <returns>The current IK Look at weight (0 - 1)</returns>
        public float GetIKLookAtWeight() => currentIKLookAtWeight;

        /// <returns>The current IK Look at position</returns>
        public Vector3 GetIKLookAtPosition() => currentIKLookAtPosition;

        /// <summary>
        /// Checks all layers for a blend tree that uses the named blendVar.
        /// </summary>
        /// <param name="blendVar">blendVar to check for.</param>
        /// <returns>true if the blendvar is used in any blend tree in any layer.</returns>
        public bool HasBlendVarInAnyLayer(string blendVar) {
            foreach (var layer in layers) {
                if (layer.HasBlendTreeUsingBlendVar(blendVar))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a new state to the AnimationPlayer, and makes sure that all the graphs are correctly setup.
        /// At edit time, just add new states directly into the layer's states
        /// </summary>
        /// <param name="state">State to add.</param>
        /// <param name="layer">Layer to add the state to.</param>
        /// <returns>The index of the added state</returns>
        public int AddState(AnimationState state, int layer = 0)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Don't call AnimationPlayer.AddState at runtime! Just add states to the layers directly!");
                return -1;
            }
            AssertLayerInBounds(layer, "Adding an animation state");
            return layers[layer].AddState(state);
        }

        /// <summary>
        /// Gets a blend variable controller for a specific variable, allowing you to edit that variable
        /// much faster than by SetBlendVar(name, value).
        /// </summary>
        /// <param name="blendVar">blendVar you want to controll</param>
        public BlendVarController GetBlendControllerFor(string blendVar)
        {
            BlendVarController controller = new BlendVarController(blendVar);
            foreach (var animationLayer in layers)
            {
                animationLayer.AddTreesMatchingBlendVar(controller, blendVar);
            }

            if (controller.InnerControllerCount == 0)
            {
                if (!hasAwoken)
                {
                    Debug.LogError("Trying to create a blend controller in an AnimationPlayer before it has called Awake!. Please either move your calls " +
                                   "to Start or later, or use script execution order to make sure you're called after AnimationPlayer!");
                }
                else
                {
                    Debug.LogWarning($"Warning! Creating a blend controller for {blendVar} on AnimationPlayer on {name}, " +
                                     $"but there's no blend trees that cares about that variable!", gameObject);
                }
            }

            return controller;
        }

        public bool EnsureVersionUpgraded()
        {
            if (versionNumber == lastVersion)
                return false;

            if (versionNumber < 1 && layers != null)
            {
                foreach (var layer in layers)
                {
                    foreach (var state in layer.states)
                    {
                        state.EnsureHasGUID();
                    }
                }
            }

            versionNumber = lastVersion;
            return true;
        }

        [Conditional("UNITY_ASSERTIONS")]
        private void AssertLayerInBounds(int layer, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (layer {layer}, there are {layers.Length} layers! " +
                               $"GameObject {gameObject.name})", gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        private void AssertLayerInBounds(int layer, int state, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers! " +
                               $"GameObject {gameObject.name})", gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        private void AssertLayerInBounds(int layer, string state, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers! " +
                               $"GameObject {gameObject.name})", gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        private void AssertTransitionDataFine(TransitionData transitionData)
        {
            if (transitionData.type == TransitionType.Curve && transitionData.curve != null)
                Debug.LogError("Trying to transition using a curve, but the curve is null!");
        }

        [Conditional("UNITY_ASSERTIONS")]
        private void AssertStateInBounds(int layer, int state, string action)
        {
            if (!(state >= 0 && state < layers[layer].states.Count))
                Debug.LogError(
                    $"Trying to {action} on an out of bounds state! (state {state} on layer {layer}, but there are {layers[layer].states.Count} " +
                    $"states on that layer! GameObject {gameObject.name})", gameObject);
        }
    }
}