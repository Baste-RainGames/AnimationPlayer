using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationPlayer))]
public class AnimationPlayerEditor : Editor
{

	private AnimationPlayer animationPlayer;
	private int selectedLayer;

	void OnEnable()
	{
		animationPlayer = (AnimationPlayer) target;
		animationPlayer.editTimeUpdateCallback -= Repaint;
		animationPlayer.editTimeUpdateCallback += Repaint;

		selectedLayer = 0;
	}
	
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
//		if(animationPlayer.baseLayer)
		
		
		if (!Application.isPlaying)
			return;

		for (int i = animationPlayer.GetClipCount() - 1; i >= 0; i--)
		{
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Snap to " + i))
			{
				animationPlayer.SnapTo(i);
			}
			if (GUILayout.Button("Blend to " + i + " over .5 secs"))
			{
				animationPlayer.Play(i, AnimTransition.Linear(.5f));
			}
			if (GUILayout.Button("Blend to " + i + " using default transition"))
			{
				animationPlayer.Play(i);
			}
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.LabelField("Playing clip " + animationPlayer.GetCurrentPlayingClip());
		for (int i = animationPlayer.GetClipCount() - 1; i >= 0; i--)
		{
			EditorGUILayout.LabelField("weigh for " + i + ": " + animationPlayer.GetClipWeight(i));
		}
	}
}