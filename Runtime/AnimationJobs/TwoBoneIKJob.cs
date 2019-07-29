/*
 * Base on https://github.com/Unity-Technologies/animation-jobs-samples/blob/master/Assets/animation-jobs-samples/Samples/Scripts/TwoBoneIK/TwoBoneIK.cs
 */

using UnityEngine;
using UnityEngine.Experimental.Animations;

namespace Animation_Player {
    public struct TwoBoneIKJob : IAnimationJob {
        private TransformSceneHandle  effector;
        private TransformStreamHandle top;
        private TransformStreamHandle mid;
        private TransformStreamHandle low;

        public void Setup(Animator animator, Transform topX, Transform midX, Transform lowX, Transform effectorX) {
            top = animator.BindStreamTransform(topX);
            mid = animator.BindStreamTransform(midX);
            low = animator.BindStreamTransform(lowX);

            effector = animator.BindSceneTransform(effectorX);
        }

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream) {
            Solve(stream, top, mid, low, effector);
        }

        /// <summary>
        /// Returns the angle needed between v1 and v2 so that their extremities are
        /// spaced with a specific length.
        /// </summary>
        /// <returns>The angle between v1 and v2.</returns>
        /// <param name="aLen">The desired length between the extremities of v1 and v2.</param>
        /// <param name="v1">First triangle edge.</param>
        /// <param name="v2">Second triangle edge.</param>
        private static float TriangleAngle(float aLen, Vector3 v1, Vector3 v2) {
            var aLen1 = v1.magnitude;
            var aLen2 = v2.magnitude;
            var c     = Mathf.Clamp((aLen1 * aLen1 + aLen2 * aLen2 - aLen * aLen) / (aLen1 * aLen2) / 2.0f, -1.0f, 1.0f);
            return Mathf.Acos(c);
        }

        private static void Solve(AnimationStream stream, TransformStreamHandle topHandle, TransformStreamHandle midHandle, TransformStreamHandle lowHandle,
                                  TransformSceneHandle effectorHandle) {
            var aRotation = topHandle.GetRotation(stream);
            var bRotation = midHandle.GetRotation(stream);
            var eRotation = effectorHandle.GetRotation(stream);

            var aPosition = topHandle.GetPosition(stream);
            var bPosition = midHandle.GetPosition(stream);
            var cPosition = lowHandle.GetPosition(stream);
            var ePosition = effectorHandle.GetPosition(stream);

            var ab = bPosition - aPosition;
            var bc = cPosition - bPosition;
            var ac = cPosition - aPosition;
            var ae = ePosition - aPosition;

            var abcAngle = TriangleAngle(ac.magnitude, ab, bc);
            var abeAngle = TriangleAngle(ae.magnitude, ab, bc);
            var angle    = (abcAngle - abeAngle) * Mathf.Rad2Deg;
            var axis     = Vector3.Cross(ab, bc).normalized;

            var fromToRotation = Quaternion.AngleAxis(angle, axis);

            var worldQ = fromToRotation * bRotation;
            midHandle.SetRotation(stream, worldQ);

            cPosition = lowHandle.GetPosition(stream);
            ac        = cPosition - aPosition;
            var fromTo = Quaternion.FromToRotation(ac, ae);
            topHandle.SetRotation(stream, fromTo * aRotation);

            lowHandle.SetRotation(stream, eRotation);
        }
    }
}