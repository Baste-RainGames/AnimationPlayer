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
        private AnimationState currentPreviewedState;
        private PlayableGraph previewGraph;
        private PreviewMode previewMode;
        private float previewTime;
        private float automaticPreviewLastTime;

        public AnimationStatePreviewer(AnimationPlayer player)
        {
            animationPlayer = player;
        }
        
        public void DrawStatePreview(PersistedInt selectedLayer, PersistedInt selectedState)
        {
            //@TODO: handle changing state somehow
            
            if (IsShowingPreview)
            {
                DrawAnimationStatePreview();
            }
            else if (GUILayout.Button("Start previewing state"))
            {
                var state = animationPlayer.layers[selectedLayer].states[selectedState];
                StartPreviewing(state);
            }
        }

        public void StartPreviewing(AnimationState state)
        {
            IsShowingPreview = true;
            currentPreviewedState = state;

            previewGraph = PlayableGraph.Create();
            var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
            var animOutput = AnimationPlayableOutput.Create(previewGraph, "AnimationOutput", animator);

            animOutput.SetSourcePlayable(state.GeneratePlayable(previewGraph, new Dictionary<string, List<AnimationLayer.BlendTreeController1D>>(), 
                                                                              new Dictionary<string, List<AnimationLayer.BlendTreeController2D>>(), 
                                                                              new Dictionary<string, float>()));

            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            previewGraph.Play();
        }

        public void StopPreviewing()
        {
            IsShowingPreview = false;
            
            previewGraph.Destroy();
            
            animationPlayer.gameObject.GetComponent<Animator>().Update(0f);
        }


        private void DrawAnimationStatePreview()
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
            

            if (oldPreviewMode == PreviewMode.Manual && previewMode == PreviewMode.Automatic)
                automaticPreviewLastTime = Time.realtimeSinceStartup;

            if (previewMode == PreviewMode.Manual)
            {
                var oldPreviewTime = previewTime;
                previewTime = EditorGUILayout.Slider(previewTime, 0f, currentPreviewedState.Duration);
                
                previewGraph.Evaluate(previewTime - oldPreviewTime);
                
            }
            else
            {
                var currentTime = Time.realtimeSinceStartup;
                var deltaTime = currentTime - automaticPreviewLastTime;
                automaticPreviewLastTime = currentTime;

                previewTime = (previewTime + deltaTime) % currentPreviewedState.Duration;

                previewGraph.Evaluate(deltaTime);

                var oldVal = previewTime;
                previewTime = EditorGUILayout.Slider(previewTime, 0f, currentPreviewedState.Duration);
                if(oldVal != previewTime)
                    previewMode = PreviewMode.Manual;
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
            if(previewGraph.IsValid())
                previewGraph.Destroy();
        }
    }
}