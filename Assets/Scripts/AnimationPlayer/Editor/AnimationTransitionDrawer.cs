using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public static class AnimationTransitionDrawer
    {
        public static void DrawTransitions(AnimationPlayer animationPlayer, int selectedLayer, int selectedStateIdx, PersistedInt selectedToStateIdx,
                                           string[] stateNamesInLayer)
        {
            var layer = animationPlayer.layers[selectedLayer];
            if (layer.states.Count == 0)
            {
                EditorGUILayout.LabelField("No states, can't define transitions");
                return;
            }

            var selectedState = layer.states[selectedStateIdx];
            var selectedToState = layer.states[selectedToStateIdx];
            var selectedTransition = layer.transitions.Find(t => t.FromState == selectedState && t.ToState == selectedToState);
            var fromStateName = selectedState.Name;
            var toStateName = selectedToState.Name;

            if (selectedTransition != null)
            {
                EditorGUILayout.LabelField($"Selected transition: From \"{fromStateName}\" to \"{toStateName}\"");
                EditorUtilities.RecordUndo(animationPlayer, $"Edit of transition from  {fromStateName} to {toStateName}");

                EditorUtilities.DrawIndented(() =>
                {
                    selectedTransition.transitionData = DrawTransitionData(selectedTransition.transitionData);
                    GUILayout.Space(20f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (EditorUtilities.AreYouSureButton("Clear transition", "Are you sure?",
                                                         "Clear_Transition_" + fromStateName + "_" + toStateName,
                                                         1f, GUILayout.Width(150f)))
                    {
                        EditorUtilities.RecordUndo(animationPlayer, $"Clear transition from  {fromStateName} to {toStateName}");
                        layer.transitions.Remove(selectedTransition);
                    }

                    EditorGUILayout.EndHorizontal();
                });
            }

            EditorUtilities.Splitter();

            var transitionsFromState = layer.transitions.Where(t => t.FromState == selectedState).ToList();
            if (transitionsFromState.Count == 0)
            {
                EditorGUILayout.LabelField($"No defined transitions from {fromStateName}");
            }
            else
            {
                EditorGUILayout.LabelField($"Transitions from {fromStateName}:");

                EditorGUILayout.Space();
                EditorUtilities.DrawHorizontal(() =>
                {
                    GUILayout.FlexibleSpace();
                    EditorUtilities.DrawVertical(() =>
                    {
                        EditorUtilities.DrawIndented(() =>
                        {
                            foreach (var transition in transitionsFromState)
                            {
                                EditorGUI.BeginDisabledGroup(transition == selectedTransition);
                                if (GUILayout.Button(transition.ToState.Name, GUILayout.MinWidth(100f)))
                                    selectedToStateIdx.SetTo(layer.states.IndexOf(transition.ToState));
                                EditorGUI.EndDisabledGroup();
                            }
                        });
                    });
                });
            }

            EditorGUILayout.Space();

            EditorUtilities.DrawHorizontal(() =>
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create new transition"))
                {
                    GenericMenu menu = new GenericMenu();
                    foreach (var state in stateNamesInLayer)
                    {
                        menu.AddItem(new GUIContent($"Transition from {fromStateName} to {state}"), false, () =>
                        {
                            EditorUtilities.RecordUndo(animationPlayer, $"Adding transition from {fromStateName} to {toStateName}");
                            var newState = new StateTransition
                            {
                                FromState = selectedState,
                                ToState = layer.states.Find(s => s.Name == state),
                                transitionData = TransitionData.Linear(.2f)
                            };
                            layer.transitions.Add(newState);
                            selectedToStateIdx.SetTo(layer.states.FindIndex(s => s.Name == state));
                        });
                    }

                    menu.ShowAsContext();
                }
            });
            
            
            EditorUtilities.Splitter();
            EditorGUILayout.LabelField("Default transition");
            EditorUtilities.RecordUndo(animationPlayer, "Change default transition", () =>
            {
                animationPlayer.defaultTransition = DrawTransitionData(animationPlayer.defaultTransition);
            });
        }

        private static TransitionData DrawTransitionData(TransitionData transitionData)
        {
            transitionData.type = (TransitionType) EditorGUILayout.EnumPopup("Type", transitionData.type);
            transitionData.duration = EditorGUILayout.FloatField("Duration", transitionData.duration);

            if (transitionData.type == TransitionType.Curve)
                transitionData.curve = EditorGUILayout.CurveField(transitionData.curve);

            return transitionData;
        }
    }
}