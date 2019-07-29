using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

using Debug = UnityEngine.Debug;
using UObject = UnityEngine.Object;

namespace Animation_Player
{
    public class AnimationPlayer : MonoBehaviour, IAnimationClipSource
    {
        // Serialized data:
        private const int lastVersion = 1;
        [SerializeField, HideInInspector]
        private int versionNumber;

        public static StateID DefaultState => 0;

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
            if(hasAwoken)
                return;
            hasAwoken = true;
            EnsureVersionUpgraded();

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
                layers[i].InitializeSelf(graph, defaultTransition);

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
            //GraphVisualizerClient.Show(graph, visualizerClientName);
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
        /// Ensures that the AnimationPlayer is ready - ie. has had Awake called. Use this if you're calling something before you can be sure that the
        /// AnimationPlayer's gotten around to calling Awake yet, like if you're calling into AnimationPlayer from Awake.
        /// </summary>
        public void EnsureReady()
        {
            if(!hasAwoken)
                Awake();
        }

        /// <summary>
        /// Play a state, using the defined transition between the current state and that state if it exists,
        /// or the player's default transition if it doesn't.
        /// The state will immediately be the current played state.
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public AnimationState Play(StateID state, LayerID layer = default)
        {
            return Play(state, layer, "Play state");
        }

        private AnimationState Play(StateID state, LayerID layer, string actionIDForErrors)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, actionIDForErrors);
            if (!foundIndices)
                return null;
            return layers[layerIndex].Play(stateIndex, TransitionData.UseDefined());
        }

        /// <summary>
        /// Play a state, using a custom transition. The state will immediately be the current played state.
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="transitionData">How to transition into the state</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void Play(StateID state, TransitionData transitionData, LayerID layer = default)
        {
            Play(state, transitionData, layer, "Play state with transition");
        }

        private void Play(StateID state, TransitionData transitionData, LayerID layer, string actionIDForErrors)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, actionIDForErrors);
            if (!foundIndices)
                return;

            if (transitionData.type == TransitionType.Curve && transitionData.curve != null) {
                Debug.LogError(
                    $"Trying to transition using a curve, but the curve is null! " +
                    $"Error happened for AnimationPlayer on GameObject {gameObject.name})", gameObject
                );
                return;
            }

            layers[layerIndex].Play(stateIndex, transitionData);
        }

        /// <summary>
        /// Play a state, using an instant transition. The state will immediately be the current played state.
        /// </summary>
        /// <param name="state">state index to play</param>
        /// <param name="layer">Layer the state should be played on</param>
        public void SnapTo(StateID state, LayerID layer = default)
        {
            Play(state, TransitionData.Instant(), layer, "Snap to state");
        }

        /// <summary>
        /// Plays the default state of the state machine
        /// </summary>
        /// <param name="layer">Layer to play the default state on</param>
        public void PlayDefaultState(LayerID layer = default)
        {
            Play(0, layer, "Play default state");
        }

        public bool IsPlaying(StateID state, LayerID layer = default)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Check if a state is playing");
            if (!foundIndices)
                return false;
            return layers[layerIndex].GetIndexOfPlayingState() == stateIndex;
        }

        public void QueueStateChange(StateID state) => QueueStateChange(state, default, default, default);
        public void QueueStateChange(StateID state, QueueInstruction instruction) => QueueStateChange(state, instruction, default, default);
        public void QueueStateChange(StateID state, TransitionData transition) => QueueStateChange(state, default, transition, default);
        public void QueueStateChange(StateID state, LayerID layer) => QueueStateChange(state, default, default, layer);
        public void QueueStateChange(StateID state, QueueInstruction instruction, TransitionData transition) => QueueStateChange(state, instruction, transition, default);
        public void QueueStateChange(StateID state, QueueInstruction instruction, LayerID layer) => QueueStateChange(state, instruction, default, layer);
        public void QueueStateChange(StateID state, TransitionData transition, LayerID layer) => QueueStateChange(state, default, transition, layer);

        public void QueueStateChange(StateID state, QueueInstruction instruction, TransitionData transition, LayerID layer)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, $"Play a state when the next state is done");
            if (!foundIndices)
                return;

            layers[layerIndex].QueuePlayCommand(stateIndex, instruction, transition);
        }

        public void ClearQueuedPlayCommands(LayerID layer = default)
        {
            var (layerIndex, foundIndices) = GetLayerIndex(layer, $"Clearing queued play commands");
            if (!foundIndices)
                return;

            layers[layerIndex].ClearQueuedPlayCommands();
        }

        // /// <summary>
        // /// As PlayAfterSeconds, but with the default state.
        // /// </summary>
        // /// <param name="seconds">How long to wait before playing the state.</param>
        // /// <param name="layer">Layer to play the state on.</param>
        // public void PlayDefaultStateAfterSeconds(float seconds, LayerID layer = default)
        // {
        //     PlayAfterSeconds(seconds, 0, layer);
        // }

        /// <summary>
        /// Jumps the current played state to a certain time between 0 and 1.
        /// Input time will be smartly modulated; 1.3 will be interpreted as 0.3, and -0.2 will be interpreted as 0.8
        /// Animation events will be skipped, and any ongoing blends will be cleared. @TODO: maybe have a parameter for these?
        /// </summary>
        /// <param name="time">The time to set.</param>
        /// <param name="layer">Layer to set the time of a state on.</param>
        public void JumpToRelativeTime(float time, LayerID layer = default)
        {
            var (layerIndex, foundIndex) = GetLayerIndex(layer, "Jumping to a relative time");
            if (!foundIndex)
                return;

            layers[layerIndex].JumpToRelativeTime(time);
        }

        /// <summary>
        /// Checks if the animation player has a state with the specified name.
        /// </summary>
        /// <param name="stateName">Name to check on</param>
        /// <param name="layer">Layer to check (default 0)</param>
        /// <returns>true if there is a state with the name on the layer</returns>
        public bool HasState(string stateName, LayerID layer = default)
        {
            var (layerIndex, foundIndex) = GetLayerIndex(layer, "Check if state exists");
            if (!foundIndex)
                return false;

            return layers[layerIndex].HasState(stateName);
        }

        /// <summary>
        /// Finds the weight of a state in the layer's blend, eg. how much the state is playing.
        /// This is a number between 0 and 1, with 0 for "not playing" and 1 for "playing completely"
        /// These do not neccessarilly sum to 1.
        /// </summary>
        /// <param name="state">State to check for</param>
        /// <param name="layer">Layer to check in</param>
        /// <returns>The weight for state in layer</returns>
        public float GetStateWeight(StateID state, LayerID layer)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Get the weight of a layer");
            if (!foundIndices)
                return 0;
            return layers[layerIndex].GetStateWeight(stateIndex);
        }

        /// <summary>
        /// Get the weight of a layer. This is it's current weight in the playable layer mixer.
        /// If there's only a single layer, this method always returns 1.
        /// </summary>
        /// <param name="layer">Layer to get the weight of.</param>
        /// <returns>The weight of layer in the layer mixer.</returns>
        public float GetLayerWeight(LayerID layer)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "Get the weight of a layer");
            if (!foundLayer)
                return 0;

            if (layers.Length < 2) {
                // The root playable is the layer's state mixer if there's only a single layer, so we need this special case
                return 1;
            }

            return rootPlayable.GetInputWeight(layerIndex);
        }

        /// <summary>
        /// Set the weight of a layer in the playable layer mixer.
        /// </summary>
        /// <param name="layer">Layer to set the weight of.</param>
        /// <param name="weight">Weight to set the layer to. \</param>
        public void SetLayerWeight(LayerID layer, float weight)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "Set the weight of a layer");
            if (!foundLayer)
                return;

            if (layers.Length < 2) {
                Debug.LogWarning($"You're setting the weight of {layer} to {weight}, but there's only one layer!");
            }

            rootPlayable.SetInputWeight(layerIndex, weight);
        }

        /// <summary>
        /// Get a state. You probably want to use other ways of interacting with the state, editing the details of the state generally doesn't affect the
        /// played graph.
        /// </summary>
        /// <param name="state">State to get</param>
        /// <param name="layer">Layer to get the state from</param>
        /// <returns>The state container.</returns>
        public AnimationState GetState(StateID state, LayerID layer = default)
        {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Get a state");
            if (!foundIndices)
                return null;

            return layers[layerIndex].states[stateIndex];
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="state"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public float GetStateDuration(StateID state, LayerID layer = default) {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Get a state's duration");
            if (!foundIndices)
                return 0f;

            return layers[layerIndex].states[stateIndex].Duration;
        }

        public double GetHowLongStateHasBeenPlaying(StateID state, LayerID layer = default) {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Get how long a state has been playing");
            if (!foundIndices)
                return 0f;

            return layers[layerIndex].GetHowLongStateHasBeenPlaying(stateIndex);
        }

        public double GetNormalizedStateProgress(StateID state, LayerID layer = default) {
            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Get a state's progress");
            if (!foundIndices)
                return 0f;

            return layers[layerIndex].GetNormalizedStateProgress(stateIndex);
        }

        /// <summary>
        /// Gets the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
        /// </summary>
        /// <param name="layer">Layer to get the currently playing state in.</param>
        /// <returns>The currently playing state.</returns>
        public AnimationState GetPlayingState(LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get the current playing state");
            if (!foundLayer)
                return null;

            return layers[layerIndex].GetCurrentPlayingState();
        }

        /// <summary>
        /// Gets the index of the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
        /// </summary>
        /// <param name="layer">Layer to get the currently playing state's index in.</param>
        /// <returns>The currently playing state's index.</returns>
        public int GetIndexOfPlayingState(LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get the index of the current playing state");
            if (!foundLayer)
                return -1;

            return layers[layerIndex].GetIndexOfPlayingState();
        }

        /// <summary>
        /// Gets the name of the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
        /// </summary>
        /// <param name="layer">Layer to get the currently playing state's name in.</param>
        /// <returns>The currently playing state's name.</returns>
        public string GetNameOfPlayingState(LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get the index of the current playing state");
            if (!foundLayer)
                return null;

            return layers[layerIndex].GetCurrentPlayingState().Name;
        }

        /// <summary>
        /// Retrives all of the currently playing states. The first element in the list will be the currently played state. All
        /// other results will be states that are not finished blending out
        /// </summary>
        /// <param name="results">Result container for the states.</param>
        /// <param name="layer">Layer to get the states from.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
        public void GetAllPlayingStates(List<AnimationState> results, LayerID layer = default, bool clearResultsList = true)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get all playing states");
            if (!foundLayer)
                return;

            if(clearResultsList)
                results.Clear();

            layers[layerIndex].AddAllPlayingStatesTo(results);
        }

        /// <summary>
        /// Retrives all of the indices of currently playing states. The first element in the list will be the currently played state. All
        /// other results will be states that are not finished blending out
        /// </summary>
        /// <param name="results">Result container for the state indices.</param>
        /// <param name="layer">Layer to get the state indices from.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the state indices are added?</param>
        public void GetAllPlayingStateIndices(List<int> results, LayerID layer = default, bool clearResultsList = true)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get all playing state indices");
            if (!foundLayer)
                return;

            if(clearResultsList)
                results.Clear();

            layers[layerIndex].AddAllPlayingStatesTo(results);
        }

        /// <summary>
        /// Retrives all of the names of currently playing states. The first element in the list will be the currently played state. All
        /// other results will be states that are not finished blending out.
        /// </summary>
        /// <param name="results">Result container for the state names.</param>
        /// <param name="layer">Layer to get state names from.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the state names are added?</param>
        public void GetAllPlayingStateNames(List<string> results, LayerID layer = default, bool clearResultsList = true)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get all playing state names");
            if (!foundLayer)
                return;

            if(clearResultsList)
                results.Clear();

            layers[layerIndex].AddAllPlayingStatesTo(results);
        }

        /// <summary>
        /// Get all of the states in a layer of the AnimationPlayer.
        /// </summary>
        /// <param name="results">Result container for the states.</param>
        /// <param name="layer">Layer to get the states from.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
        public void CopyAllStatesTo(List<AnimationState> results, LayerID layer = default, bool clearResultsList = true)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get all states");
            if (!foundLayer)
                return;

            if(clearResultsList)
                results.Clear();

            for (int i = 0; i < layers[layerIndex].states.Count; i++) {
                results.Add(layers[layerIndex].states[i]);
            }
        }

        /// <summary>
        /// Get all of the names of the states in the AnimationPlayer.
        /// </summary>
        /// <param name="results">Result container for the state naems.</param>
        /// <param name="layer">Layer to get the state names from.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
        public void CopyAllStateNamesTo(List<string> results, LayerID layer = default, bool clearResultsList = true)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get all state names");
            if (!foundLayer)
                return;

            if(clearResultsList)
                results.Clear();

            for (int i = 0; i < layers[layerIndex].states.Count; i++) {
                results.Add(layers[layerIndex].states[i].Name);
            }
        }

        private static List<string> allStateNamesHelper = new List<string>();
        public IEnumerable<string> GetAllStateNames(LayerID layer = default) {
            CopyAllStateNamesTo(allStateNamesHelper, layer);
            return allStateNamesHelper;
        }

        /// <summary>
        /// Gets how many states there are in a layer
        /// </summary>
        /// <param name="layer">Layer to check in</param>
        /// <returns>The number of states in the layer</returns>
        public int GetStateCount(LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get the state count");
            if (!foundLayer)
                return 0;

            return layers[layerIndex].states.Count;
        }

        /// <summary>
        /// Sets a blend var to a value. This will affect every state that blend variable controls.
        /// If you're setting a blend variable a lot - like setting "Speed" every frame based on a rigidbody's velocity, consider getting a BlendVarController
        /// instead, as that's faster.
        /// </summary>
        /// <param name="var">The blend variable to set.</param>
        /// <param name="value">The value to set it to.</param>
        /// <param name="layer">The layer to set the blend var on @TODO: Stop having blend variables be layer-based, that's crazy, I think</param>
        public void SetBlendVar(string var, float value, LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, $"set the blend var \"{var}\" to {value}");
            if (!foundLayer)
                return;

            layers[layerIndex].SetBlendVar(var, value);
        }

        /// <summary>
        /// Get the current value of a blend var
        /// </summary>
        /// <param name="var">The blend var to get the value of.</param>
        /// <param name="layer">Layer to get the blend var value in.</param>
        /// <returns>The current weight of var.</returns>
        public float GetBlendVar(string var, LayerID layer = default)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, $"get the blend var \"{var}\"");
            if (!foundLayer)
                return 0f;

            return layers[layerIndex].GetBlendVar(var);
        }

        /// <summary>
        /// Gets all blend variables on all layers.
        /// </summary>
        /// <returns>A list containing all the blend variables on all layers.</returns>
        public List<string> GetBlendVariables()
        {
            List<string> result = new List<string>();
            GetBlendVariables(result, false);
            return result;
        }

        /// <summary>
        /// Adds all blend variables on all layers to a list.
        /// </summary>
        /// <param name="result">Result to add the blend variables to.</param>
        /// <param name="clearResultsList">Should the results list be cleared before the blend variables are added?</param>
        private void GetBlendVariables(List<string> result, bool clearResultsList)
        {
            if(clearResultsList)
                result.Clear();

            foreach (var layer in layers)
            {
                layer.AddAllBlendVarsTo(result);
            }
        }

        /// <summary>
        /// Gets the index of a state from it's name.
        /// When you call Play("name"), the AnimationPlayer compares all state names with the name until it finds a match, so you can use this to cache that
        /// comparison for some  minor speed gains.
        /// </summary>
        /// <param name="state">The name of the state you're looking for.</param>
        /// <param name="layer">The layer to look for state in.</param>
        public int GetStateIndex(string state, LayerID layer = default)
        {
            var (_, stateIndex, foundIndex) = GetLayerAndStateIndices(state, layer, "Get a state from it's name");
            return foundIndex ? stateIndex : -1;
        }

        private (int stateIndex, bool foundState) GetStateIndex(StateID state, int layer, string actionIDForErrors)
        {
            var (_, stateIndex, foundIndex) = GetLayerAndStateIndices(state, layer, actionIDForErrors);
            return (stateIndex, foundIndex);
        }

        /// <summary>
        /// Gets the index of a layer from it's name.
        /// When you call Play(state, "layer"), the AnimationPlayer compares all layer names with the name until it finds a match, so you can use this to cache that
        /// comparison for some  minor speed gains.
        /// </summary>
        public int GetLayerIndex(string layer)
        {
            var (layerIndex, foundIndex) = GetLayerIndex(layer, "Get a layer from it's name");
            return foundIndex ? layerIndex : -1;
        }

        private (int layerIndex, bool foundIndex) GetLayerIndex(LayerID layerID, string actionIDForErrors) {
            try
            {
                var layerIndex = GetLayerIndex_ThrowOnFailure(layerID, actionIDForErrors);
                return (layerIndex, true);
            }
            catch (StateException exception)
            {
                Debug.LogException(exception, exception.context);
                return (-1, false);
            }
        }

        private (int layerIndex, int stateIndex, bool foundIndices) GetLayerAndStateIndices(StateID stateID, LayerID layerID, string actionIDForErrors)
        {
            try
            {
                var layerIndex = GetLayerIndex_ThrowOnFailure(layerID, actionIDForErrors);
                var stateIndex = GetStateIndex_ThrowOnFailure(stateID, layerID, layerIndex, actionIDForErrors);
                return (layerIndex, stateIndex, true);
            }
            catch (StateException exception)
            {
                Debug.LogException(exception, exception.context);
                return (-1, -1, false);
            }
        }

        private int GetLayerIndex_ThrowOnFailure(LayerID layerID, string actionIDForErrors) {
            int layerIndex;
            if (!layerID.isNameBased) {
                layerIndex = layerID.index;

                if (!(layerIndex >= 0 && layerIndex < layers.Length)) {
                    var errorMsg =
                        $"Trying to {actionIDForErrors} on an out of bounds layer! (layer {layerIndex}, but there are  {layers.Length} layers! " +
                        $"Error happened for AnimationPlayer on GameObject {gameObject.name})";
                    throw new StateException (errorMsg, gameObject);
                }
            }
            else {
                layerIndex = -1;
                for (int i = 0; i < layers.Length; i++) {
                    if (string.Equals(layerID.name, layers[i].name, StringComparison.InvariantCulture)) {
                        layerIndex = i;
                        break;
                    }
                }

                if (layerIndex == -1) {
                    var errorMsg =
                        $"Trying to {actionIDForErrors} on layer {layerID}, but that layer doesn't exist! Layers that exist are:\n" +
                        $"{layers.PrettyPrint(true, l => l.name)}\n" +
                        $"Error happened for AnimationPlayer on GameObject {gameObject.name})";
                    throw new StateException(errorMsg, gameObject);
                }
            }

            return layerIndex;

        }

        private int GetStateIndex_ThrowOnFailure(StateID stateID, LayerID layerID, int layerIndex, string actionIDForErrors) {
            if (!stateID.isNameBased)
            {
                if (!(stateID.index >= 0 && stateID.index < layers[layerIndex].states.Count)) {
                    if (!hasAwoken) {
                        throw new StateException (
                            $"Trying to {actionIDForErrors} on an out of bounds state, due to the Animation player not having awoken yet! State {stateID.index} Layer {layerIndex}" +
                            $"Error happened for AnimationPlayer on GameObject {gameObject.name})", gameObject
                        );
                    }
                    else {
                        throw new StateException (
                            $"Trying to {actionIDForErrors} on an out of bounds state! (state {stateID.index} on layer {layerIndex}, but there are {layers[layerIndex].states.Count} " +
                            $"states on that layer! " +
                            $"Error happened for AnimationPlayer on GameObject {gameObject.name})", gameObject
                        );
                    }
                }

                return stateID.index;
            }

            int stateIdx = layers[layerIndex].GetStateIdx(stateID.name);
            if (stateIdx == -1)
            {
                var errorMsg =
                    !hasAwoken ?
                        $"Trying to {actionIDForErrors} \"{stateID}\" on layer {layerID} before AnimationPlayer has had the time to Awake yet! " +
                        $"Please call {nameof(EnsureReady)}, or wait until Start with whatever you're doing! " +
                        $"Error happened for AnimationPlayer on GameObject {gameObject.name})"
                        :
                        $"Trying to {actionIDForErrors} \"{stateID}\" on layer {layerID}, but that doesn't exist! States that exist are:" +
                        $"\n{layers[layerIndex].states.PrettyPrint(true, s => s.Name)} " +
                        $"Error happened for AnimationPlayer on GameObject {gameObject.name})";

                throw new StateException(errorMsg, gameObject);
            }

            return stateIdx;
        }

        /// <summary>
        /// Register a listener for an animation event.
        /// </summary>
        /// <param name="targetEvent">Event to listen to.</param>
        /// <param name="listener">Action to be called when targetEvent fires.</param>
        public void RegisterAnimationEventListener(string targetEvent, Action listener)
        {
            bool registeredAny = false;
            foreach (var layer in layers)
            foreach (var state in layer.states)
            foreach (var animationEvent in state.animationEvents)
                if (animationEvent.name == targetEvent)
                {
                    animationEvent.RegisterListener(listener);
                    registeredAny = true;
                }

            if (!registeredAny)
                Debug.LogError($"Trying to register the event {targetEvent} on AnimationPlayer {name}, but it doesn't exist!", gameObject);
        }

        /// <summary>
        /// Swaps the currently playing clip on a state.
        /// Haven't quite figured out how this should work yet. Probably will get more options
        /// </summary>
        /// <param name="clip">The new clip to use on the state</param>
        /// <param name="state">Index of the state to swap the clip on. This state must be a SingleClipState (for now)</param>
        /// <param name="layer">Layer of the state</param>
        public void SwapClipOnState(AnimationClip clip, StateID state, LayerID layer = default)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("In edit mode, just set the clip on a state directly.");
                return;
            }

            var (layerIndex, stateIndex, foundIndices) = GetLayerAndStateIndices(state, layer, "Swap clip on a state");
            if (!foundIndices)
                return;

            layers[layerIndex].SwapClipOnState(stateIndex, clip, graph);
        }


        /// <summary>
        /// Gets the default transition between two states. This will either be a transition that's specifically defined for the two states, or the animation
        /// player's default transition if none are set up. This is the same transition as the one that will be used if the animationPlayer is in state from,
        /// and state to is played with no transition set.
        /// Use this to tweak the intended transition while being somewhat guarded against that transition being
        /// changed
        /// </summary>
        /// <param name="from">State to transition from</param>
        /// <param name="to">State to transition to</param>
        /// <param name="layer">Layer to get the transition on</param>
        /// <returns>The transition between from and to</returns>
        public TransitionData GetTransitionFromTo(StateID from, StateID to, LayerID layer)
        {
            var (layerIndex, foundLayer) = GetLayerIndex(layer, "get a transition between states");
            if (!foundLayer)
                return default;

            var (fromIndex, foundFromIndex) = GetStateIndex(from, layerIndex, "get a transition between states");
            if (!foundFromIndex)
                return default;

            var (toIndex, foundToIndex) = GetStateIndex(from, layerIndex, "get a transition between states");
            if (!foundToIndex)
                return default;

            return layers[layerIndex].GetTransitionFromTo(fromIndex, toIndex);
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
        public int AddState(AnimationState state, LayerID layer = default)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Don't call AnimationPlayer.AddState at runtime! Just add states to the layers directly!");
                return -1;
            }

            var (layerIndex, foundLayer) = GetLayerIndex(layer, $"Add an animation state");
            if (!foundLayer)
                return -1;

            return layers[layerIndex].AddState(state);
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

        private class StateException : Exception {
            public readonly UObject context;
            public StateException(string message, UObject context) : base(message) {
                this.context = context;
            }
        }

        private List<AnimationClip> allClipsInPlayer = new List<AnimationClip>();
        public void GetAnimationClips(List<AnimationClip> results) {
            if (allClipsInPlayer.Count == 0) {
                foreach (var layer in layers) {
                    layer.AddAllClipsInStatesTo(allClipsInPlayer);
                }
            }

            foreach (var clip in allClipsInPlayer) {
                if (!results.Contains(clip))
                    results.Add(clip);
            }
        }
    }


    public struct QueueInstruction
    {
        internal QueueStateType type;
        internal float timeParam;
        internal bool boolParam;

        public bool CountFromQueued => boolParam;

        public static QueueInstruction AfterSeconds(float seconds, bool countFromNow = true)
        {
            return new QueueInstruction
            {
                type      = QueueStateType.AfterSeconds,
                timeParam = seconds,
                boolParam = countFromNow,
            };
        }

        public static QueueInstruction WhenCurrentDone()
        {
            return default;
        }

        public static QueueInstruction SecondsBeforeDone(float seconds)
        {
            return new QueueInstruction
            {
                type = QueueStateType.BeforeCurrentDone_Seconds,
                timeParam = seconds
            };
        }

        /// <summary>
        /// The next state should be played before the current state is finished by a fraction of it's duration.
        /// So if the current animation is 3 seconds long, and the fraction is 0.1f, the next state should be played 0.3 seconds
        /// before the current state is finished.
        ///
        /// The fraction is taken as a fraction of the state that is playing when this instruction becomes the next instruction. So if you:
        /// Play(1 second anim)
        /// Queue(2 second anim)
        ///
        /// and then call this with a fraction of .1f, that fraction will resolve to 0.2 seconds.
        /// </summary>
        /// <param name="fraction">Fraction of the played animation's duration.</param>
        /// <returns>A queue instruction set up a fraction before the played state is finished.</returns>
        public static QueueInstruction FractionBeforeDone(float fraction)
        {
            return new QueueInstruction
            {
                type = QueueStateType.BeforeCurrentDone_Relative,
                timeParam = fraction
            };
        }
    }

    internal enum QueueStateType
    {
        WhenCurrentDone,
        AfterSeconds,
        BeforeCurrentDone_Seconds,
        BeforeCurrentDone_Relative,
    }
}