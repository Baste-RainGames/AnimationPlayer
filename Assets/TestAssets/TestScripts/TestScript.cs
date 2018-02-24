using Animation_Player;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private AnimationPlayer animationPlayer;
    private float attack1Dur, attack2Dur, attack3Dur;

    void Start()
    {
        animationPlayer = GetComponent<AnimationPlayer>();
        attack1Dur = animationPlayer.GetState("Attack").Duration;
//        attack2Dur = animationPlayer.GetState("Attack 2").Duration;
//        attack3Dur = animationPlayer.GetState("Attack 3").Duration;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            animationPlayer.Play("Attack");
            animationPlayer.PlayAfterSeconds(attack1Dur * .8f, "Movement");
//            animationPlayer.PlayAfterSeconds(attack1Dur * .8f, "Attack 2");
//            animationPlayer.PlayAfterSeconds(attack1Dur + (attack2Dur * .8f), "Attack 3");
//            animationPlayer.PlayAfterSeconds(attack1Dur + attack2Dur + (attack3Dur * .8f), "Movement");
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
            Debug.Log("4");
            animationPlayer.Play("Empty");
        }
    }

}