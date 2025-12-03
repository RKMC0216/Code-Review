using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestoneBehaviour : MonoBehaviour
{
    [SerializeField]
    private Image targetImage;

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private TMP_Text progressTxt, rewardTxt;

    [HideInInspector]
    public Milestone milestone;

    void Start()
    {
        targetImage.sprite = Database.instance.activeLocation.GetSpriteForCollector(milestone.milestoneTargetId);
        progressSlider.value = 1 - milestone.GetProgressToCompletionValue();
        progressTxt.text = milestone.GetFormattedProgressString();
        rewardTxt.text = milestone.GetFormattedRewardString(false);
    }
}