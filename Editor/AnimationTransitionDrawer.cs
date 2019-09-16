using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public static class AnimationTransitionDrawer
    {
        public static void DrawTransitions(AnimationPlayer animationPlayer, int selectedLayer, int selectedStateIdx,
                                           PersistedShortPair selectedToStateAndTransition, string[] stateNamesInLayer)
        {
            var layer = animationPlayer.layers[selectedLayer];
            if (layer.states.Count == 0)
            {
                EditorGUILayout.LabelField("No states, can't define transitions");
                return;
            }

            var (toStateIndex, transitionIndex) = selectedToStateAndTransition;

            var selectedState      = layer.states[selectedStateIdx];
            var selectedToState    = layer.states.GetIfInBounds(toStateIndex);
            var allTransitionsBetweenStates = GetTransitionsBetweenStates(layer, selectedState, selectedToState);

            var selectedTransition = allTransitionsBetweenStates.GetIfInBounds(transitionIndex);
            var fromStateName      = selectedState.Name;
            var toStateName        = selectedToState.Name;

            void SetTransition(StateTransition transition) {
                var newStateIndex = layer.states.IndexOf(transition.ToState);
                var newTransitionIndex = GetTransitionsBetweenStates(layer, selectedState, transition.ToState).IndexOf(transition);

                selectedToStateAndTransition.SetTo(((short) newStateIndex, (short) newTransitionIndex));
            }

            EditorUtilities.Splitter();
            DrawTransitionSelection(layer, selectedState, fromStateName, selectedTransition, SetTransition);
            DrawCreateNewTransition(animationPlayer, SetTransition, stateNamesInLayer, fromStateName, toStateName, selectedState, layer);

            EditorUtilities.Splitter();
            DrawSelectedTransition(animationPlayer, selectedTransition, fromStateName, toStateName, layer, transitionsFromState);

            EditorUtilities.Splitter();
            DrawDefaultTransition(animationPlayer);
        }

        private static List<StateTransition> GetTransitionsBetweenStates(AnimationLayer layer, AnimationPlayerState selectedState, AnimationPlayerState selectedToState) {
            var allTransitionsBetweenStates = new List<StateTransition>();
            foreach (var transition in layer.transitions)
                if (transition.FromState == selectedState && transition.ToState == selectedToState)
                    allTransitionsBetweenStates.Add(transition);
            return allTransitionsBetweenStates;
        }

        private static void DrawCreateNewTransition(AnimationPlayer animationPlayer, Action<StateTransition> SetSelectedTransition, string[] stateNamesInLayer,
                                                    string fromStateName, string toStateName, AnimationPlayerState selectedState, AnimationLayer layer) {
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button($"Create new transition from {fromStateName}")) {
                    GenericMenu menu = new GenericMenu();
                    foreach (var state in stateNamesInLayer) {
                        menu.AddItem(new GUIContent($"Transition from {fromStateName} to {state}"), false, () => {
                            //@TODO: If this is the first transition between the states, it should be set as default.
                            EditorUtilities.RecordUndo(animationPlayer, $"Adding transition from {fromStateName} to {toStateName}");
                            var newState = new StateTransition {
                                FromState      = selectedState,
                                ToState        = layer.states.Find(s => s.Name == state),
                                transitionData = TransitionData.Linear(.2f)
                            };
                            layer.transitions.Add(newState);
                            SetSelectedTransition(newState);
                        });
                    }
                    menu.ShowAsContext();
                }
            }
        }

        private static readonly List<StateTransition> transitionsFromState = new List<StateTransition>();
        private static void DrawTransitionSelection(AnimationLayer layer, AnimationPlayerState selectedState, string fromStateName,
                                                    StateTransition selectedTransition, Action<StateTransition> SetSelectedTransition)
        {
            transitionsFromState.Clear();
            foreach (var transition in layer.transitions)
                if (transition.FromState == selectedState)
                    transitionsFromState.Add(transition);

            transitionsFromState.Sort((t1, t2) => t1.ToState.Name.CompareTo(t2.ToState.Name)); //@TODO: Grab hold of NaturalComparrison from Mesmer, use it, so state_2 doesn't sort under state_11

            if (transitionsFromState.Count == 0)
            {
                EditorGUILayout.LabelField($"No defined transitions from {fromStateName}");
            }
            else
            {
                EditorGUILayout.LabelField($"Transitions from {fromStateName}:");

                var buttonWidth = 100f;
                foreach (var transition in transitionsFromState) {
                    var width = GUI.skin.button.CalcSize(new GUIContent(transition.name)).x + 20f;
                    buttonWidth = Mathf.Max(width, buttonWidth);
                }

                EditorGUILayout.Space();
                AnimationPlayerState lastToState = null;

                EditorUtilities.DrawIndented(() => {
                    for (var i = 0; i < transitionsFromState.Count; i++)
                    {
                        var transition = transitionsFromState[i];
                        if (transition.ToState != lastToState)
                        {
                            lastToState = transition.ToState;
                            EditorGUILayout.LabelField($"Transitions to {lastToState.Name}");
                        }

                        using (new GUILayout.HorizontalScope())
                        using (new EditorGUI.DisabledScope(transition == selectedTransition)) {
                            if (GUILayout.Button(transition.name, GUILayout.Width(buttonWidth)))
                                SetSelectedTransition(transition);

                            if (transition.isDefault)
                                EditorGUILayout.LabelField("(Default)");
                        }
                    }
                });
            }
        }


        private static void DrawSelectedTransition(AnimationPlayer animationPlayer, StateTransition selectedTransition, string fromStateName,
                                                   string toStateName, AnimationLayer layer, List<StateTransition> allTransitionsFromState)
        {
            if (selectedTransition != null)
            {
                EditorGUILayout.LabelField($"Selected transition: From \"{fromStateName}\" to \"{toStateName}\"");
                Undo.RecordObject(animationPlayer, $"Edit of transition from  {fromStateName} to {toStateName}");

                EditorUtilities.DrawIndented(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    selectedTransition.isDefault = EditorGUILayout.Toggle("Is Default", selectedTransition.isDefault);
                    if (EditorGUI.EndChangeCheck() && selectedTransition.isDefault)
                    {
                        foreach (var transition in allTransitionsFromState)
                        {
                            if (transition != selectedTransition && transition.ToState == selectedTransition.ToState)
                            {
                                transition.isDefault = false;
                            }
                        }
                    }

                    selectedTransition.name = EditorGUILayout.TextField("Name", selectedTransition.name);

                    selectedTransition.transitionData = DrawTransitionData(selectedTransition.transitionData);
                    GUILayout.Space(20f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (EditorUtilities.AreYouSureButton("Clear transition", "Are you sure?", $"Clear_Transition_{fromStateName}_{toStateName}", 1f, GUILayout.Width(150f))) {
                        Undo.RecordObject(animationPlayer, $"Clear transition from  {fromStateName} to {toStateName}");
                        layer.transitions.Remove(selectedTransition);
                    }

                    EditorGUILayout.EndHorizontal();
                });
            }
        }

        private static TransitionData DrawTransitionData(TransitionData transitionData)
        {
            transitionData.type = (TransitionType) EditorGUILayout.EnumPopup("Type", transitionData.type);

            if (transitionData.type == TransitionType.Clip)
            {
                EditorGUI.BeginChangeCheck();
                transitionData.clip = EditorUtilities.ObjectField("Clip", transitionData.clip);
                if (EditorGUI.EndChangeCheck())
                    transitionData.duration = transitionData.clip == null ? 0 : transitionData.clip.length;

                return transitionData;
            }

            transitionData.duration = EditorGUILayout.FloatField("Duration", transitionData.duration);

            if (transitionData.type == TransitionType.Curve)
                transitionData.curve = EditorGUILayout.CurveField(transitionData.curve);

            return transitionData;
        }

        private static void DrawDefaultTransition(AnimationPlayer animationPlayer)
        {
            EditorGUILayout.LabelField("Default transition for all states");
            Undo.RecordObject(animationPlayer, "Change default transition");
            animationPlayer.defaultTransition = DrawTransitionData(animationPlayer.defaultTransition);
        }
    }
}