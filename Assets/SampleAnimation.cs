using UnityEngine;
using UnityEditor;
using System.Collections;

public class SampleClipTool: EditorWindow {

	class Styles
	{
		public Styles()
		{
		}
	}
	static Styles s_Styles;

	protected GameObject go;
	protected AnimationClip animationClip;
	protected float time = 0.0f;
	protected bool lockSelection = false;
	protected bool animationMode = false;

	[MenuItem("Mecanim/SampleClip", false, 2000)]
	public static void DoWindow()
	{
		GetWindow<SampleClipTool>();
	}

	public void OnEnable()
	{		
	}

	public void OnSelectionChange()
	{
		if (!lockSelection)
		{
			go = Selection.activeGameObject;
			Repaint();
		}
	}

	public void OnGUI()
	{
		if (s_Styles == null)
			s_Styles = new Styles();

		if (go == null)
		{
			EditorGUILayout.HelpBox("Please select a GO", MessageType.Info);
			return;
		}

		GUILayout.BeginHorizontal(EditorStyles.toolbar);

		EditorGUI.BeginChangeCheck();
		GUILayout.Toggle(AnimationMode.InAnimationMode(), "Animate", EditorStyles.toolbarButton);
		if (EditorGUI.EndChangeCheck())
			ToggleAnimationMode();

		GUILayout.FlexibleSpace();
		lockSelection = GUILayout.Toggle(lockSelection, "Lock", EditorStyles.toolbarButton);
		GUILayout.EndHorizontal();
		
		EditorGUILayout.BeginVertical();
		animationClip = EditorGUILayout.ObjectField(animationClip, typeof(AnimationClip), false) as AnimationClip;
		if (animationClip != null)
		{
			float startTime = 0.0f;
			float stopTime  = animationClip.length;
			time = EditorGUILayout.Slider(time, startTime, stopTime);
		}
		else if (AnimationMode.InAnimationMode())
			AnimationMode.StopAnimationMode();


		EditorGUILayout.EndVertical();
	}

	void Update()
	{
		if (go == null)
			return;

		if (animationClip == null)
			return;

//		// there is a bug in AnimationMode.SampleAnimationClip which crash unity if there is no valid controller attached
//		Animator animator = go.GetComponent<Animator>();
//		if (animator != null && animator.runtimeAnimatorController == null)
//			return;

		if (!EditorApplication.isPlaying && AnimationMode.InAnimationMode())
		{
			AnimationMode.BeginSampling();
			AnimationMode.SampleAnimationClip(go, animationClip, time);
			AnimationMode.EndSampling();

			SceneView.RepaintAll();
		}
	}

	void ToggleAnimationMode()
	{
		if(AnimationMode.InAnimationMode())
			AnimationMode.StopAnimationMode();
		else
			AnimationMode.StartAnimationMode();
	}
}
