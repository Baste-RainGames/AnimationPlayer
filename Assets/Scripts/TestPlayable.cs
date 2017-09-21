using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class TestPlayable : MonoBehaviour
{
    public AnimationClip clipA;
    public AnimationClip clipB;
    public AnimationClip clipC;

    public AvatarMask mask;

    private PlayableGraph graph;

    void Start()
    {
        // Create the PlayableGraph.
        graph = PlayableGraph.Create();

        // Add an AnimationPlayableOutput to the graph.
        var animOutput = AnimationPlayableOutput.Create(graph, "AnimationOutput", gameObject.AddComponent<Animator>());

        // Add an AnimationMixerPlayable to the graph.
        var mixerPlayable = AnimationMixerPlayable.Create(graph, 3, false);

        // Add two AnimationClipPlayable to the graph.
        var clipPlayableA = AnimationClipPlayable.Create(graph, clipA);
        var clipPlayableB = AnimationClipPlayable.Create(graph, clipB);

        // Add a custom PlayableBehaviour to the graph.
        // This behavior will change the weights of the mixer dynamically.
        var blenderPlayable = ScriptPlayable<BlenderPlayableBehaviour>.Create(graph);
        blendBehaviour = blenderPlayable.GetBehaviour();
        blendBehaviour.mixerPlayable = mixerPlayable;

        // Create the topology, connect the AnimationClipPlayable to the
        // AnimationMixerPlayable.  Also add the BlenderPlayableBehaviour.
        graph.Connect(clipPlayableA, 0, mixerPlayable, 0);
        graph.Connect(clipPlayableB, 0, mixerPlayable, 1);
        graph.Connect(blenderPlayable, 0, mixerPlayable, 2);

        var clipPlayableC = AnimationClipPlayable.Create(graph, clipC);
        layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
        
        layerMixer.SetLayerMaskFromAvatarMask(1, mask);
        layerMixer.SetLayerAdditive(1, false);
        layerMixer.SetInputWeight(0, 1f);
        layerMixer.SetInputWeight(1, 0f);

        graph.Connect(mixerPlayable, 0, layerMixer, 0);
        graph.Connect(clipPlayableC, 0, layerMixer, 1);

        // Use the AnimationMixerPlayable as the source for the AnimationPlayableOutput.
        animOutput.SetSourcePlayable(layerMixer);

        // Play the graph.
        graph.Play();
    }

    private Coroutine c;
    private BlenderPlayableBehaviour blendBehaviour;
    public float debugBlendVal;
    private AnimationLayerMixerPlayable layerMixer;

    void Update()
    {
        if (c == null && Input.GetKeyDown(KeyCode.Space))
        {
            c = StartCoroutine(Blend());
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            layerMixer.SetInputWeight(1, 1f);
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            layerMixer.SetInputWeight(1, 0f);
        }
    }

    private IEnumerator Blend()
    {
        var start = blendBehaviour.blendVal;
        var end = start == 0f ? 1f : 0f;

        while (blendBehaviour.blendVal != end)
        {
            //1 sec blend
            blendBehaviour.blendVal = Mathf.MoveTowards(blendBehaviour.blendVal, end, Time.deltaTime * 2f);
            debugBlendVal = blendBehaviour.blendVal;
            yield return null;
        }

        c = null;
    }

    private void OnDestroy()
    {
        //Otherwise Unity complains about memory leaks!
        graph.Destroy(); 
    }
    
    
}