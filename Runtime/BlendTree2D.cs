using System;
using System.Collections.Generic;

using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
[Serializable]
public class BlendTree2D : AnimationPlayerState
{
    public const string DefaultName = "New 2D Blend Tree";

    public string blendVariable;
    public string blendVariable2;

    //@TODO: should we compensate for different durations like we do in the 1D blend tree?
    public List<BlendTreeEntry2D> entries;

    private BlendTree2D() { }

    public static BlendTree2D Create(string name)
    {
        var blendTree = new BlendTree2D();
        blendTree.Initialize(name, DefaultName);
        blendTree.blendVariable = "blend1";
        blendTree.blendVariable2 = "blend2";
        blendTree.entries = new List<BlendTreeEntry2D>();
        return blendTree;
    }

    public override Playable GeneratePlayable(PlayableGraph graph, Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers,
                                              Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers,
                                              List<BlendTreeController2D> all2DControllers)
    {
        var treeMixer = AnimationMixerPlayable.Create(graph, entries.Count);
        treeMixer.SetPropagateSetTime(true);

        if (entries.Count == 0)
            return treeMixer;

        var controller = new BlendTreeController2D(blendVariable, blendVariable2, treeMixer, entries.Count);
        all2DControllers.Add(controller);
        varTo2DBlendControllers.GetOrAdd(blendVariable).Add(controller);
        varTo2DBlendControllers.GetOrAdd(blendVariable2).Add(controller);

        for (int j = 0; j < entries.Count; j++)
        {
            var blendTreeEntry = entries[j];
            var clipPlayable = AnimationClipPlayable.Create(graph, GetClipToUseFor(blendTreeEntry.clip));
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetSpeed(speed);
            graph.Connect(clipPlayable, 0, treeMixer, j);

            controller.AddThresholdsForClip(j, blendTreeEntry.threshold1, blendTreeEntry.threshold2);
        }

        controller.OnAllThresholdsAdded();

        controller.SetInitialValues(0f, 0f);
        return treeMixer;
    }

    public override float Duration
    {
        get
        {
            var longest = 0f;
            foreach (var entry in entries)
            {
                var clip = GetClipToUseFor(entry.clip);
                var clipLength = clip == null ? 0f : clip.length;
                if (clipLength > longest)
                    longest = clipLength;
            }

            return longest;
        }
    }

    public override bool Loops
    {
        get
        {
            foreach (var entry in entries)
            {
                var clip = GetClipToUseFor(entry.clip);
                if (clip != null && clip.isLooping)
                    return true;
            }

            return false;
        }
    }

    public override void JumpToRelativeTime(ref Playable ownPlayable, float time)
    {
        var unNormalizedTime = time * Duration;
        ownPlayable.SetTime(unNormalizedTime);
        for (int i = 0; i < ownPlayable.GetInputCount(); i++)
        {
            ownPlayable.GetInput(i).SetTime(unNormalizedTime);
        }
    }

    public override void OnClipSwapsChanged(ref Playable ownPlayable)
    {
        var asMixer = (AnimationMixerPlayable) ownPlayable;
        var inputCount = asMixer.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            var clipPlayable = (AnimationClipPlayable) asMixer.GetInput(i);
            var shouldPlay = GetClipToUseFor(entries[i].clip);
            var isPlaying = clipPlayable.GetAnimationClip();

            if (isPlaying != shouldPlay)
                PlayableUtilities.ReplaceClipInPlace(ref clipPlayable, shouldPlay);
        }
    }

    public override void RegisterUsedBlendVarsIn(Dictionary<string, float> blendVariableValues)
    {
        if (!blendVariableValues.ContainsKey(blendVariable))
            blendVariableValues[blendVariable] = 0;
        if (!blendVariableValues.ContainsKey(blendVariable2))
            blendVariableValues[blendVariable2] = 0;
    }
}
}