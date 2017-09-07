using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Debug = UnityEngine.Debug;

public class AnimationPlayer : MonoBehaviour
{
    public AnimationLayer[] layers;
    public TransitionData defaultTransition;

    private PlayableGraph graph;

    private Playable rootPlayable;
    private string visualizerClientName;

#if UNITY_EDITOR
    //Used to make the inspector continually update
    public Action editTimeUpdateCallback;
#endif

    private void Start()
    {
        if (layers.Length == 0)
            return;

        //The playable graph is a directed graph of Playables.
        graph = PlayableGraph.Create();

        // The AnimationPlayableOutput links the graph with an animator that plays the graph.
        // I think we can ditch the animator, but the documentation is kinda sparse!
	    AnimationPlayableOutput animOutput = AnimationPlayableOutput.Create(graph, $"{name}_animation_player", gameObject.EnsureComponent<Animator>());

        for (var i = 0; i < layers.Length; i++)
            layers[i].InitializeSelf(graph, i);

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
    /// Play a state, using the player's default transition. The state will immediately be the current played state. 
    /// </summary>
    /// <param name="state">Name of the state to play</param>
    /// <param name="layer">Layer the state should be played on</param>
	public void Play(string state, int layer = 0) 
	{
		AssertLayerInBounds(layer, state, "play a state");
		Play(state, defaultTransition, layer);
	}

    /// <summary>
    /// Play a state, using the player's default transition. The state will immediately be the current played state. 
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(int state, int layer = 0)
    {
        AssertLayerInBounds(layer, state, "play a state");
        Play(state, defaultTransition, layer);
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
    /// Gets the currently playing state. This is the last state you called Play on, and might not even have started blending in yet.
    /// </summary>
    /// <param name="layer">Layer to check in</param>
    public AnimationState GetCurrentPlayingState(int layer = 0)
    {
        AssertLayerInBounds(layer, "get the current playing state");
        var animationLayer = layers[layer];
        return animationLayer.states[animationLayer.currentPlayedState];
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
    
    /// <summary>
    /// Gets the index of a state from it's name.
    /// This method is used internally whenever you send in a string Play("Idle"), so it's recommended to cache the result
    /// of this method instead of sending in strings.
    /// </summary>
    public int GetStateIdxFromName(string state, int layer = 0)
    {
        int stateIdx = layers[layer].GetStateIdx(state);
        Debug.Assert(stateIdx != -1, $"Trying to get the state \"{state}\" on layer {layer}, but that doesn't exist!");
        return stateIdx;
    }

    [Conditional("UNITY_ASSERTIONS")]
    public void AssertLayerInBounds(int layer, string action)
    {
        Debug.Assert(layer >= 0 && layer < layers.Length,
                     $"Trying to {action} on an out of bounds layer! (layer {layer}, there are {layers.Length} layers!)",
                     gameObject);
    }

    [Conditional("UNITY_ASSERTIONS")]
    public void AssertLayerInBounds(int layer, int state, string action)
    {
        Debug.Assert(layer >= 0 && layer < layers.Length,
                     $"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers!)",
                     gameObject);
    }
	
	[Conditional("UNITY_ASSERTIONS")]
	public void AssertLayerInBounds(int layer, string state, string action)
	{
		Debug.Assert(layer >= 0 && layer < layers.Length,
			$"Trying to {action} on an out of bounds layer! (state {state} on layer {layer}, but there are {layers.Length} layers!)",
			gameObject);
	}
    
    [Conditional("UNITY_ASSERTIONS")]
    public void AssertTransitionDataFine(TransitionData transitionData)
    {
        if(transitionData.type == TransitionType.Curve)
            Debug.Assert(transitionData.curve != null, "Trying to transition using a curve, but the curve is null!");
    }

    [Conditional("UNITY_ASSERTIONS")]
    public void AssertStateInBounds(int layer, int state, string action)
    {
        Debug.Assert(state >= 0 && state < layers[layer].states.Count, 
            $"Trying to {action} on an out of bounds state! (state {state} on layer {layer}, but there are {layers[layer].states.Count} states on that layer!)",
            gameObject);
    }
}