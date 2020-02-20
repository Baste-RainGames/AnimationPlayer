using System.Collections.Generic;
using System.Linq;
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
        private AnimationPlayerState previewedState;
        private Playable previewedPlayable;
        private AnimationMixerPlayable previewedPlayableMixer;
        private PlayableGraph previewGraph;
        private PreviewMode previewMode;
        private float manualModeTime;
        private float automaticModeTime;

        private readonly Dictionary<string, List<BlendTreeController1D>> previewControllers1D = new Dictionary<string, List<BlendTreeController1D>>();
        private readonly Dictionary<string, List<BlendTreeController2D>> previewControllers2D = new Dictionary<string, List<BlendTreeController2D>>();
        private readonly List<BlendTreeController2D> all2DControllers = new List<BlendTreeController2D>();

        private (string blendVar, float min, float max, float current)[] blendVars;

        private bool swapToManual;

        public AnimationStatePreviewer(AnimationPlayer player)
        {
            animationPlayer = player;
        }

        public void DrawStatePreview(int selectedLayer, int selectedState)
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

        public void StartPreviewing(AnimationPlayerState state)
        {
            IsShowingPreview = true;
            previewedState = state;

            previewGraph = PlayableGraph.Create();
            var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
            var animOutput = AnimationPlayableOutput.Create(previewGraph, "AnimationOutput", animator);

            previewControllers1D.Clear();
            previewControllers2D.Clear();
            all2DControllers.Clear();
            previewedPlayableMixer = AnimationMixerPlayable.Create(previewGraph, 1);
            previewedPlayable = state.Initialize(previewGraph, previewControllers1D, previewControllers2D, all2DControllers, null);
            previewedPlayableMixer.AddInput(previewedPlayable, 0, 1f);
            animOutput.SetSourcePlayable(previewedPlayableMixer);
            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            previewGraph.GetRootPlayable(0).SetTime(0);
            previewGraph.GetRootPlayable(0).SetPropagateSetTime(true);
            automaticModeTime = Time.time;

            if (state is BlendTree1D bt1d) {
                var min = bt1d.blendTree.Min(entry => entry.threshold);
                var max = bt1d.blendTree.Max(entry => entry.threshold);
                blendVars = new [] {
                    (bt1d.blendVariable, min, max, Mathf.Clamp(0, min, max))
                };
            }
            else if (state is BlendTree2D bt2d) {
                var min1 = bt2d.blendTree.Min(entry => entry.threshold1);
                var max1 = bt2d.blendTree.Max(entry => entry.threshold1);
                var min2 = bt2d.blendTree.Min(entry => entry.threshold2);
                var max2 = bt2d.blendTree.Max(entry => entry.threshold2);

                blendVars = new [] {
                    (bt2d.blendVariable,  min1, max1, Mathf.Clamp(0, min1, max1)),
                    (bt2d.blendVariable2, min2, max2, Mathf.Clamp(0, min2, max2))
                };
            }
            else {
                blendVars = new (string blendVar, float min, float max, float current)[0];
            }
        }

        public void StopPreviewing()
        {
            IsShowingPreview = false;
            Cleanup();
        }

        private void DrawAnimationStatePreview(AnimationPlayerState previewedState, bool changedState)
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
                if (GUILayout.Button("Manual") || swapToManual)
                    previewMode = PreviewMode.Manual;
                EditorGUI.EndDisabledGroup();
            });

            swapToManual = false;

            if (oldPreviewMode != previewMode) {
                if (previewMode == PreviewMode.Automatic)
                    automaticModeTime = Time.realtimeSinceStartup;
                else
                    manualModeTime = (float) previewedPlayable.GetTime();
            }

            if (previewMode == PreviewMode.Manual) {
                var last = manualModeTime;
                manualModeTime = EditorGUILayout.Slider(manualModeTime, 0f, previewedState.Duration);
                if (manualModeTime != last || changedState)
                {
                    previewedPlayable.SetTime(manualModeTime);
                    previewGraph.Evaluate();
                    if (previewedState is Sequence sequence) {
                        sequence.ProgressThroughSequence(ref previewedPlayable);
                    }
                }
            }
            else
            {
                var currentTime = Time.realtimeSinceStartup;
                var deltaTime = currentTime - automaticModeTime;
                automaticModeTime = currentTime;

                previewGraph.Evaluate(deltaTime);
                if (previewedState is Sequence sequence) {
                    sequence.ProgressThroughSequence(ref previewedPlayable);
                }
                var evaluatedTime = previewedPlayable.GetTime();

                var oldTime = (float) (evaluatedTime % previewedState.Duration);
                var newTime = EditorGUILayout.Slider(oldTime, 0f, previewedState.Duration);
                if (newTime != oldTime)
                {
                    swapToManual = true;
                    previewedPlayable.SetTime(newTime);
                }
            }

            foreach (var controller2D in all2DControllers) {
                controller2D.Update();
            }

            for (int i = 0; i < previewedPlayable.GetInputCount(); i++)
            {
                var input = previewedPlayable.GetInput(i);
                var clipDuration = 10f;
                if (input.IsPlayableOfType<AnimationClipPlayable>()) {
                    clipDuration = ((AnimationClipPlayable) input).GetAnimationClip().length;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Slider(previewedPlayable.GetInputWeight(i), 0f, 1f);
                EditorGUILayout.Slider((float) input.GetTime(), 0f, clipDuration);
                EditorGUILayout.EndHorizontal();
            }

            if (blendVars.Length > 0)
            {
                EditorUtilities.Splitter();
                EditorGUILayout.LabelField($"Blend vars for {previewedState.Name}");
                for (var i = 0; i < blendVars.Length; i++) {
                    var (name, min, max, current) = blendVars[i];

                    var newVal = EditorGUILayout.Slider(name, current, min, max);

                    if (current != newVal) {
                        blendVars[i] = (name, min, max, newVal);
                        if (previewControllers1D.TryGetValue(name, out var controllersForVar1D))
                            foreach (var controller in controllersForVar1D)
                                controller.SetValue(newVal);
                        if (previewControllers2D.TryGetValue(name, out var controllersForVar2D))
                            foreach (var controller in controllersForVar2D)
                                controller.SetValue(name, newVal);
                    }
                }

                EditorUtilities.Splitter();
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
            if (!previewGraph.IsValid())
                return;

            previewGraph.Destroy();
            if (animationPlayer == null) //Happens when entering play mode with the animationplayer selected
                return;

            //Reset the object to the bind pose. Only way I've found is to play an empty clip for a single frame.
            var resetGraph = PlayableGraph.Create();
            try {
                var animator = animationPlayer.gameObject.EnsureComponent<Animator>();
                var animOutput = AnimationPlayableOutput.Create(resetGraph, "Cleanup Graph", animator);
                var state = animationPlayer.layers[0].states[0];

                AnimationClip clip;
                if (state is BlendTree1D blendTree1D)
                    clip = blendTree1D.blendTree[0].clip;
                else if (state is BlendTree2D blendTree2D)
                    clip = blendTree2D.blendTree[0].clip;
                else if (state is PlayRandomClip randomClip)
                    clip = randomClip.clips[0];
                else if (state is SingleClip singleClip)
                    clip = singleClip.clip;
                else
                    throw new System.Exception("Unknown type");

                // A solution where we play an empty clip worked ay one point, but broke. I really just want to get the model into the bind pose,
                // but Unity really resists that idea.
                var clipPlayable = AnimationClipPlayable.Create(resetGraph, clip);
                clipPlayable.SetApplyFootIK(false);
                animOutput.SetSourcePlayable(clipPlayable);
                resetGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                resetGraph.GetRootPlayable(0).SetTime(0);
                resetGraph.Evaluate();
            }
            catch { }
            finally {
                resetGraph.Destroy();
            }
        }
    }
}