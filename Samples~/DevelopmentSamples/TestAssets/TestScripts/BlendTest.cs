using Animation_Player;
using UnityEngine;

public class BlendTest : MonoBehaviour
{
    public AnimationPlayer animationPlayer;
    public Animator animator;

    void Update()
    {
        for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
        {
            if (Input.GetKeyDown(k))
            {
                float keyVal = (int) k - (int) KeyCode.Alpha0;
                float val = Mathf.InverseLerp(0f, 9f, keyVal);
                
                animationPlayer.SetBlendVar("Speed", val);
                animator.SetFloat("Speed", val);
            }
        }
    }

    //    
    //    public Animator thatAnimator;
    //
    //    public AnimationClip walk;
    //    public AnimationClip jog;
    //    public AnimationClip run;
    //
    //    private PlayableGraph graph;
    //    private AnimationMixerPlayable mixer;
    //    private AnimationClipPlayable walkPlayable;
    //    private AnimationClipPlayable jogPlayable;
    //    private AnimationClipPlayable runPlayable;
    //
    //    void Start()
    //    {
    //        var animator = gameObject.AddComponent<Animator>();
    //
    //        graph = PlayableGraph.Create();
    //        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
    //        var animOutput = AnimationPlayableOutput.Create(graph, $"test the thing", animator);
    //
    //        var avgLength = (jog.length + run.length) / 2f;
    //
    //        walkPlayable = AnimationClipPlayable.Create(graph, walk);
    //        jogPlayable  = AnimationClipPlayable.Create(graph, jog);
    //        runPlayable  = AnimationClipPlayable.Create(graph, run);
    //        
    //        runPlayable.SetSpeed(run.length / avgLength);
    //        jogPlayable.SetSpeed(jog.length / avgLength);
    //
    //        mixer = AnimationMixerPlayable.Create(graph, 3, true);
    //
    //        graph.Connect(walkPlayable, 0, mixer, 1);
    //        graph.Connect(jogPlayable,  0, mixer, 1);
    //        graph.Connect(runPlayable,  0, mixer, 2);
    //
    //        mixer.SetInputWeight(0, 1f);
    //        mixer.SetInputWeight(1, 0f);
    //        mixer.SetInputWeight(2, 0f);
    //        
    //        animOutput.SetSourcePlayable(mixer);
    //        graph.Play();
    //        
    //        var visualizerClientName = name + " AnimationPlayer";
    //        GraphVisualizerClient.Show(graph, visualizerClientName);
    //    }
    //
    //    private void OnDestroy()
    //    {
    //        graph.Destroy();
    //    }
    //
    //    void Update()
    //    {
    //        for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
    //        {
    //            if (Input.GetKeyDown(k))
    //            {
    //                float keyVal = (int) k - (int) KeyCode.Alpha0;
    //                float val = Mathf.InverseLerp(0f, 9f, keyVal);
    //                
    //                thatAnimator.SetFloat("Blend", val);
    //
    //                mixer.SetInputWeight(0, 1 - val);
    //                mixer.SetInputWeight(1, val);
    //
    //                var jogSpeedWhenPlayingJog = 1f;
    //                var jogSpeedWhenPlayingRun = jog.length / run.length;
    //
    //                var runSpeedWhenPlayingRun = 1f;
    //                var runSpeedWhenPlayingJog = run.length / jog.length;
    //
    //                jogPlayable.SetSpeed(Mathf.Lerp(jogSpeedWhenPlayingJog, jogSpeedWhenPlayingRun, val));
    //                runPlayable.SetSpeed(Mathf.Lerp(runSpeedWhenPlayingJog, runSpeedWhenPlayingRun, val));
    //    
    //                Debug.Log(mixer.GetInputWeight(0) + "/" + mixer.GetInputWeight(1));
    //            }
    //        }
    //    }
}
