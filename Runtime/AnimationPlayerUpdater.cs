using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Profiling;

namespace Animation_Player
{
public static class AnimationPlayerUpdater
{
    private static List<AnimationPlayer> animationPlayers = new ();

    [RuntimeInitializeOnLoadMethod]
    public static void Initialize()
    {
#if UNITY_2019_3_OR_NEWER
        PlayerLoopInterface.InsertSystemAfter(typeof(AnimationPlayerUpdater), Update, typeof(UnityEngine.PlayerLoop.Update));
#else
            PlayerLoopInterface.InsertSystemAfter(typeof(AnimationPlayerUpdater), Update, typeof(UnityEngine.Experimental.PlayerLoop.Update));
#endif
    }

    private static void Update()
    {
        Profiler.BeginSample("Animation Player Update");
        foreach (var animationPlayer in animationPlayers)
            animationPlayer.UpdateSelf();
        Profiler.EndSample();
    }

    internal static void RegisterAnimationPlayer(AnimationPlayer animationPlayer)
    {
        animationPlayers.Add(animationPlayer);
    }

    internal static void DeregisterAnimationPlayer(AnimationPlayer animationPlayer)
    {
        animationPlayers.Remove(animationPlayer);
    }
}
}