using System.Collections.Generic;
using UnityEngine;

namespace Animation_Player
{
    /// <summary>
    /// Instead of calling player.SetBlendVar("myVar", value), you can get a BlendVarController through player.GetBlendControllerFor("myVar"),
    /// which can be cached. This saves some internal Dictionary lookups, which speeds up things.
    /// </summary>
    public class BlendVarController
    {

        private readonly List<BlendTreeController1D> inner1D      = new List<BlendTreeController1D>();
        private readonly List<BlendTreeController2D> inner2D_set1 = new List<BlendTreeController2D>();
        private readonly List<BlendTreeController2D> inner2D_set2 = new List<BlendTreeController2D>();
        private readonly string blendVar;

        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }

        public BlendVarController(string blendVar)
        {
            this.blendVar = blendVar;
        }

        public int InnerControllerCount => inner1D.Count + inner2D_set1.Count + inner2D_set2.Count;

        public void AddControllers(List<BlendTreeController1D> blendControllers1D)
        {
            inner1D.AddRange(blendControllers1D);
            foreach (var blendController in blendControllers1D) {
                MinValue = Mathf.Min(blendController.GetMinThreshold(), MinValue);
                MaxValue = Mathf.Max(blendController.GetMaxThreshold(), MaxValue);
            }
        }

        public void AddControllers(List<BlendTreeController2D> blendControllers2D)
        {
            foreach (var controller2D in blendControllers2D)
            {
                if (controller2D.blendVar1 == blendVar) {
                    inner2D_set1.Add(controller2D);
                    MinValue = Mathf.Min(controller2D.GetMinValForVar1(), MinValue);
                    MaxValue = Mathf.Max(controller2D.GetMaxValForVar1(), MaxValue);
                }
                else {
                    inner2D_set2.Add(controller2D);
                    MinValue = Mathf.Min(controller2D.GetMinValForVar2(), MinValue);
                    MaxValue = Mathf.Max(controller2D.GetMaxValForVar2(), MaxValue);
                }
            }
        }

        public float GetBlendVar() {
            foreach (var controller1D in inner1D)
                return controller1D.CurrentValue;

            foreach (var controller2D in inner2D_set1)
                return controller2D.CurrentValue1;

            foreach (var controller2D in inner2D_set2)
                return controller2D.CurrentValue2;

            return 0f; //error?
        }

        public void SetBlendVar(float value)
        {
            foreach (var controller1D in inner1D)
            {
                controller1D.SetValue(value);
            }

            foreach (var controller2D in inner2D_set1)
            {
                controller2D.SetValue1(value);
            }

            foreach (var controller2D in inner2D_set2)
            {
                controller2D.SetValue2(value);
            }
        }

        public void DampBlendVarTowards(float target, ref float currentVelocity, float smoothTime) {
            var currentValue = GetBlendVar();
            var smoothed = Mathf.SmoothDamp(currentValue, target, ref currentVelocity, smoothTime);
            SetBlendVar(smoothed);
        }
    }
}