using System;
using System.Collections.Generic;
using UnityEngine;

namespace Animation_Player
{
    [Serializable]
    public class ClipSwapCollection
    {
        public string name;
        public List<ClipSwap> swaps = new List<ClipSwap>();

        [NonSerialized] internal bool active = false;

        public bool TryGetSwapFor(AnimationClip clip, out AnimationClip swappedClip)
        {
            if (active)
            {
                foreach (var swap in swaps)
                {
                    if (swap.swapFrom == clip)
                    {
                        swappedClip = swap.swapTo;
                        return true;
                    }
                }
            }

            swappedClip = null;
            return false;
        }
    }

    [Serializable]
    public class ClipSwap
    {
        public AnimationClip swapFrom;
        public AnimationClip swapTo;
    }
}