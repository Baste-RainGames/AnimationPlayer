using UnityEngine;
using UnityEditor;

namespace Animation_Player {
    [CustomEditor(typeof(AnimationStateWrapper))]
    public class AnimationStateWrapperEditor : Editor {
        private AnimationStateWrapper animationStateWrapper;
        private AnimationStateWrapper.Type currentType;

        private void OnEnable() {
            animationStateWrapper = (AnimationStateWrapper) target;
            currentType = animationStateWrapper.type;
        }

        public override void OnInspectorGUI() {
            animationStateWrapper.type = (AnimationStateWrapper.Type) EditorGUILayout.EnumPopup(animationStateWrapper.type);
            if (currentType != animationStateWrapper.type) {
                ResetType(currentType);
                currentType = animationStateWrapper.type;
            }

            GUILayout.Space(10f);
            DrawCurrentSelectedState(currentType);
        }

        private void DrawCurrentSelectedState(AnimationStateWrapper.Type type) {
            bool markDirty = false;
            StateDataDrawer.ReloadCheck();
            AnimationPlayerState selectedState = null;
            if (type == AnimationStateWrapper.Type.BlendTree1D)
                selectedState = animationStateWrapper.blendTree1D;
            else if (type == AnimationStateWrapper.Type.BlendTree2D)
                selectedState = animationStateWrapper.blendTree2D;
            else if (type == AnimationStateWrapper.Type.PlayRandomClip)
                selectedState = animationStateWrapper.playRandomClip;
            else if (type == AnimationStateWrapper.Type.SingleClip)
                selectedState = animationStateWrapper.singleClip;

            if (selectedState != null)
                StateDataDrawer.DrawStateData(selectedState, ref markDirty);

            if (markDirty)
                EditorUtility.SetDirty(animationStateWrapper);
        }

        private void ResetType(AnimationStateWrapper.Type type) {
            if (type == AnimationStateWrapper.Type.BlendTree1D)
                animationStateWrapper.blendTree1D = null;
            else if (type == AnimationStateWrapper.Type.BlendTree2D)
                animationStateWrapper.blendTree2D = null;
            else if (type == AnimationStateWrapper.Type.PlayRandomClip)
                animationStateWrapper.playRandomClip = null;
            else if (type == AnimationStateWrapper.Type.SingleClip)
                animationStateWrapper.singleClip = null;

            EditorUtility.SetDirty(animationStateWrapper);
        }
    }
}