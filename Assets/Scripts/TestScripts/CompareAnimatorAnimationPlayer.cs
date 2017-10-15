using UnityEngine;

namespace Animation_Player
{
	public class CompareAnimatorAnimationPlayer : MonoBehaviour
	{

		public Animator animator;
		public AnimationPlayer animationPlayer;

		[Range(0f, 1f)]
		public float forward;
		[Range(-1f, 1f)]
		public float turn;


		void Update()
		{
			animator.SetFloat("Forward", forward);
			animator.SetFloat("Turn", turn);

			animationPlayer.SetBlendVar("Forward", forward);
			animationPlayer.SetBlendVar("Turn", turn);
		}
	}
}