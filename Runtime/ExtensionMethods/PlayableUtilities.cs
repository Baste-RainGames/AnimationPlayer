using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    public static class PlayableUtilities
    {
        /// <summary>
        /// Use this to replace the clip played by an AnimationClipPlayable in a PlayableGraph.
        /// The method creates a clone of the playable, with the new clip, and then swaps that in all the
        /// places that took the old playable as an input.
        /// </summary>
        /// <param name="playable">Playable to replace.</param>
        /// <param name="clip">Clip the new playable should play.</param>
        public static void ReplaceClipInPlace(ref AnimationClipPlayable playable, AnimationClip clip)
        {
            var newPlayable = AnimationClipPlayable.Create(playable.GetGraph(), clip);

            newPlayable.SetApplyFootIK(playable.GetApplyFootIK());
            newPlayable.SetApplyPlayableIK(playable.GetApplyPlayableIK());
            newPlayable.SetSpeed(playable.GetSpeed());

            var outputCount = playable.GetOutputCount();
            for (int i = 0; i < outputCount; i++)
            {
                var outputTarget = playable.GetOutput(i);

                var inputIndex = -1;
                var inputCount = outputTarget.GetInputCount();

                for (int j = 0; j < inputCount; j++)
                {
                    if (outputTarget.GetInput(j).Equals(playable))
                    {
                        inputIndex = j;
                        break;
                    }
                }

                var oldWeight = outputTarget.GetInputWeight(inputIndex);
                outputTarget.DisconnectInput(inputIndex);
                outputTarget.ConnectInput(inputIndex, newPlayable, i);
                outputTarget.SetInputWeight(inputIndex, oldWeight);
            }

            playable.Destroy();
            playable = newPlayable;
        }
    }
}