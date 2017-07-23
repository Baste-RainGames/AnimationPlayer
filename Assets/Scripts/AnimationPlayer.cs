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
        //The playable graph is a directed graph of Playables.
        graph = PlayableGraph.Create();

        // The AnimationPlayableOutput links the graph with an animator that plays the graph.
        // I think we can ditch the animator, but the documentation is kinda sparse!
        AnimationPlayableOutput animOutput = AnimationPlayableOutput.Create(graph, $"{name}_animation_player", gameObject.AddComponent<Animator>());

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
        graph.Destroy();
    }

    /// <summary>
    /// Play a clip, using an instant transition. The clip will immediately be the current played clip. 
    /// </summary>
    /// <param name="clip">Clip index to play</param>
    /// <param name="layer">Layer the clip should be played on</param>
    public void SnapTo(int clip, int layer = 0)
    {
        Play(clip, TransitionData.Instant(), layer);
    }

    /// <summary>
    /// Play a clip, using the player's default transition. The clip will immediately be the current played clip. 
    /// </summary>
    /// <param name="clip">Clip index to play</param>
    /// <param name="layer">Layer the clip should be played on</param>
    public void Play(int clip, int layer = 0)
    {
        AssertLayerInBounds(layer, clip, "play a clip");
        layers[layer].PlayUsingInternalTransition(clip, defaultTransition);
    }

    /// <summary>
    /// Play a clip. The clip will immediately be the current played clip. 
    /// </summary>
    /// <param name="clip">Clip index to play</param>
    /// <param name="transitionData">How to transition into the clip</param>
    /// <param name="layer">Layer the clip should be played on</param>
    public void Play(int clip, TransitionData transitionData, int layer = 0)
    {
        AssertLayerInBounds(layer, clip, "play a clip");
        layers[layer].PlayUsingExternalTransition(clip, transitionData);
    }

    public float GetClipWeight(int clip, int layer = 0)
    {
        AssertLayerInBounds(layer, clip, "get a clip weight");
        return layers[layer].GetClipWeight(clip);
    }

    public int GetCurrentPlayingClip(int layer = 0)
    {
        AssertLayerInBounds(layer, "get the current playing clip");
        return layers[layer].currentPlayedClip;
    }

    public int GetStateCount(int layer = 0)
    {
        AssertLayerInBounds(layer, "get the clip count");
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
    public void AssertLayerInBounds(int layer, int clip, string action)
    {
        Debug.Assert(layer >= 0 && layer < layers.Length,
                     $"Trying to {action} on an out of bounds layer! (Clip {clip} on layer {layer}, but there are {layers.Length} layers!)",
                     gameObject);
    }
}