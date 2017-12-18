using System;
using System.Collections.Generic;

namespace Animation_Player {
    [Serializable]
    public class AnimationEvent
    {
        public string name;
        public double time;

        private List<Action> registeredActions = new List<Action>();

        public void RegisterListener(Action listener)
        {
            registeredActions.Add(listener);
        }

        public void InvokeRegisteredListeners()
        {
            foreach (var action in registeredActions)
                action();
        }
    }
}