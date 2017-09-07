using System;
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
            rootPlayable = layers[0].layerMixer;
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
        int stateIdx = layers[layer].GetStateIdx(state);
        
        if(stateIdx == -1) {
            Debug.LogError($"AnimationPlayer asked to play state {state} on layer {layer}, but that doesn't exist!", gameObject);
            return;
        }
        SnapTo(stateIdx, layer);
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
		int stateIdx = layers[layer].GetStateIdx(state);
        
		if(stateIdx == -1) {
			Debug.LogError($"AnimationPlayer asked to play state {state} on layer {layer}, but that doesn't exist!", gameObject);
			return;
		}
		Play(stateIdx, defaultTransition, layer);
	}

    /// <summary>
    /// Play a state, using the player's default transition. The state will immediately be the current played state. 
    /// </summary>
    /// <param name="state">state index to play</param>
    /// <param name="layer">Layer the state should be played on</param>
    public void Play(int state, int layer = 0)
    {
        AssertLayerInBounds(layer, state, "play a state");
        layers[layer].PlayUsingInternalTransition(state, defaultTransition);
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
        layers[layer].PlayUsingExternalTransition(state, transitionData);
    }

    public float GetStateWeight(int state, int layer = 0)
    {
        AssertLayerInBounds(layer, state, "get a state weight");
        return layers[layer].GetStateWeight(state);
    }

    public AnimationState GetCurrentPlayingState(int layer = 0)
    {
        AssertLayerInBounds(layer, "get the current playing state");
        var animationLayer = layers[layer];
        return animationLayer.states[animationLayer.currentPlayedState];
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
}