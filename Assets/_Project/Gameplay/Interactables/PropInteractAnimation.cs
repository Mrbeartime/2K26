using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public class PropInteractAnimation : MonoBehaviour
{
    [SerializeField] private AnimationClip interactClip;
    [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private float time;
    private float targetTime;

    private void OnEnable()
    {
        if (interactClip == null) return;
        var animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create(name + " Interact");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, interactClip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(0);
        var output = AnimationPlayableOutput.Create(graph, "Prop", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        Sample();
    }

    public void SetActiveState(bool active)
    {
        if (interactClip != null) targetTime = active ? interactClip.length : 0f;
    }

    public void PlayInteract()
    {
        if (interactClip == null) return;
        time = 0f;
        targetTime = interactClip.length;
    }

    public void ResetPose()
    {
        time = targetTime = 0f;
        Sample();
    }

    private void Update()
    {
        if (!graph.IsValid()) return;
        time = Mathf.MoveTowards(time, targetTime, Time.deltaTime * playbackSpeed);
        Sample();
    }

    private void Sample()
    {
        if (!graph.IsValid()) return;
        playable.SetTime(time);
        graph.Evaluate(0f);
    }

    private void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}
