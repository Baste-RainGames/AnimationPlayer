using System.Collections.Generic;

namespace Animation_Player
{
    /// <summary>
    /// Instead of calling player.SetBlendVar("myVar", value), you can get a BlendVarController through player.GetBlendControllerFor("myVar"),
    /// which can be cached. This saves some internal Dictionary lookups, which speeds up things.
    /// </summary>
    public class BlendVarController
    {

        private readonly List<AnimationLayer.BlendTreeController1D> inner1D = new List<AnimationLayer.BlendTreeController1D>();
        private readonly List<AnimationLayer.BlendTreeController2D> inner2D_set1 = new List<AnimationLayer.BlendTreeController2D>();
        private readonly List<AnimationLayer.BlendTreeController2D> inner2D_set2 = new List<AnimationLayer.BlendTreeController2D>();
        private readonly string blendVar;

        public BlendVarController(string blendVar)
        {
            this.blendVar = blendVar;
        }

        public int InnerControllerCount => inner1D.Count + inner2D_set1.Count + inner2D_set2.Count;

        public void AddControllers(List<AnimationLayer.BlendTreeController1D> blendControllers1D)
        {
            inner1D.AddRange(blendControllers1D);
        }

        public void AddControllers(List<AnimationLayer.BlendTreeController2D> blendControllers2D)
        {
            foreach (var controller2D in blendControllers2D)
            {
                if (controller2D.blendVar1 == blendVar)
                    inner2D_set1.Add(controller2D);
                else
                    inner2D_set2.Add(controller2D);
            }
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
    }
}