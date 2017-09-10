using System;
using System.Collections.Generic;
using System.Security.Policy;
using UnityEngine;

[Serializable]
//@TODO: This should be done with inheritance and custom serialization, because we now end up with single-clip states having a lot of garbage data around,
// and anso BlendTreeEntries having 2D data even when it's a 1D state
public class AnimationState
{
    public const string DefaultSingleClipName = "New State";
    public const string Default1DBlendTreeName = "New Blend Tree";
    public const string Default2DBlendTreeName = "New 2D Blend Tree";

    [SerializeField]
    private string name;
    [SerializeField]
    private bool hasUpdatedName;
    public double speed;
    public AnimationClip clip;

    public AnimationStateType type;

    public string blendVariable;
    public string blendVariable2;
    public List<BlendTreeEntry> blendTree;

    public string Name
    {
        get { return name; }
        set
        {
            if (name == value)
                return;

            hasUpdatedName = true;
            name = value;
        }
    }

    public static AnimationState SingleClip(string name, AnimationClip clip = null)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.SingleClip,
            hasUpdatedName = !name.StartsWith(DefaultSingleClipName),
            clip = clip
        };
    }

    public static AnimationState BlendTree1D(string name)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.BlendTree1D,
            blendVariable = "blend",
            blendTree = new List<BlendTreeEntry>(),
            hasUpdatedName = !name.StartsWith(Default1DBlendTreeName)
        };
    }

    public static AnimationState BlendTree2D(string name)
    {
        return new AnimationState
        {
            name = name,
            speed = 1d,
            type = AnimationStateType.BlendTree2D,
            blendVariable = "blend1",
            blendVariable2 = "blend1",
            blendTree = new List<BlendTreeEntry>(),
            hasUpdatedName = !name.StartsWith(Default1DBlendTreeName)
        };
    }

    public void OnClipAssigned()
    {
        if (type != AnimationStateType.SingleClip)
            return;

        if (string.IsNullOrEmpty(name))
            name = clip.name;
        else if (!hasUpdatedName)
            name = clip.name;
    }
	
	public float Duration {
		get {
			if(type == AnimationStateType.SingleClip)
				return clip?.length ?? 0f;
			else {
			    ////@TODO: fix this
				Debug.LogError("Length for blend trees not implemented yet!");
				return 0f;
			}
		}
	}

    public override string ToString()
    {
        return $"{name} ({type})";
    }

}

[Serializable]
public class BlendTreeEntry
{
    public float threshold;
    public float threshold2;
    public AnimationClip clip;
}

public enum AnimationStateType
{
    SingleClip,
    BlendTree1D,
    BlendTree2D
}