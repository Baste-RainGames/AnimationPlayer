using System;
using Animation_Player;
using UnityEngine;

public class TestScript : MonoBehaviour
{/*
    private AnimationPlayer animationPlayer;
    private int testIndex;
    public AnimationClip clip;

    void Start()
    {
        animationPlayer = GetComponent<AnimationPlayer>();
        animationPlayer.RegisterAnimationEventListener("TestEvent1", TestEvent1);
//        animationPlayer.RegisterAnimationEventListener("TestEvent2", TestEvent2);
    }

    private void TestEvent2() {
        print("Test Event 2!");
    }

    private void TestEvent1() {
        print("Test Event 1!");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            animationPlayer.Play("Attack 1");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }
        
        if (Input.GetKeyDown(KeyCode.W))
        {
            animationPlayer.Play("Attack 2");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }
        
        if (Input.GetKeyDown(KeyCode.E))
        {
            animationPlayer.Play("Attack 3");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            animationPlayer.Play("Empty");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!animationPlayer.HasState("Test", 0))
            {
                var newState = SingleClip.Create("Test", clip);
                testIndex = animationPlayer.AddState(newState);
            }
        
            animationPlayer.Play(testIndex);
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("0");
            animationPlayer.SetBlendVar("Speed", 0);
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("1");
            animationPlayer.SetBlendVar("Speed", 1);
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("2");
            animationPlayer.SetBlendVar("Speed", 2);
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("2");
            animationPlayer.SetBlendVar("Speed", 1.5f);
        }
    }
*/
}