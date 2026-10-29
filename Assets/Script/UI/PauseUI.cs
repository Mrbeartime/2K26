using UnityEngine;
using UnityEngine.UI;
public class PauseUI : MonoBehaviour
{
    [SerializeField] public Animator animator;

    private void Awake()
    {
        animator.Play("OpenScroll", 0 , 0f);
    }
}
