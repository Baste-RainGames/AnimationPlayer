using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public static class AnimationTransitionDrawer
    {
        public static void DrawTransitions(AnimationPlayer animationPlayer, PersistedInt selectedLayer, PersistedInt selectedState,
                                           PersistedInt selectedToState,
                                           string[][] allStateNames)
        {
            var layer = animationPlayer.layers[selectedLayer];
            if (layer.states.Count == 0)
            {
                EditorGUILayout.LabelField("No states, can't define transitions");
                return;
            }

            EditorGUILayout.LabelField("Transitions from " + layer.states[selectedState].Name);

            EditorGUILayout.Space();

            EditorUtilities.DrawIndented(() =>
            {
                selectedToState.SetTo(EditorGUILayout.Popup("Transtion to state", selectedToState, allStateNames[selectedLayer]));
                selectedToState.SetTo(Mathf.Clamp(selectedToState, 0, layer.states.Count - 1));

                EditorGUILayout.Space();

                var transition = layer.transitions.Find(state => state.fromState == selectedState && state.toState == selectedToState);
                var fromStateName = layer.states[selectedState].Name;
                var toStateName = layer.states[selectedToState].Name;

                if (transition == null)
                {
                    EditorGUILayout.LabelField($"No ({fromStateName}->{toStateName}) transition defined!");
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button($"Create one!"))
                    {
                        Undo.RecordObject(animationPlayer, $"Add transition from {fromStateName} to {toStateName}");
                        layer.transitions.Add(
                            new StateTransition {fromState = selectedState, toState = selectedToState, transitionData = TransitionData.Linear(1f)});
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    Undo.RecordObject(animationPlayer, $"Edit of transition from  {fromStateName} to {toStateName}");
                    transition.transitionData = DrawTransitionData(transition.transitionData);

                    GUILayout.Space(20f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (EditorUtilities.AreYouSureButton("Clear transition", "Are you sure?",
                                                         "Clear_Transition_" + fromStateName + "_" + toStateName,
                                                         1f, GUILayout.Width(150f)))
                    {
                        Undo.RecordObject(animationPlayer, $"Clear transition from  {fromStateName} to {toStateName}");
                        layer.transitions.Remove(transition);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        public static TransitionData DrawTransitionData(TransitionData transitionData)
        {
            transitionData.type = (TransitionType) EditorGUILayout.EnumPopup("Type", transitionData.type);
            transitionData.duration = EditorGUILayout.FloatField("Duration", transitionData.duration);

            if (transitionData.type == TransitionType.Curve)
                transitionData.curve = EditorGUILayout.CurveField(transitionData.curve);

            return transitionData;
        }
    }
}