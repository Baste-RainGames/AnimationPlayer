using System;
using System.Collections.Generic;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Profiling;

using Debug = UnityEngine.Debug;
using UObject = UnityEngine.Object;

namespace Animation_Player
{
public class AnimationPlayer : MonoBehaviour, IAnimationClipSource
{
    // Serialized data:
    private const int lastVersion = 2;
    [SerializeField, HideInInspector] private int versionNumber;
    [SerializeField] private bool showInVisualizer;

    public static int DefaultState => 0;

    public List<AnimationLayer> layers;
    public TransitionData defaultTransition;

    [SerializeField] internal List<ClipSwapCollection> clipSwapCollections = new List<ClipSwapCollection>();

    //Runtime fields:

    [NonSerialized] private bool hasAwoken;
    [NonSerialized] private Playable rootPlayable;
    [NonSerialized] private string visualizerClientName;
    [NonSerialized] private Renderer[] childRenderersForVisibilityChecks;

    private PlayableGraph graph;
    public PlayableGraph Graph => graph;

    private bool _cullCheckingActive;
    public bool CullCheckingActive
    {
        get => _cullCheckingActive;
        set
        {
            if (_cullCheckingActive != value)
            {
                _cullCheckingActive = true;
                childRenderersForVisibilityChecks = value ? GetComponentsInChildren<Renderer>() : null;
            }
        }
    }

    private readonly Dictionary<string, float> blendVariableValues = new ();
    private readonly List<string>              blendVariablesUpdatedThisFrame = new ();

    public Animator OutputAnimator { get; private set; }

    //IK. Note that IK requires an empty AnimatorController with... why am I doing this like this?
    private float currentIKLookAtWeight;
    private Vector3 currentIKLookAtPosition;

    private void Awake()
    {
        if (hasAwoken)
            return;
        hasAwoken = true;

        AnimationPlayerUpdater.RegisterAnimationPlayer(this);

        EnsureVersionUpgraded();

        if (layers.Count == 0)
            return;
        bool anyLayersWithStates = false;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].states.Count > 0)
            {
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

        for (var i = 0; i < layers.Count; i++)
            layers[i].InitializeSelf(graph, defaultTransition, clipSwapCollections, blendVariableValues);

        if (layers.Count <= 1)
        {
            rootPlayable = layers[0].stateMixer;
        }
        else
        {
            var layerMixer = AnimationLayerMixerPlayable.Create(graph, layers.Count);

            for (var i = 0; i < layers.Count; i++)
                layers[i].InitializeLayerBlending(graph, i, layerMixer);

            rootPlayable = layerMixer;
        }

        var ikConnection = GetComponent<IIKAnimationPlayerConnection>();
        if (ikConnection != null)
        {
            var ikPlayable = ikConnection.GeneratePlayable(OutputAnimator, graph);
            ikPlayable.AddInput(rootPlayable, 0, 1f);
            animOutput.SetSourcePlayable(ikPlayable);
        }
        else
        {
            animOutput.SetSourcePlayable(rootPlayable);
        }

        //fun fact: default is DSPClock!
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        graph.Play();

        SetVizualizerName(name + " AnimationPlayer", true);
    }

    public bool IsCulled
    {
        get
        {
            if (!Application.isPlaying)
                return false;

            if (!CullCheckingActive)
            {
                Debug.LogError($"Asking AnimationPlayer {name} if it's culled before first activating cull checking (CullCheckingActive = true", gameObject);
                return false;
            }

            if (OutputAnimator == null || OutputAnimator.cullingMode != AnimatorCullingMode.CullCompletely)
                return false;

            if (childRenderersForVisibilityChecks.Length == 0)
                Debug.LogWarning($"Asking AnimationPlayer {name} if it's culled, but no renderer children were found", gameObject);

            foreach (var rend in childRenderersForVisibilityChecks)
                if (rend.isVisible)
                    return false;

            return true;
        }
    }

    public void AddLayer(int index, AnimationLayerType type, float weight, AvatarMask mask = null)
    {
        var newLayer = new AnimationLayer
        {
            type = type,
            startWeight = weight,
            states = new (),
            transitions = new (),
            mask = mask
        };

        layers.Insert(index, newLayer);
        layers[index].InitializeSelf(graph, defaultTransition, clipSwapCollections, blendVariableValues);
    }

    public void SetVizualizerName(string newName, bool forceShowInVisualizer = false)
    {
        if (forceShowInVisualizer)
            showInVisualizer = true;

        if (showInVisualizer)
        {
            GraphVisualizerClient.Hide(graph);
            GraphVisualizerClient.Show(graph, newName);
        }
    }

    // Called from AnimationPlayerUpdater, after Update has been called on other scripts
    internal void UpdateSelf()
    {
        Profiler.BeginSample("Update blend variables");
        if (blendVariablesUpdatedThisFrame.Count > 0)
        {
            foreach (var layer in layers)
                layer.UpdateBlendVariables(blendVariableValues, blendVariablesUpdatedThisFrame);
            blendVariablesUpdatedThisFrame.Clear();
        }

        Profiler.EndSample();

        Profiler.BeginSample("Update layers");
        foreach (var layer in layers)
            layer.Update();
        Profiler.EndSample();
    }

    private void OnDestroy()
    {
        AnimationPlayerUpdater.DeregisterAnimationPlayer(this);

        if (graph.IsValid())
            graph.Destroy();
    }

#if UNITY_EDITOR
    private Vector3 positionBeforePreview;

    public void EnterPreview()
    {
        hasAwoken = false;
        Awake();
        positionBeforePreview = transform.position;
    }

    public void ExitPreview()
    {
        OnDestroy();
        hasAwoken = false;
        if (positionBeforePreview != transform.position)
            transform.position = positionBeforePreview;
    }
#endif

    /// <summary>
    /// Ensures that the AnimationPlayer is ready - ie. has had Awake called. Use this if you're calling something before you can be sure that the
    /// AnimationPlayer's gotten around to calling Awake yet, like if you're calling into AnimationPlayer from Awake.
    /// </summary>
    public void EnsureReady()
    {
        if (!hasAwoken)
            Awake();
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
        layers[layer].Play(state);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Play a state, using the defined transition between the current state and that state if it exists,
    /// or the player's default transition if it doesn't.
    /// The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state name to play</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(Play)))
            Play(stateID);
    }
#endif

    /// <summary>
    /// Play a state, using the transition defined by transition.
    /// The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="transition">transition to use</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(int state, string transition, int layer = 0)
    {
        layers[layer].Play(state, transition);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Play a state, using the transition defined by transition.
    /// The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="transition">transition to use</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(string state, string transition, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(Play)))
            Play(stateID, transition);
    }
#endif

    /// <summary>
    /// Play a state, using a custom transition. The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="transitionData">How to transition into the state</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(int state, TransitionData transitionData, int layer = 0)
    {
        if (transitionData is { type: TransitionType.Curve, curve: null })
        {
            Debug.LogError(
                $"Trying to transition using a curve, but the curve is null! " +
                $"Error happened for AnimationPlayer on GameObject {gameObject.name})", gameObject
            );
            return;
        }

        layers[layer].Play(state, transitionData, "Custom");
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Play a state, using a custom transition. The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="transitionData">How to transition into the state</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(string state, TransitionData transitionData, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(Play)))
            Play(stateID, transitionData);
    }
#endif

    /// <summary>
    /// Play a state, using an instant transition. The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void SnapTo(int state, int layer = 0)
    {
        Play(state, TransitionData.Instant(), layer);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Play a state, using an instant transition. The state will immediately be the current played state.
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void SnapTo(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out int stateID, nameof(SnapTo)))
            SnapTo(stateID, layer);
    }
#endif

    /// <summary>
    /// Plays the default state of the state machine
    /// </summary>
    /// <param name="layer">Layer to play the default state on</param>
    public void PlayDefaultState(int layer = 0)
    {
        Play(0, layer);
    }

    /// <summary>
    /// Plays the default state of the state machine
    /// </summary>
    /// <param name="transitionData">Custom transition to use</param>
    /// <param name="layer">Layer to play the default state on</param>
    public void PlayDefaultState(TransitionData transitionData, int layer = 0)
    {
        Play(0, transitionData, layer);
    }

    /// <summary>
    /// Checks if a specific state is the currently played state. When you Play() a state, that state will immediately be considered the "playing" state.
    /// </summary>
    /// <param name="state">State to check if is playing</param>
    /// <param name="layer">Layer to check if the state is playing on</param>
    /// <returns>True if state is the state currently playing on layer</returns>
    public bool IsPlaying(int state, int layer = default)
    {
        return layers[layer].GetIndexOfPlayingState() == state;
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Checks if a specific state is the currently played state. When you Play() a state, that state will immediately be considered the "playing" state.
    /// </summary>
    /// <param name="state">State to check if is playing</param>
    /// <param name="layer">Layer to check if the state is playing on</param>
    /// <returns>True if state is the state currently playing on layer</returns>
    public bool IsPlaying(string state, int layer = default)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(IsPlaying)))
            return IsPlaying(stateID, layer);
        return false;
    }
#endif

    /// <summary>
    /// Queue a state to change. By default, the change happens after the previous state has ended, this can be changed by setting the instruction parameter.
    /// This method creates a queue if you call it several times in a row. Play(a); Queue(b); Queue(c); will cause a to play, then b to play, then c.
    /// If you call Play or SnapTo or any other method that changes the played state, the current queue is discarded.
    /// </summary>
    /// <param name="state">State to queue change to</param>
    /// <param name="instruction">When the state change should happen in relation to the previous playing state</param>
    /// <param name="transition">How to transition between the states</param>
    /// <param name="layer">Layer to queue the change on</param>
    public void QueueStateChange(int state, QueueInstruction instruction = default, TransitionData? transition = null, int layer = default)
    {
        layers[layer].QueuePlayCommand(state, instruction, transition, "Custom");
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Queue a state to change. By default, the change happens after the previous state has ended, this can be changed by setting the instruction parameter.
    /// This method creates a queue if you call it several times in a row. Play(a); Queue(b); Queue(c); will cause a to play, then b to play, then c.
    /// If you call Play or SnapTo or any other method that changes the played state, the current queue is discarded.
    /// </summary>
    /// <param name="state">State to queue change to</param>
    /// <param name="instruction">When the state change should happen in relation to the previous playing state</param>
    /// <param name="transition">How to transition between the states</param>
    /// <param name="layer">Layer to queue the change on</param>
    public void QueueStateChange(string state, QueueInstruction instruction = default, TransitionData? transition = null, int layer = default)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(QueueStateChange)))
            QueueStateChange(stateID, instruction, transition, layer);
    }
#endif

    public void ClearQueuedPlayCommands(int layer = 0)
    {
        layers[layer].ClearQueuedPlayCommands();
    }

    /// <summary>
    /// Jumps the current played state to a certain time between 0 and 1.
    /// Input time will be smartly modulated; 1.3 will be interpreted as 0.3, and -0.2 will be interpreted as 0.8
    /// Animation events will be skipped, and any ongoing blends will be cleared.
    /// </summary>
    /// <param name="time">The time to set.</param>
    /// <param name="layer">Layer to set the time of a state on.</param>
    public void JumpToRelativeTime(float time, int layer = 0)
    {
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
    public float GetStateWeight(int state, int layer = 0)
    {
        return layers[layer].GetStateWeight(state);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
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
        if (TryGetStateIndex(state, layer, out var stateID, nameof(GetStateWeight)))
            return GetStateWeight(stateID, layer);
        return 0f;
    }
#endif

    /// <summary>
    /// Get the weight of a layer. This is it's current weight in the playable layer mixer.
    /// If there's only a single layer, this method always returns 1.
    /// </summary>
    /// <param name="layer">Layer to get the weight of.</param>
    /// <returns>The weight of layer in the layer mixer.</returns>
    public float GetLayerWeight(int layer)
    {
        if (layers.Count < 2)
        {
            // The root playable is the layer's state mixer if there's only a single layer, so we need this special case
            return 1;
        }

        return rootPlayable.GetInputWeight(layer);
    }

    /// <summary>
    /// Set the weight of a layer in the playable layer mixer.
    /// </summary>
    /// <param name="layer">Layer to set the weight of.</param>
    /// <param name="weight">Weight to set the layer to. \</param>
    public void SetLayerWeight(int layer, float weight)
    {
        if (layers.Count < 2)
        {
            Debug.LogWarning($"You're setting the weight of {layer} to {weight}, but there's only one layer!");
        }

        rootPlayable.SetInputWeight(layer, weight);
    }

    /// <summary>
    /// Get a state. NOTE; modifying the state at runtime is a bad idea unless you know what you're doing
    /// </summary>
    /// <param name="state">State to get</param>
    /// <param name="layer">Layer to get the state from</param>
    /// <returns>The state container.</returns>
    public AnimationPlayerState GetState(int state, int layer = 0)
    {
        return layers[layer].states[state];
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Get a state. NOTE; modifying the state at runtime is a bad idea unless you know what you're doing
    /// </summary>
    /// <param name="state">State to get</param>
    /// <param name="layer">Layer to get the state from</param>
    /// <returns>The state container.</returns>
    public AnimationPlayerState GetState(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(GetState)))
            return GetState(stateID, layer);
        return null;
    }
#endif

    /// <summary>
    /// Gets the duration of a state. What this is depends on what kind of state it is, and can change if eg. clip swaps are applied.
    /// </summary>
    /// <param name="state">Name of the state to get the duration of</param>
    /// <param name="layer">Layer to check the duration on</param>
    /// <returns>The duration of the state.</returns>
    public float GetStateDuration(int state, int layer = 0)
    {
        return layers[layer].states[state].Duration;
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Gets the duration of a state. What this is depends on what kind of state it is, and can change if eg. clip swaps are applied.
    /// </summary>
    /// <param name="state">Name of the state to get the duration of</param>
    /// <param name="layer">Layer to check the duration on</param>
    /// <returns>The duration of the state.</returns>
    public float GetStateDuration(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(GetStateDuration)))
            return GetStateDuration(stateID, layer);
        return 0f;
    }
#endif

    /// <summary>
    /// Finds how long a state has been playing - this means how long the state has had a weight above 0.
    /// </summary>
    /// <param name="state">State to check</param>
    /// <param name="layer">Layers to check on</param>
    /// <returns>How long the state has been playing</returns>
    public double GetHowLongStateHasBeenPlaying(int state, int layer = 0)
    {
        return layers[layer].GetHowLongStateHasBeenPlaying(state);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Finds how long a state has been playing - this means how long the state has had a weight above 0.
    /// </summary>
    /// <param name="state">State to check</param>
    /// <param name="layer">Layers to check on</param>
    /// <returns>How long the state has been playing</returns>
    public double GetHowLongStateHasBeenPlaying(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(GetHowLongStateHasBeenPlaying)))
            return GetHowLongStateHasBeenPlaying(stateID, layer);
        return 0f;
    }
#endif

    /// <summary>
    /// Finds the normalized progress of a state. This means 0 if it just started, and 1 if it's at it's final frame.
    /// </summary>
    /// <param name="state">State to find the normalized progress of</param>
    /// <param name="layer">Layer to find the state progress on</param>
    /// <returns>The normalized progress of the state.</returns>
    public double GetNormalizedStateProgress(int state, int layer = 0)
    {
        return layers[layer].GetNormalizedStateProgress(state);
    }

#if !ANIMATION_PLAYER_FORCE_INTEGER_STATES
    /// <summary>
    /// Finds the normalized progress of a state. This means 0 if it just started, and 1 if it's at it's final frame.
    /// </summary>
    /// <param name="state">State to find the normalized progress of</param>
    /// <param name="layer">Layer to find the state progress on</param>
    /// <returns>The normalized progress of the state.</returns>
    public double GetNormalizedStateProgress(string state, int layer = 0)
    {
        if (TryGetStateIndex(state, layer, out var stateID, nameof(GetNormalizedStateProgress)))
            return GetNormalizedStateProgress(stateID, layer);
        return 0f;
    }
#endif

    /// <summary>
    /// Gets the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
    /// </summary>
    /// <param name="layer">Layer to get the currently playing state in.</param>
    /// <returns>The currently playing state.</returns>
    public AnimationPlayerState GetPlayingState(int layer = 0)
    {
        return layers[layer].GetCurrentPlayingState();
    }

    /// <summary>
    /// Gets the index of the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
    /// </summary>
    /// <param name="layer">Layer to get the currently playing state's index in.</param>
    /// <returns>The currently playing state's index.</returns>
    public int GetIndexOfPlayingState(int layer = 0)
    {
        return layers[layer].GetIndexOfPlayingState();
    }

    /// <summary>
    /// Gets the name of the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
    /// </summary>
    /// <param name="layer">Layer to get the currently playing state's name in.</param>
    /// <returns>The currently playing state's name.</returns>
    public string GetNameOfPlayingState(int layer = 0)
    {
        return layers[layer].GetCurrentPlayingState().Name;
    }

    /// <summary>
    /// Retrives all of the currently playing states. The first element in the list will be the currently played state. All
    /// other results will be states that are not finished blending out
    /// </summary>
    /// <param name="results">Result container for the states.</param>
    /// <param name="layer">Layer to get the states from.</param>
    /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
    public void GetAllPlayingStates(List<AnimationPlayerState> results, int layer = 0, bool clearResultsList = true)
    {
        if (clearResultsList)
            results.Clear();

        layers[layer].AddAllPlayingStatesTo(results);
    }

    /// <summary>
    /// Retrives all of the indices of currently playing states. The first element in the list will be the currently played state. All
    /// other results will be states that are not finished blending out
    /// </summary>
    /// <param name="results">Result container for the state indices.</param>
    /// <param name="layer">Layer to get the state indices from.</param>
    /// <param name="clearResultsList">Should the results list be cleared before the state indices are added?</param>
    public void GetAllPlayingStateIndices(List<int> results, int layer = 0, bool clearResultsList = true)
    {
        if (clearResultsList)
            results.Clear();

        layers[layer].AddAllPlayingStatesTo(results);
    }

    /// <summary>
    /// Retrives all of the names of currently playing states. The first element in the list will be the currently played state. All
    /// other results will be states that are not finished blending out.
    /// </summary>
    /// <param name="results">Result container for the state names.</param>
    /// <param name="layer">Layer to get state names from.</param>
    /// <param name="clearResultsList">Should the results list be cleared before the state names are added?</param>
    public void GetAllPlayingStateNames(List<string> results, int layer = 0, bool clearResultsList = true)
    {
        if (clearResultsList)
            results.Clear();

        layers[layer].AddAllPlayingStatesTo(results);
    }

    /// <summary>
    /// Get all of the states in a layer of the AnimationPlayer.
    /// </summary>
    /// <param name="results">Result container for the states.</param>
    /// <param name="layer">Layer to get the states from.</param>
    /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
    public void CopyAllStatesTo(List<AnimationPlayerState> results, int layer = 0, bool clearResultsList = true)
    {
        if (clearResultsList)
            results.Clear();

        foreach (var state in layers[layer].states)
            results.Add(state);
    }

    /// <summary>
    /// Get all of the names of the states in the AnimationPlayer.
    /// </summary>
    /// <param name="results">Result container for the state naems.</param>
    /// <param name="layer">Layer to get the state names from.</param>
    /// <param name="clearResultsList">Should the results list be cleared before the states are added?</param>
    public void CopyAllStateNamesTo(List<string> results, int layer = 0, bool clearResultsList = true)
    {
        if (clearResultsList)
            results.Clear();

        foreach (var state in layers[layer].states)
            results.Add(state.Name);
    }

    /// <summary>
    /// Gets how many states there are in a layer
    /// </summary>
    /// <param name="layer">Layer to check in</param>
    /// <returns>The number of states in the layer</returns>
    public int GetStateCount(int layer = default)
    {
        return layers[layer].states.Count;
    }

    /// <summary>
    /// Sets a blend var to a value. This will affect every state that blend variable controls.
    /// If you're setting a blend variable a lot - like setting "Speed" every frame based on a rigidbody's velocity, consider getting a BlendVarController
    /// instead, as that's faster.
    /// </summary>
    /// <param name="variable">The blend variable to set.</param>
    /// <param name="value">The value to set it to.</param>
    public void SetBlendVar(string variable, float value)
    {
        AssertBlendVariableDefined(variable, nameof(SetBlendVar));

        blendVariableValues[variable] = value;
        blendVariablesUpdatedThisFrame.EnsureContains(variable);
    }

    /// <summary>
    /// Get the current value of a blend var
    /// </summary>
    /// <param name="variable">The blend var to get the value of.</param>
    /// <returns>The current weight of var.</returns>
    public float GetBlendVar(string variable)
    {
        AssertBlendVariableDefined(variable, nameof(GetBlendVar));

        if (blendVariableValues.TryGetValue(variable, out var value))
            return value;
        return 0f;
    }

    public (float min, float max) GetMinAndMaxValuesForBlendVar(string variable)
    {
        if (!blendVariableValues.ContainsKey(variable))
        {
            Debug.LogError($"Trying to get the minimum value for blend var {variable} from AnimationPlayer {name}, but that variable isn't used in any states!",
                           this);
            return (0f, 0f);
        }

        var min = 0f;
        var max = 0f;
        var minSet = false;
        var maxSet = false;

        foreach (var animationLayer in layers)
        {
            if (animationLayer.TryGetMinAndMaxValuesForBlendVar(variable, out var minVal, out var maxVal))
            {
                if (!minSet || minVal < min)
                {
                    minSet = true;
                    min = minVal;
                }

                if (!maxSet || maxVal < max)
                {
                    maxSet = true;
                    max = maxVal;
                }
            }
        }

        return (min, max);
    }

    /// <summary>
    /// Gets all blend variables on all layers.
    /// </summary>
    /// <returns>A list containing all the blend variables on all layers.</returns>
    public List<string> GetBlendVariables()
    {
        List<string> result = new List<string>();
        GetBlendVariables(result);
        return result;
    }

    /// <summary>
    /// Adds all blend variables on all layers to a list.
    /// </summary>
    /// <param name="result">Result to add the blend variables to.</param>
    private void GetBlendVariables(List<string> result)
    {
        result.Clear();

        foreach (var variable in blendVariableValues.Keys)
        {
            result.Add(variable);
        }
    }

    /// <summary>
    /// Gets the index of a state from it's name.
    /// Use this to cache the index of a state, to be a bit faster than Play("name"), as Play("name") calls this internally.
    /// </summary>
    /// <param name="stateName">The name of the state you're looking for.</param>
    /// <param name="layer">The layer to look for state in.</param>
    public int GetStateIndex(string stateName, int layer = 0)
    {
        AssertLayer(layer, nameof(GetStateIndex));

        for (var i = 0; i < layers[layer].states.Count; i++)
        {
            var state = layers[layer].states[i];
            if (state.Name == stateName)
                return i;
        }

        AssertionFailure(
            $"Layer {layers[layer].name} doesn't have a state named {stateName}. It has the states {layers[layer].states.PrettyPrint(s => s.Name)}");
        return -1;
    }


    public bool TryGetStateIndex(string stateName, out int index, int layer = 0)
    {
        return TryGetStateIndex(stateName, layer, out index);
    }

    private bool TryGetStateIndex(string stateName, int layer, out int stateID, string printErrorWithMethodName = null)
    {
        AssertLayer(layer, nameof(TryGetStateIndex));

        for (var i = 0; i < layers[layer].states.Count; i++)
        {
            var state = layers[layer].states[i];
            if (state.Name == stateName)
            {
                stateID = i;
                return true;
            }
        }

        if (printErrorWithMethodName != null)
            AssertionFailure($"Couldn't find state {stateName} when calling {printErrorWithMethodName}");
        stateID = -1;
        return false;
    }

    /// <summary>
    /// Gets the index of a state from it's name.
    /// Use this to cache the index of a state, to be a bit faster than Play(state, "layer"), as Play(state, "layer") calls this internally.
    /// </summary>
    public int GetLayerIndex(string layerName)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].name == layerName)
                return i;
        }

        Assert.IsTrue(false, $"There's no layer named {layerName}");
        return -1;
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
    /// Register a listener for an animation event on the currently played state. When the state changes, the event is dropped.
    /// Usefull to play an animation, and then react to an event once.
    /// </summary>
    /// <param name="targetEvent">Event to listen to.</param>
    /// <param name="listener">Action to be called when targetEvent fires.</param>
    public void RegisterAnimationEventListenerForCurrentState(string targetEvent, Action listener)
    {
        bool registeredAny = false;
        foreach (var layer in layers)
        foreach (var animationEvent in layer.GetCurrentPlayingState().animationEvents)
        {
            if (animationEvent.name == targetEvent)
            {
                animationEvent.RegisterListenerForCurrentState(listener);
                registeredAny = true;
            }
        }

        if (!registeredAny)
        {
            var eventDebugString = layers.PrettyPrint(true, l => $"{l.GetCurrentPlayingState().Name} with events " +
                                                                 $"{l.GetCurrentPlayingState().animationEvents.PrettyPrint(ae => ae.name)}");

            Debug.LogError($"Trying to register the event {targetEvent} for the current state on AnimationPlayer {name}, but no state with that event " +
                           $"is playing on any layers! States playing are {eventDebugString}", gameObject);
        }
    }

    /// <summary>
    /// Check how long it is before a certain event will fire. This only works if the current played state has the event, it doesn't try to
    /// do calculations on states that are fading out but has events that can fire even if they're not the active state.
    /// </summary>
    /// <param name="targetEvent">Event to check for</param>
    /// <returns>If the event will fire, and how long it is until the event will fire if it will.</returns>
    public (bool willFire, double timeUntilWillFire) TimeUntilEvent(string targetEvent)
    {
        var minTime = double.MaxValue;
        var foundEvent = false;
        var foundEventPassed = false;

        foreach (var layer in layers)
        {
            var playingState = layer.GetCurrentPlayingState();
            foreach (var animationEvent in playingState.animationEvents)
            {
                if (animationEvent.name == targetEvent)
                {
                    var eventTime = animationEvent.time;
                    var currentTime = layer.GetHowLongStateHasBeenPlaying(layer.GetIndexOfPlayingState());

                    if (playingState.Loops)
                        currentTime = currentTime % playingState.Duration;

                    if (currentTime > eventTime)
                    {
                        if (!foundEvent)
                        {
                            foundEvent = true;
                            foundEventPassed = true;
                        }
                    }
                    else
                    {
                        foundEvent = true;
                        foundEventPassed = false;

                        var timeUntilEvent = eventTime - currentTime;
                        if (timeUntilEvent < minTime)
                        {
                            minTime = timeUntilEvent;
                        }
                    }
                }
            }
        }

        if (!foundEvent)
        {
            AssertionFailure($"Couldn't find event {targetEvent} in any of the playing states! Playing states are " +
                             $"{layers.PrettyPrint(l => l.GetCurrentPlayingState().Name)}");
            return (false, 0);
        }

        if (foundEventPassed)
            return (false, 0);
        return (true, minTime);
    }

    /// <summary>
    /// Swaps the currently playing clip on a state.
    /// Haven't quite figured out how this should work yet. Probably will get more options
    /// </summary>
    /// <param name="clip">The new clip to use on the state</param>
    /// <param name="state">Index of the state to swap the clip on. This state must be a SingleClipState (for now)</param>
    /// <param name="layer">Layer of the state</param>
    public void SwapClipOnState(AnimationClip clip, int state, int layer = 0)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("In edit mode, just set the clip on a state directly.");
            return;
        }

        layers[layer].SwapClipOnState(state, clip);
    }


    /// <summary>
    /// Gets the default transition between two states. This will either be a transition that's specifically defined for the two states, or the animation
    /// player's default transition if none are set up. This is the same transition as the one that will be used if the animationPlayer is in state from,
    /// and state to is played with no transition set.
    /// Use this to tweak the intended transition while being somewhat guarded against that transition being
    /// changed
    /// </summary>
    /// <param name="fromState">State to transition from</param>
    /// <param name="toState">State to transition to</param>
    /// <param name="layer">Layer to get the transition on</param>
    /// <returns>The transition between from and to</returns>
    public TransitionData GetTransitionFromTo(int fromState, int toState, int layer = 0)
    {
        return layers[layer].GetDefaultTransitionFromTo(fromState, toState).transition;
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
    public void SetIKLookAtPosition(Vector3 position)
    {
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
    public bool HasBlendVarLayer(string blendVar)
    {
        return blendVariableValues.ContainsKey(blendVar);
    }

    /// <summary>
    /// Activates or deactivates a clip swap. These are set up at edit time, and makes all states that use a clip use a different clip instead.
    /// No blending will happen if the current playing state is using a clip affected by the clip swap, so there might be discontinuities.
    /// </summary>
    /// <param name="name">Name of the clip swap.</param>
    /// <param name="active">Should the clip swap be active?</param>
    public void SetClipSwapActive(string name, bool active)
    {
        var found = false;
        var changed = false;

        foreach (var clipSwapCollection in clipSwapCollections)
        {
            if (clipSwapCollection.name == name)
            {
                found = true;
                changed = active != clipSwapCollection.active;
                clipSwapCollection.active = active;
            }
        }

        if (!found)
        {
            Debug.LogError($"Couldn't find the clip swap {name} on the AnimationPlayer {gameObject.name}. The ones that exist are:\n" +
                           $"{clipSwapCollections.PrettyPrint(csc => csc.name)}");
            return;
        }

        if (changed)
        {
            foreach (var layer in layers)
            {
                layer.OnClipSwapsChanged();
            }
        }
    }

    public int GetNumberOfStateChanges(int layer = 0)
    {
        return layers[layer].GetNumberOfStateChanges();
    }

    /// <summary>
    /// Adds a new state to the AnimationPlayer, and makes sure that all the graphs are correctly setup.
    /// At edit time, just add new states directly into the layer's states
    /// </summary>
    /// <param name="state">State to add.</param>
    /// <param name="layer">Layer to add the state to.</param>
    /// <returns>The index of the added state</returns>
    public int AddState(AnimationPlayerState state, int layer = 0)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Don't call AnimationPlayer.AddState at runtime! Just add states to the layers directly!");
            return -1;
        }

        return layers[layer].AddState(state, blendVariableValues);
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

        if (versionNumber < 2 && layers != null)
        {
            foreach (var layer in layers)
            {
                foreach (var transition in layer.transitions)
                {
                    transition.name = "Transition";
                }
            }
        }

        versionNumber = lastVersion;
        return true;
    }

    private readonly List<AnimationClip> allClipsInPlayer = new List<AnimationClip>();

    public void GetAnimationClips(List<AnimationClip> results)
    {
        allClipsInPlayer.Clear();
        foreach (var layer in layers)
        {
            layer.AddAllClipsInStatesAndTransitionsTo(allClipsInPlayer);
        }

        foreach (var clip in allClipsInPlayer)
        {
            if (!results.Contains(clip))
                results.Add(clip);
        }
    }

    /// <summary>
    /// Check if the animation player is transitioning on a state
    /// </summary>
    /// <param name="layer">Layer to check</param>
    /// <returns>True if the layer is transitioning</returns>
    public bool IsTransitioning(int layer = 0)
    {
        return layers[layer].IsTransitioning();
    }

    public bool IsInTransition(string transition, int layer = 0)
    {
        return layers[layer].IsInTransition(transition);
    }

    // really want Assert.Fail, but that's private
    [Conditional("UNITY_ASSERTIONS")]
    private void AssertionFailure(string userMessage)
    {
        throw new AssertionException("AnimationPlayer Assertion Failure: ", userMessage);
    }

    [Conditional("UNITY_ASSERTIONS")]
    public void AssertBlendVariableDefined(string variable, string methodName)
    {
        if (!blendVariableValues.ContainsKey(variable))
            AssertionFailure($"Couldn't find blend variable {variable} when calling {methodName}");
    }

    [Conditional("UNITY_ASSERTIONS")]
    public void AssertLayer(int layer, string methodName)
    {
        if (layer < 0 || layer > layers.Count - 1)
            AssertionFailure($"layer {layer} is out of bounds when calling {methodName}");
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