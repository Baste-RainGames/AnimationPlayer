using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Animation_Player
{
    [Serializable]
    public class AnimationLayer
    {
        public List<AnimationState> states;
        public List<StateTransition> transitions;

        public float startWeight;
        public AvatarMask mask;
        public AnimationLayerType type = AnimationLayerType.Override;

        public AnimationMixerPlayable stateMixer { get; private set; }
        private int currentPlayedState;

        //blend info:
        private bool blending;
        private TransitionData blendTransitionData;
        private float blendStartTime;
        private bool[] activeWhenBlendStarted;
        private float[] valueWhenBlendStarted;

        //transitionLookup[a, b] contains the index of the transition from a to b in transitions
        private int[,] transitionLookup;
        private Playable[] runtimePlayables;

        private readonly Dictionary<string, int> stateNameToIdx = new Dictionary<string, int>();

        //@TODO: string key is slow
        private readonly Dictionary<string, float> blendVars = new Dictionary<string, float>();
        private readonly Dictionary<string, List<BlendTreeController1D>> varTo1DBlendControllers = new Dictionary<string, List<BlendTreeController1D>>();
        private readonly Dictionary<string, List<BlendTreeController2D>> varTo2DBlendControllers = new Dictionary<string, List<BlendTreeController2D>>();

        public void InitializeSelf(PlayableGraph graph)
        {
            if (states.Count == 0)
            {
                stateMixer = AnimationMixerPlayable.Create(graph, 0, false);
                return;
            }

            foreach (var transition in transitions)
            {
                transition.FetchStates(states);
            }

            runtimePlayables = new Playable[states.Count];

            stateMixer = AnimationMixerPlayable.Create(graph, states.Count, false);
            stateMixer.SetInputWeight(0, 1f);
            currentPlayedState = 0;

            // Add the statess to the graph
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                stateNameToIdx[state.Name] = i;

                var playable = state.GeneratePlayable(graph, varTo1DBlendControllers, varTo2DBlendControllers);
                runtimePlayables[i] = playable;
                graph.Connect(playable, 0, stateMixer, i);
            }

            activeWhenBlendStarted = new bool[states.Count];
            valueWhenBlendStarted = new float[states.Count];

            transitionLookup = new int[states.Count, states.Count];
            for (int i = 0; i < states.Count; i++)
            for (int j = 0; j < states.Count; j++)
                transitionLookup[i, j] = -1;

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                var fromState = states.IndexOf(transition.FromState);
                var toState = states.IndexOf(transition.ToState);
                if (fromState == -1 || toState == -1)
                {
                    //TODO: fixme
                }
                else
                {
                    if (transitionLookup[fromState, toState] != -1)
                        Debug.LogWarning("Found two transitions from " + states[fromState] + " to " + states[toState]);

                    transitionLookup[fromState, toState] = i;
                }
            }
        }

        public void InitializeLayerBlending(PlayableGraph graph, int layerIndex, AnimationLayerMixerPlayable layerMixer)
        {
            graph.Connect(stateMixer, 0, layerMixer, layerIndex);

            layerMixer.SetInputWeight(layerIndex, startWeight);
            layerMixer.SetLayerAdditive((uint) layerIndex, type == AnimationLayerType.Additive);
            if (mask != null)
                layerMixer.SetLayerMaskFromAvatarMask((uint) layerIndex, mask);
        }

        public bool HasState(string stateName)
        {
            foreach (var state in states)
            {
                if (state.Name == stateName)
                    return true;
            }
            return false;
        }

        public void PlayUsingExternalTransition(int state, TransitionData transitionData)
        {
            Play(state, transitionData);
        }

        public void PlayUsingInternalTransition(int state, TransitionData defaultTransition)
        {
            var transitionToUse = transitionLookup[currentPlayedState, state];
            var transition = transitionToUse == -1 ? defaultTransition : transitions[transitionToUse].transitionData;
            Play(state, transition);
        }

        private void Play(int state, TransitionData transitionData)
        {
            if (state < 0 || state >= states.Count)
            {
                Debug.LogError($"Trying to play out of bounds clip {state}! There are {states.Count} clips in the animation player");
                return;
            }
            if (transitionData.type == TransitionType.Curve && transitionData.curve == null)
            {
                Debug.LogError("Trying to play an animationCurve based transition, but the transition curve is null!");
                return;
            }

            var isCurrentlyPlaying = stateMixer.GetInputWeight(state) > 0f;
            if (!isCurrentlyPlaying)
            {
                runtimePlayables[state].SetTime(0f);
            }

            if (transitionData.duration <= 0f)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    stateMixer.SetInputWeight(i, i == state ? 1f : 0f);
                }
                currentPlayedState = state;
                blending = false;
            }
            else if (state != currentPlayedState)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    var currentMixVal = stateMixer.GetInputWeight(i);
                    activeWhenBlendStarted[i] = currentMixVal > 0f;
                    valueWhenBlendStarted[i] = currentMixVal;
                }

                blending = true;
                currentPlayedState = state;
                blendTransitionData = transitionData;
                blendStartTime = Time.time;
            }
        }

        public void Update()
        {
            if (!blending)
                return;

            var lerpVal = (Time.time - blendStartTime) / blendTransitionData.duration;
            if (blendTransitionData.type == TransitionType.Curve)
            {
                lerpVal = blendTransitionData.curve.Evaluate(lerpVal);
            }

            for (int i = 0; i < states.Count; i++)
            {
                var isTargetClip = i == currentPlayedState;
                if (isTargetClip || activeWhenBlendStarted[i])
                {
                    var target = isTargetClip ? 1f : 0f;
                    stateMixer.SetInputWeight(i, Mathf.Lerp(valueWhenBlendStarted[i], target, lerpVal));
                }
            }

            if (lerpVal < 1)
                return;

            blending = false;
        }

        public float GetStateWeight(int state)
        {
            if (state < 0 || state >= states.Count)
            {
                Debug.LogError($"Trying to get the state weight for {state}, which is out of bounds! There are {states.Count} states!");
                return 0f;
            }
            return stateMixer.GetInputWeight(state);
        }

        public int GetStateIdx(string stateName)
        {
            int idx;
            if (stateNameToIdx.TryGetValue(stateName, out idx))
                return idx;
            return -1;
        }

        public bool IsBlending()
        {
            return blending;
        }

        public static AnimationLayer CreateLayer()
        {
            var layer = new AnimationLayer
            {
                states = new List<AnimationState>(),
                transitions = new List<StateTransition>(),
                startWeight = 1f
            };
            return layer;
        }

        public void SetBlendVar(string var, float value)
        {
            blendVars[var] = value;

            List<BlendTreeController1D> blendControllers1D;
            if (varTo1DBlendControllers.TryGetValue(var, out blendControllers1D))
                foreach (var controller in blendControllers1D)
                    controller.SetValue(value);

            List<BlendTreeController2D> blendControllers2D;
            if (varTo2DBlendControllers.TryGetValue(var, out blendControllers2D))
                foreach (var controller in blendControllers2D)
                    controller.SetValue(var, value);
        }

        public float GetBlendVar(string var)
        {
            float result = 0f;
            blendVars.TryGetValue(var, out result);
            return result;
        }

        public void AddAllPlayingStatesTo(List<AnimationState> results)
        {
            results.Add(states[currentPlayedState]);

            for (var i = 0; i < states.Count; i++)
            {
                if (i == currentPlayedState)
                    return;
                var state = states[i];
                if (stateMixer.GetInputWeight(i) > 0f)
                {
                    results.Add(state);
                }
            }
        }

        public class BlendTreeController1D
        {
            private AnimationMixerPlayable mixer;
            private readonly float[] thresholds;
            private float lastValue;

            public BlendTreeController1D(AnimationMixerPlayable mixer, float[] thresholds)
            {
                for (int i = 0; i < thresholds.Length - 2; i++)
                    //@TODO: should reorder these!
                    if (thresholds[i] >= thresholds[i + 1])
                        throw new UnityException($"The thresholds on the blend tree should be be strictly increasing!");

                this.mixer = mixer;
                this.thresholds = thresholds;
            }

            public void SetValue(float value)
            {
                if (value == lastValue)
                    return;
                lastValue = value;

                int idxOfLastLowerThanVal = -1;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    var threshold = thresholds[i];
                    if (threshold <= value)
                        idxOfLastLowerThanVal = i;
                    else
                        break;
                }

                int idxBefore = idxOfLastLowerThanVal;
                int idxAfter = idxOfLastLowerThanVal + 1;

                float fractionTowardsAfter;
                if (idxBefore == -1)
                    fractionTowardsAfter = 1f; //now after is 0
                else if (idxAfter == thresholds.Length)
                    fractionTowardsAfter = 0f; //now before is the last
                else
                {
                    var range = (thresholds[idxAfter] - thresholds[idxBefore]);
                    var distFromStart = (value - thresholds[idxBefore]);
                    fractionTowardsAfter = distFromStart / range;
                }

                for (int i = 0; i < thresholds.Length; i++)
                {
                    float inputWeight;

                    if (i == idxBefore)
                        inputWeight = 1f - fractionTowardsAfter;
                    else if (i == idxAfter)
                        inputWeight = fractionTowardsAfter;
                    else
                        inputWeight = 0f;

                    mixer.SetInputWeight(i, inputWeight);
                }
            }
        }

        public class BlendTreeController2D
        {
            public readonly string blendVar1;
            public readonly string blendVar2;
            private float minVal1, minVal2, maxVal1, maxVal2;
            private Vector2 currentBlendVector;

            private readonly AnimationMixerPlayable treeMixer;
            private readonly BlendTree2DMotion[] motions;
            private readonly float[] motionInfluences;

            public BlendTreeController2D(string blendVar1, string blendVar2, AnimationMixerPlayable treeMixer, int numClips)
            {
                this.blendVar1 = blendVar1;
                this.blendVar2 = blendVar2;
                this.treeMixer = treeMixer;
                motions = new BlendTree2DMotion[numClips];
                motionInfluences = new float[numClips];
            }

            public void Add(int clipIdx, float threshold, float threshold2)
            {
                motions[clipIdx] = new BlendTree2DMotion(new Vector2(threshold, threshold2));

                minVal1 = Mathf.Min(threshold, minVal1);
                minVal2 = Mathf.Min(threshold2, minVal2);
                maxVal1 = Mathf.Max(threshold, maxVal1);
                maxVal2 = Mathf.Max(threshold2, maxVal2);
            }

            public void SetValue1(float value)
            {
                currentBlendVector.x = Mathf.Clamp(value, minVal1, maxVal1);
                Recalculate();
            }

            public void SetValue2(float value)
            {
                currentBlendVector.y = Mathf.Clamp(value, minVal2, maxVal2);
                Recalculate();
            }

            public void SetValue(string blendVar, float value)
            {
                if (blendVar == blendVar1)
                    currentBlendVector.x = Mathf.Clamp(value, minVal1, maxVal1);
                else
                    currentBlendVector.y = Mathf.Clamp(value, minVal2, maxVal2);

                //Recalculate weights, based on Rune Skovbo Johansen's thesis (Docs/rune_skovbo_johansen_thesis.pdf)
                //For now, using the version without polar coordinates
                //@TODO: use the polar coordinate version, looks better

                Recalculate();
            }

            private void Recalculate()
            {
                float influenceSum = 0f;
                for (int i = 0; i < motions.Length; i++)
                {
                    var influence = InfluenceFunc(currentBlendVector, i);
                    motionInfluences[i] = influence;
                    influenceSum += influence;
                }

                for (int i = 0; i < motions.Length; i++)
                {
                    treeMixer.SetInputWeight(i, motionInfluences[i] / influenceSum);
                }
            }

            //See chapter 6.3 in Docs/rune_skovbo_johansen_thesis.pdf
            private float InfluenceFunc(Vector2 inputPoint, int referencePointIdx)
            {
                if (motions.Length == 1)
                    return 1f;

                var referencePoint = motions[referencePointIdx].thresholdPoint;
                Func<int, float> subFunc = idx =>
                {
                    var toPointAtIdx = motions[idx].thresholdPoint - referencePoint;

                    var dotProd = Vector2.Dot(inputPoint - referencePoint, toPointAtIdx);
                    var magSqr = Mathf.Pow(toPointAtIdx.magnitude, 2);

                    var val = dotProd / magSqr;
                    return 1f - val;
                };

                var minVal = Mathf.Infinity;
                for (int i = 0; i < motions.Length; i++)
                {
                    // Note that we will get infinity values if there are two motions with the same thresholdPoint.
                    // But having two motions at the same point should error further up, as it's kinda meaningless.
                    if (i == referencePointIdx)
                        continue;
                    var val = subFunc(i);
                    if (val < minVal)
                        minVal = val;

                    //This is not mentioned in the thesis, but seems to be neccessary.
                    if (minVal < 0f)
                        minVal = 0f;
                }
                return minVal;
            }

            private class BlendTree2DMotion
            {
                public readonly Vector2 thresholdPoint;

                public BlendTree2DMotion(Vector2 thresholdPoint)
                {
                    this.thresholdPoint = thresholdPoint;
                }
            }
        }

        public void AddTreesMatchingBlendVar(BlendVarController aggregateController, string blendVar)
        {
            List<BlendTreeController1D> blendControllers1D;
            if (varTo1DBlendControllers.TryGetValue(blendVar, out blendControllers1D))
                aggregateController.AddControllers(blendControllers1D);

            List<BlendTreeController2D> blendControllers2D;
            if (varTo2DBlendControllers.TryGetValue(blendVar, out blendControllers2D))
                aggregateController.AddControllers(blendControllers2D);
        }

        public AnimationState GetCurrentPlayingState()
        {
            if (states.Count == 0)
                return null;
            return states[currentPlayedState];
        }
    }

    public enum AnimationLayerType
    {
        Override,
        Additive
    }
}