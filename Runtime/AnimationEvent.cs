using System;
using System.Collections.Generic;

namespace Animation_Player
{
[Serializable]
public class AnimationEvent
{
    /// <summary>
    /// Name of the Animation Event, used to bind event receivers.
    /// </summary>
    public string name;
    /// <summary>
    /// At what time in the state the event should be played, in seconds.
    /// </summary>
    public double time;
    /// <summary>
    /// Only play this animation event if it's on the active state. If false, it's also played on states being blended out of.
    /// </summary>
    public bool mustBeActiveState = true;
    /// <summary>
    /// The minimum weight needed for the event to fire. If the weight is less than this, the event will be skipped.
    /// </summary>
    public float minWeight = .5f;

    private readonly List<Action> registeredActions = new ();
    private readonly List<Action> registeredActionsForCurrentState = new ();

    public void RegisterListener(Action listener)
    {
        registeredActions.Add(listener);
    }

    public void RegisterListenerForCurrentState(Action listener)
    {
        registeredActionsForCurrentState.Add(listener);
    }

    public void ClearRegisteredForCurrentState()
    {
        registeredActionsForCurrentState.Clear();
    }

    public void InvokeRegisteredListeners()
    {
        foreach (var action in registeredActions)
            action();
        foreach (var action in registeredActionsForCurrentState)
            action();
    }
}
}