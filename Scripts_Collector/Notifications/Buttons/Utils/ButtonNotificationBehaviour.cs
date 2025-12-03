using UnityEngine;

public abstract class ButtonNotificationBehaviour : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    protected abstract bool ShouldNotify();

    protected bool isNotifying = false;

    protected void InitNotification()
    {
        isNotifying = ShouldNotify();
        PlayAnim();
    }

    protected void UpdateNotification()
    {
        if (isNotifying != ShouldNotify())
        {
            isNotifying = !isNotifying;
            PlayAnim();
        }
    }

    protected void PlayAnim()
    {
        if (isNotifying)
        {
            // Notify
            animator.Play("Notify");
        }
        else
        {
            // Stop notifying
            animator.Play("Default");
        }
    }

    public void ForceUpdateNotification()
    {
        if(gameObject.activeInHierarchy)
        {
            UpdateNotification();
        }
    }
}