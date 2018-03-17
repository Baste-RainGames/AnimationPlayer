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
                EditorUtilities.ObjectField(clip);
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
            if (!usedClipsNeedsUpdate)
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

            foreach (var state in animationPlayer.layers.SelectMany(layer => layer.states))
            {
                //@TODO: Use pattern matching when C# 7
                var type = state.GetType();
                if (type == typeof(SingleClip))
                {
                    var singleClipState = (SingleClip) state;
                    if (singleClipState.clip != null && !animationClipsUsed.Contains(singleClipState.clip))
                        animationClipsUsed.Add(singleClipState.clip);
                }
                else if (type == typeof(BlendTree1D))
                {
                    foreach (var clip in ((BlendTree1D) state).blendTree.Select(bte => bte.clip).Where(c => c != null))
                        if (!animationClipsUsed.Contains(clip))
                            animationClipsUsed.Add(clip);
                }
                else if (type == typeof(BlendTree2D))
                {
                    foreach (var clip in ((BlendTree2D) state).blendTree.Select(bte => bte.clip).Where(c => c != null))
                        if (!animationClipsUsed.Contains(clip))
                            animationClipsUsed.Add(clip);
                    break;
                }
                else
                {
                    Debug.LogError($"Unknown state type {type.Name}");
                }
            }

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

        private const string persistedFoldoutUsedClips = "APE_FO_UsedClips_";
        private const string persistedFoldoutUsedModels = "APE_FO_UsedModels_";
    }

}