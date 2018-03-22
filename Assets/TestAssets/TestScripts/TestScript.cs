using System;
using Animation_Player;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private AnimationPlayer animationPlayer;
    public AnimationClip clip;

    void Start()
    {
        animationPlayer = GetComponent<AnimationPlayer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            animationPlayer.Play("Attack_1");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }
        
        if (Input.GetKeyDown(KeyCode.W))
        {
            animationPlayer.Play("Attack_2");
            animationPlayer.PlayAfterSeconds(animationPlayer.GetPlayingState().Duration * .8f, "Movement");
        }
        
        if (Input.GetKeyDown(KeyCode.E))
        {
            animationPlayer.Play("Attack_3");
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
                animationPlayer.AddState(newState, 0);
            }

            animationPlayer.Play("Test", 0);
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
    }

}