using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RuntimePlayable : MonoBehaviour {

	public AnimationClip clipA;
	public AnimationClip clipB;
	
	private PlayableGraph graph;
	private BlenderPlayableBehaviour blendBehaviour;

	public void StartPlaying () {
		graph = PlayableGraph.Create();

		//ensureComponent adds the component if it can't be found
		var animOutput = AnimationPlayableOutput.Create(graph, "AnimationOutput", gameObject.EnsureComponent<Animator>());

		var mixerPlayable = AnimationMixerPlayable.Create(graph, 2, true);

		var clipPlayableA = AnimationClipPlayable.Create(graph, clipA);
		var clipPlayableB = AnimationClipPlayable.Create(graph, clipB);
		
		graph.Connect(clipPlayableA, 0, mixerPlayable, 0);
		graph.Connect(clipPlayableB, 0, mixerPlayable, 1);
		
		mixerPlayable.SetInputWeight(0, .5f);
		mixerPlayable.SetInputWeight(1, .5f);
		
		animOutput.SetSourcePlayable(mixerPlayable);

		graph.Play();
	}

	public void Tick()
	{
		graph.Evaluate(.1f);
	}

	private void OnDestroy()
	{
		graph.Destroy();
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(RuntimePlayable))]
public class RuntimePlayableEditor : Editor {

	private RuntimePlayable script;

	void OnEnable() {
		script = (RuntimePlayable) target;
	}

	public override void OnInspectorGUI() {
		base.OnInspectorGUI();

		if (GUILayout.Button("Start"))
		{
			script.StartPlaying();
		}

		if (GUILayout.Button("Tick"))
		{
			script.Tick();
		}
	}

}
#endif