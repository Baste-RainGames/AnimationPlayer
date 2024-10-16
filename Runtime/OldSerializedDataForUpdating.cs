using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Animation_Player
{
[Serializable]
internal class AnimationLayer_V0
{
    [SerializeReference]
    public List<AnimationPlayerState> states;
    public List<StateTransition_V0> transitions;

    public string             name;
    public float              startWeight;
    public AvatarMask         mask;
    public AnimationLayerType type;

    public AnimationLayer ToV1Layer()
    {
        return new AnimationLayer
        {
            states      = states,
            name        = name,
            startWeight = startWeight,
            mask        = mask,
            type        = type,

            defaultTransitions = transitions.Where(t => t.isDefault).Select(t => t.ToDefaultTransition()).ToList(),
            namedTransitions   = transitions.Where(t => !t.isDefault).Select(t => t.ToNamedTransition()).ToList(),
        };
    }
}

[Serializable]
internal class StateTransition_V0
{
    public string name;
    public bool isDefault;

    [SerializeReference] public AnimationPlayerState fromState;
    [SerializeReference] public AnimationPlayerState toState;

    public TransitionData transitionData;

    public StateTransition ToDefaultTransition()
    {
        return new ()
        {
            fromState = fromState,
            toState = toState,
            transitionData = transitionData
        };
    }

    public NamedStateTransition ToNamedTransition()
    {
        return new ()
        {
            name = name,
            fromState = fromState,
            toState = toState,
            transitionData = transitionData
        };
    }
}
}