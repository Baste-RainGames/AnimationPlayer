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
        private readonly List<BlendVarController> blendVarControllers = new List<BlendVarController>();
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
            blendVarControllers.Clear();
            Dictionary<string, float> blendVars = new Dictionary<string, float>();
            previewedPlayableMixer = AnimationMixerPlayable.Create(previewGraph, 1);
            previewedPlayable = state.Initialize(previewGraph, previewControllers1D, previewControllers2D, all2DControllers , blendVars, null);
            previewedPlayableMixer.AddInput(previewedPlayable, 0, 1f);
            animOutput.SetSourcePlayable(previewedPlayableMixer);
            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            previewGraph.GetRootPlayable(0).SetTime(0);
            previewGraph.GetRootPlayable(0).SetPropagateSetTime(true);
            automaticModeTime = Time.time;

            foreach (var blendVar in blendVars.Keys)
            {
                var controller = new BlendVarController(blendVar);
                foreach (var kvp in previewControllers1D.Where(kvp => kvp.Key == blendVar))
                    controller.AddControllers(kvp.Value);
                foreach (var kvp in previewControllers2D.Where(kvp => kvp.Key == blendVar))
                    controller.AddControllers(kvp.Value);

                blendVarControllers.Add(controller);
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
                if (evaluatedTime > previewedState.Duration) {
                    evaluatedTime %= previewedState.Duration;
                    previewedPlayable.SetTime(evaluatedTime);
                }
                else if (evaluatedTime < 0 && previewedState.speed < 0) {
                    evaluatedTime = previewedState.Duration + evaluatedTime;
                    previewedPlayable.SetTime(evaluatedTime);
                }

                var oldTime = (float) evaluatedTime;
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

            if (blendVarControllers.Count > 0)
            {
                EditorUtilities.Splitter();
                EditorGUILayout.LabelField($"Blend vars for {previewedState.Name}");
                foreach (var controller in blendVarControllers)
                {
                    var label = controller.BlendVar;
                    var oldVal = controller.GetBlendVar();
                    var newVal = EditorGUILayout.Slider(label, oldVal, controller.MinValue, controller.MaxValue);
                    if (oldVal != newVal)
                    {
                        controller.SetBlendVar(newVal);
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
                    clip = singleClip.assignedClip;
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