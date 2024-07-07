using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.Playables;

namespace Animation_Player
{
public static class AnimationPlayerEditorUtility
{
    public static void ShowModelAsPoseInAnimation(AnimationPlayer animationPlayer, string state, int layer = 0)
    {
        animationPlayer.EnterPreview();
        animationPlayer.Play(state, layer);
        var previewGraph = animationPlayer.Graph;
        previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        previewGraph.Evaluate(0f);

        var data = animationPlayer.GetComponentsInChildren<Transform>().Select(t => (t, t.localPosition, t.localRotation, t.localScale));
        animationPlayer.ExitPreview();

        foreach (var (transform, localPos, localRot, localScale) in data)
        {
            Undo.RecordObject(transform, "Showing transform at position");
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            transform.localScale = localScale;
        }

    }
}
}