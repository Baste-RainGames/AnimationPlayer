using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Animator outputAnimator;
        private float currentIKLookAtWeight;
        //@TODO: It would be nice to figure out if IK is available at runtime, but currently that's not possible!
        // This is because we currently do IK through having an AnimatorController with an IK layer on it on the animator, which works, 
        // but it's not possible to check if IK is turned on on an AnimatorController at runtime:  
        // https://forum.unity.com/threads/check-if-ik-pass-is-enabled-at-runtime.505892/#post-3299087
        // There are two good solutions:
        // 1: Wait until IK Playables are implemented, at some point
        // 2: Ship AnimationPlayer with an AnimatorController that's set up correctly, and which we set as the runtime animator
        // controller on startup
        //        public bool IKAvailable { get; private set; }

#if UNITY_EDITOR
        //Used to make the inspector continually update
        public Action editTimeUpdateCallback;
#endif

        private bool hasAwoken;

        private void Awake()
        {
            EnsureVersionUpgraded();
            hasAwoken = true;

            if (layers.Length == 0)
                return;

            //The playable graph is a directed graph of Playables.
            graph = PlayableGraph.Create();

            // The AnimationPlayableOutput links the graph with an animator that plays the graph.
            // I think we can ditch the animator, but the documentation is kinda sparse!
            outputAnimator = gameObject.EnsureComponent<Animator>();
            AnimationPlayableOutput animOutput = AnimationPlayableOutput.Create(graph, $"{name}_animation_player", outputAnimator);

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
#if UNITY_EDITOR
            editTimeUpdateCallback?.Invoke();
#endif
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
        public AnimationState GetCurrentPlayingState(int layer = 0)
        {
            AssertLayerInBounds(layer, "get the current playing state");
            return layers[layer].GetCurrentPlayingState();
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
            return GetCurrentPlayingState(layer).Name == state;
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

        public int GetStateCount(int layer = 0)
        {
            AssertLayerInBounds(layer, "get the state count");
            return layers[layer].states.Count;
        }

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
                Debug.LogError($"Trying to get the state \"{state}\" on layer {layer}, but that doesn't exist! States that exist are:" +
                               $"\n{layers[layer].states.PrettyPrint(s => s.Name)}", gameObject);
                return -1;
            }

            return stateIdx;
        }

        /// <summary>
        /// Equivalent to Animator.SetIKHintPosition
        /// Sets the position of an IK hint.
        /// </summary>
        /// <param name="hint">The AvatarIKHint that is set.</param>
        /// <param name="hintPosition">The position in world space.</param>
        public void SetIKHintPosition(AvatarIKHint hint, Vector3 hintPosition)
            => outputAnimator.SetIKHintPosition(hint, hintPosition);

        /// <summary>
        /// Equivalent to Animator.SetIKHintPositionWeight
        /// Sets the translative weight of an IK hint (0 = at the original animation before IK, 1 = at the hint).
        /// </summary>
        /// <param name="hint">The AvatarIKHint that is set.</param>
        /// <param name="weight">The translative weight.</param>
        public void SetIKHintPositionWeight(AvatarIKHint hint, float weight)
            => outputAnimator.SetIKHintPositionWeight(hint, weight);

        /// <summary>
        /// Equivalent to Animator.SetIKPosition
        /// Sets the position of an IK goal.
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="goalPosition">The position in world space.</param>
        public void SetIKPosition(AvatarIKGoal goal, Vector3 goalPosition)
            => outputAnimator.SetIKPosition(goal, goalPosition);

        /// <summary>
        /// Equivalent to Animator.SetIKPositionWeight
        /// Sets the translative weight of an IK goal (0 = at the original animation before IK, 1 = at the goal).
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="weight">The translative weight.</param>
        public void SetIKPositionWeight(AvatarIKGoal goal, float weight)
            => outputAnimator.SetIKPositionWeight(goal, weight);

        /// <summary>
        /// Equivalent to Animator.SetIKRotation
        /// Sets the rotation of an IK goal.
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="goalRotation">The rotation in world space.</param>
        public void SetIKRotation(AvatarIKGoal goal, Quaternion goalRotation)
            => outputAnimator.SetIKRotation(goal, goalRotation);

        /// <summary>
        /// Equivalent to Animator.SetIKRotationWeight
        /// Sets the rotational weight of an IK goal (0 = rotation before IK, 1 = rotation at the IK goal).
        /// </summary>
        /// <param name="goal">The AvatarIKGoal that is set.</param>
        /// <param name="weight">The rotational weight.</param>
        public void SetIKRotationWeight(AvatarIKGoal goal, float weight)
            => outputAnimator.SetIKRotationWeight(goal, weight);

        /// <summary>
        /// Equivalent to Animator.SetLookAtPosition
        /// Sets the look at position.
        /// </summary>
        /// <param name="position">The position to lookAt.</param>
        public void SetIKLookAtPosition(Vector3 position)
            => outputAnimator.SetLookAtPosition(position);

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
            outputAnimator.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight);
        }

        public float GetIKLookAtWeight() => currentIKLookAtWeight;

        public bool HasBlendVarInAnyLayer(string blendVar) {
            foreach (var layer in layers) {
                if (layer.HasBlendTreeUsingBlendVar(blendVar))
                    return true;
            }
            return false;
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
        public void AssertLayerInBounds(int layer, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (layer {layer}, there are {layers.Length} layers!)", gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public void AssertLayerInBounds(int layer, int state, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers!)",
                               gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public void AssertLayerInBounds(int layer, string state, string action)
        {
            if (!(layer >= 0 && layer < layers.Length))
                Debug.LogError($"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers!)",
                               gameObject);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public void AssertTransitionDataFine(TransitionData transitionData)
        {
            if (transitionData.type == TransitionType.Curve && transitionData.curve != null)
                Debug.LogError("Trying to transition using a curve, but the curve is null!");
        }

        [Conditional("UNITY_ASSERTIONS")]
        public void AssertStateInBounds(int layer, int state, string action)
        {
            if (!(state >= 0 && state < layers[layer].states.Count))
                Debug.LogError(
                    $"Trying to {action} on an out of bounds state! (state {state} on layer {layer}, but there are {layers[layer].states.Count} " +
                    $"states on that layer!)", gameObject);
        }
    }
}