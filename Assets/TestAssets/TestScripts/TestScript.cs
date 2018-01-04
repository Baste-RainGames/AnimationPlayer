using System.Collections;
using Animation_Player;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private AnimationPlayer animationPlayer;

    void Awake()
    {
        animationPlayer = GetComponent<AnimationPlayer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        animationPlayer.Play("Attack");
        yield return new WaitForSeconds(animationPlayer.GetPlayingState().Duration * .8f);
        animationPlayer.PlayDefaultStateIfPlaying("Attack");
    }
}