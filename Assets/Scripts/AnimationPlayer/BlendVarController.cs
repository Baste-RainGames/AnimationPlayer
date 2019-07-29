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
        private readonly string                      blendVar;
        public           string                      BlendVar => blendVar;

        public float MinValue
        {
            get;
            private set;
        }
        public float MaxValue
        {
            get;
            private set;
        }

        public BlendVarController(string blendVar)
        {
            this.blendVar = blendVar;
        }

        public int InnerControllerCount => inner1D.Count + inner2D_set1.Count + inner2D_set2.Count;

        public void AddControllers(List<BlendTreeController1D> blendControllers1D)
        {
            inner1D.AddRange(blendControllers1D);
            foreach (var blendController in blendControllers1D)
            {
                MinValue = Mathf.Min(blendController.GetMinThreshold(), MinValue);
                MaxValue = Mathf.Max(blendController.GetMaxThreshold(), MaxValue);
            }
        }

        public void AddControllers(List<BlendTreeController2D> blendControllers2D)
        {
            foreach (var controller2D in blendControllers2D)
            {
                if (controller2D.blendVar1 == blendVar)
                {
                    inner2D_set1.Add(controller2D);
                    MinValue = Mathf.Min(controller2D.GetMinValForVar1(), MinValue);
                    MaxValue = Mathf.Max(controller2D.GetMaxValForVar1(), MaxValue);
                }
                else
                {
                    inner2D_set2.Add(controller2D);
                    MinValue = Mathf.Min(controller2D.GetMinValForVar2(), MinValue);
                    MaxValue = Mathf.Max(controller2D.GetMaxValForVar2(), MaxValue);
                }
            }
        }

        public float GetBlendVar()
        {
            if (inner1D.Count > 0)
                return inner1D[0].CurrentValue;

            if (inner2D_set1.Count > 0)
                return inner2D_set1[0].CurrentValue1;

            if (inner2D_set2.Count > 0)
                return inner2D_set2[0].CurrentValue2;

            return 0f; //error?
        }

        public void SetBlendVar(float value)
        {
            for (var i = 0; i < inner1D.Count; i++)
                inner1D[i].SetValue(value);

            for (var i = 0; i < inner2D_set1.Count; i++)
                inner2D_set1[i].SetValue1(value);

            for (var i = 0; i < inner2D_set2.Count; i++)
                inner2D_set2[i].SetValue2(value);
        }

        public void DampBlendVarTowards(float target, ref float currentVelocity, float smoothTime)
        {
            var currentValue = GetBlendVar();
            var smoothed     = Mathf.SmoothDamp(currentValue, target, ref currentVelocity, smoothTime);
            SetBlendVar(smoothed);
        }
    }
}