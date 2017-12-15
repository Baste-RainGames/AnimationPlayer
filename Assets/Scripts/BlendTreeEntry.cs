using System;
using UnityEngine;

namespace Animation_Player
{
    public class BlendTreeEntry
    {
        public AnimationClip clip;
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