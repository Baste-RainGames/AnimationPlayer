using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    public class AnimationStatePreviewer
    {
        public bool IsShowingPreview { get; private set; }

        private readonly AnimationPlayer animationPlayer;
        private AnimationState previewedState;
        private PlayableGraph previewGraph;
        private PreviewMode previewMode;
        private float manualModeTime;
        private float automaticModeTime;

        public AnimationStatePreviewer(AnimationPlayer player)
        {
            animationPlayer = player;
        }

        public void DrawStatePreview(PersistedInt selectedLayer, PersistedInt selectedState)
        {
            var state = animationPlayer.layers[selectedLayer].states[selectedState];
            if (IsShowingPreview)
            {
                var changedState = state != previewedState;
                if (changedState) {
                    if (previewMode == PreviewMode.Manual) {
                        var currentTimeRelative = manualModeTime / previewedState.Duration;
                        if (float.IsNaN(currentTimeRelative)) //If the previewed state's duration is 0f, which is the case for empty/null clips
                            currentTimeRelative = 0f;
                        manualModeTime = currentTimeRelative * state.Duration;
                    }
                    
                    StopPreviewing();
                    StartPreviewing(state);
                }

                DrawAnimationStatePreview(state, changedState);
            }
            else if (GUILayout.Button("Start previewing state"))
            {
                StartPreviewing(state);
            }
        }

        public void StartPreviewing(AnimationState state)
        {
            IsShowingPreview = true;
            previewedState = state;

            previewGraph = PlayableGraph.Create();
            var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
            var animOutput = AnimationPlayableOutput.Create(previewGraph, "AnimationOutput", animator);

            animOutput.SetSourcePlayable(state.GeneratePlayable(previewGraph, new Dictionary<string, List<BlendTreeController1D>>(),
                                                                new Dictionary<string, List<BlendTreeController2D>>(),
                                                                new Dictionary<string, float>()));
            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            previewGraph.GetRootPlayable(0).SetTime(0);
            automaticModeTime = Time.time;
        }

        public void StopPreviewing()
        {
            IsShowingPreview = false;
            Cleanup();
        }

        private void DrawAnimationStatePreview(AnimationState previewedState, bool changedState)
        {
            EditorUtilities.Splitter();

            var oldPreviewMode = previewMode;

            EditorUtilities.DrawHorizontal(() =>
            {
                EditorGUILayout.LabelField("Preview mode");

                EditorGUI.BeginDisabledGroup(previewMode == PreviewMode.Automatic);
                if (GUILayout.Button("Automatic"))
                    previewMode = PreviewMode.Automatic;
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(previewMode == PreviewMode.Manual);
                if (GUILayout.Button("Manual"))
                    previewMode = PreviewMode.Manual;
                EditorGUI.EndDisabledGroup();
            });

            if (oldPreviewMode != previewMode) {
                if (previewMode == PreviewMode.Automatic)
                    automaticModeTime = Time.realtimeSinceStartup;
                else
                    manualModeTime = (float) previewGraph.GetRootPlayable(0).GetTime();
            }

            if (previewMode == PreviewMode.Manual) {
                var last = manualModeTime;
                manualModeTime = EditorGUILayout.Slider(manualModeTime, 0f, previewedState.Duration);
                if (manualModeTime != last || changedState) {
                    previewGraph.GetRootPlayable(0).SetTime(manualModeTime);
                    previewGraph.Evaluate();
                }
            }
            else
            {
                var currentTime = Time.realtimeSinceStartup;
                var deltaTime = currentTime - automaticModeTime;
                automaticModeTime = currentTime;

                previewGraph.Evaluate(deltaTime);
                var evaluatedTime = previewGraph.GetRootPlayable(0).GetTime();
                if (evaluatedTime > previewedState.Duration) {
                    evaluatedTime %= previewedState.Duration;
                    previewGraph.GetRootPlayable(0).SetTime(evaluatedTime);
                }

                EditorGUILayout.Slider((float) evaluatedTime, 0f, previewedState.Duration);
            }

            SceneView.RepaintAll();

            if (GUILayout.Button("Stop preview"))
            {
                StopPreviewing();
            }
        }

        public enum PreviewMode
        {
            Automatic,
            Manual,
        }

        public void Cleanup()
        {
            if (previewGraph.IsValid())
                previewGraph.Destroy();
            if (animationPlayer == null) //Happens when entering play mode with the animationplayer selected
                return;
            
            //Reset the object to the bind pose. Only way I've found is to play an empty clip for a single frame.
            var resetGraph = PlayableGraph.Create();
            var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
            var animOutput = AnimationPlayableOutput.Create(resetGraph, "Cleanup Graph", animator);
            animOutput.SetSourcePlayable(AnimationClipPlayable.Create(resetGraph, new AnimationClip()));
            resetGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            resetGraph.GetRootPlayable(0).SetTime(0);
            resetGraph.Evaluate();
            resetGraph.Destroy();
        }
    }
}