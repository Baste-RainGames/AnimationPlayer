using System;
using UnityEngine;

namespace Animation_Player
{
    public abstract class BlendTreeEntry
    {
        public AnimationClip clip;
        public float         Duration => clip != null ? clip.length : 0f;
    }

    [Serializable]
    public class BlendTreeEntry1D : BlendTreeEntry
    {
        public float threshold;
    }

    [Serializable]
    public class BlendTreeEntry2D : BlendTreeEntry
    {
        public float threshold1;
        public float threshold2;
    }
}