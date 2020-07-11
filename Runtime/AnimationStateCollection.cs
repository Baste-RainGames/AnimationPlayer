using System.Collections.Generic;
using UnityEngine;

namespace Animation_Player
{
    [CreateAssetMenu(menuName = "Mesmer/Animation State Collection", order = 78)]
    public class AnimationStateCollection : ScriptableObject
    {

        public List<AnimationStateWrapper> animStates;
        private string[] displayNames;

        public string[] GetDisplayNames()
        {
            if (displayNames == null || displayNames.Length != animStates.Count)
                displayNames = new string[animStates.Count];

            for (int i = 0; i < animStates.Count; i++)
            {
                if (animStates[i] == null)
                    displayNames[i] = "[NULL]";
                else
                    displayNames[i] = animStates[i].GetState().Name;
            }
            return displayNames;
        }

        public int GetStateIndexInCollection(AnimationStateWrapper state)
        {
            for (int i = 0; i < animStates.Count; i++)
            {
                if (animStates[i] == state)
                {
                    return i;
                }
            }
            return -1;
        }

        public AnimationStateWrapper GetAnimationState(int index)
        {
            if (animStates.Count == 0 || index >= animStates.Count)
            {
                Debug.LogError("Error: Tried getting invalid animation state");
            }
            return animStates[index];
        }
    }
}