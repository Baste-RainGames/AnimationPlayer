using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestScript : MonoBehaviour
{

    public List<int> ints;

}

#if UNITY_EDITOR
[CustomEditor(typeof(TestScript))]
public class TestScriptEditor : Editor
{

    private TestScript script;

    private Queue<Action> addActions = new Queue<Action>();

    void OnEnable()
    {
        script = (TestScript) target;
    }

    public override void OnInspectorGUI()
    {
        if (Event.current.type == EventType.Layout)
        {
            while (addActions.Count > 0)
            {
                Undo.RecordObject(script, "Adding actions");
                var action = addActions.Dequeue();
                action();
            }
        }

        base.OnInspectorGUI();

        if (GUILayout.Button("Add some ints"))
        {
            for (int i = 0; i < Random.Range(1, 4); i++)
            {
                addActions.Enqueue(() =>
                {
                    script.ints.Add(Random.Range(-20, 20));
                });
            }
        }
    }

}
#endif