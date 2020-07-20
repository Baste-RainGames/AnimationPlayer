using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Animation_Player
{
    public class MetaDataDrawer
    {
        private PersistedBool usedClipsFoldout;
        private PersistedBool usedModelsFoldout;
        private List<AnimationClip> animationClipsUsed;
        private Dictionary<AnimationClip, bool> clipUsagesFoldedOut;
        private Dictionary<AnimationClip, List<AnimationPlayerState>> clipsUsedInStates;
        private List<Object> modelsUsed;
        private AnimationPlayer animationPlayer;
        public bool usedClipsNeedsUpdate;

        public MetaDataDrawer(AnimationPlayer animationPlayer)
        {
            this.animationPlayer = animationPlayer;
            usedClipsFoldout = new PersistedBool(persistedFoldoutUsedClips + animationPlayer.GetInstanceID());
            usedModelsFoldout = new PersistedBool(persistedFoldoutUsedModels + animationPlayer.GetInstanceID());
        }

        public void DrawMetaData()
        {
            EditorGUI.indentLevel++;
            DrawClipsUsed();
            DrawReferencedModels();
            EditorGUI.indentLevel--;
        }

        private void DrawClipsUsed()
        {
            EnsureUsedClipsCached();

            usedClipsFoldout.SetTo(EditorGUILayout.Foldout(usedClipsFoldout, "Clips used in the animation player"));
            if (!usedClipsFoldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var clip in animationClipsUsed)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorUtilities.ObjectField(clip);
                    clipUsagesFoldedOut[clip] = EditorGUILayout.Foldout(clipUsagesFoldedOut[clip], "");
                }

                if (clipUsagesFoldedOut[clip])
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var state in clipsUsedInStates[clip])
                        {
                            EditorGUILayout.LabelField("Used in state " + state.Name);
                        }
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawReferencedModels()
        {
            usedModelsFoldout.SetTo(EditorGUILayout.Foldout(usedModelsFoldout, "Models used in the animation player"));
            if (!usedModelsFoldout)
                return;

            EditorGUI.indentLevel++;
            foreach (var model in modelsUsed)
                EditorUtilities.ObjectField(model);
            EditorGUI.indentLevel--;
        }

        private void EnsureUsedClipsCached()
        {
            if (!usedClipsNeedsUpdate && animationClipsUsed != null)
                return;
            usedClipsNeedsUpdate = false;

            if (animationClipsUsed != null)
                animationClipsUsed.Clear();
            else
                animationClipsUsed = new List<AnimationClip>();

            if (modelsUsed != null)
                modelsUsed.Clear();
            else
                modelsUsed = new List<Object>();

            if (clipsUsedInStates != null)
                clipsUsedInStates.Clear();
            else
                clipsUsedInStates = new Dictionary<AnimationClip, List<AnimationPlayerState>>();

            foreach (var state in animationPlayer.layers.SelectMany(layer => layer.states))
            {
                foreach (var clip in GetClips(state).Where(c => c != null))
                {
                        animationClipsUsed.EnsureContains(clip);
                        if (!clipsUsedInStates.ContainsKey(clip))
                            clipsUsedInStates[clip] = new List<AnimationPlayerState>();
                        clipsUsedInStates[clip].Add(state);
                }
            }

            if (clipUsagesFoldedOut == null)
                clipUsagesFoldedOut = new Dictionary<AnimationClip, bool>();

            foreach (var clip in animationClipsUsed)
                if (!clipUsagesFoldedOut.ContainsKey(clip))
                    clipUsagesFoldedOut[clip] = false;

            List<string> usedAssetPaths = new List<string>();
            foreach (var animationClip in animationClipsUsed)
            {
                if (AssetDatabase.IsMainAsset(animationClip))
                    continue; //standalone animation clip

                var modelPath = AssetDatabase.GetAssetPath(animationClip);
                if (!usedAssetPaths.Contains(modelPath))
                    usedAssetPaths.Add(modelPath);
            }

            foreach (var modelPath in usedAssetPaths)
            {
                var model = AssetDatabase.LoadMainAssetAtPath(modelPath);
                modelsUsed.Add(model);
            }
        }

        private static IEnumerable<AnimationClip> GetClips(AnimationPlayerState state)
        {
            switch (state)
            {
                case BlendTree1D blendTree1D:
                    foreach (var clip in blendTree1D.entries.Select(entry => entry.clip))
                        yield return clip;
                    break;
                case BlendTree2D blendTree2D:
                    foreach (var clip in blendTree2D.blendTree.Select(entry => entry.clip))
                        yield return clip;
                    break;
                case PlayRandomClip playRandomClip:
                    foreach (var clip in playRandomClip.clips)
                        yield return clip;
                    break;
                case Sequence sequence:
                    foreach (var clip in sequence.clips)
                        yield return clip;
                    break;
                case SingleClip singleClip:
                    yield return singleClip.clip;
                    break;
            }
        }

        private const string persistedFoldoutUsedClips = "APE_FO_UsedClips_";
        private const string persistedFoldoutUsedModels = "APE_FO_UsedModels_";
    }

}