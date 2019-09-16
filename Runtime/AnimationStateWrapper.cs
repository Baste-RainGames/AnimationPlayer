using UnityEngine;

namespace Animation_Player {
    [CreateAssetMenu(menuName = "Mesmer/Animation State Object", order = 77)]
    public class AnimationStateWrapper : ScriptableObject {
        public enum Type {
            BlendTree1D,
            BlendTree2D,
            SingleClip,
            PlayRandomClip
        }

        //This is what selects what type to show
        public Type type;

        //Editor only shows the chosen one
        public BlendTree1D    blendTree1D;
        public BlendTree2D    blendTree2D;
        public SingleClip     singleClip;
        public PlayRandomClip playRandomClip;

        public AnimationPlayerState GetState() {
            switch (type) {
                case Type.BlendTree1D:
                    return blendTree1D;
                case Type.BlendTree2D:
                    return blendTree2D;
                case Type.SingleClip:
                    return singleClip;
                case Type.PlayRandomClip:
                    return playRandomClip;
                default:
                    Debug.LogError("ERROR: Animation state type error in Animation State Scriptable Object");
                    return null;
            }
        }
    }
}