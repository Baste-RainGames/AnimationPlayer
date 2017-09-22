using System;
using System.Collections;
using System.Collections.Generic;
using Animation_Player;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestScript : MonoBehaviour
{

	[SerializeField, HideInInspector]
	public SerializedGUID serializedGuid;

}


#if UNITY_EDITOR
[CustomEditor(typeof(TestScript))]
public class TestScriptEditor : Editor {

	private TestScript script;

	void OnEnable() {
		script = (TestScript) target;
	}

	public override void OnInspectorGUI() {
		base.OnInspectorGUI();
		
		EditorGUILayout.LabelField(script.serializedGuid.GUID.ToString());
		
		if (GUILayout.Button("Generate GUID"))
		{
			Undo.RecordObject(script, "GUID");
			script.serializedGuid = SerializedGUID.Create();
		}		
	}

}
#endif