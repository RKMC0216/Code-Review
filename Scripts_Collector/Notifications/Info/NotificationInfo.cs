using UnityEngine;

public class NotificationInfo
{
    public string text { get; private set; }
    public Sprite sprite { get; private set; }
    public int maxFontSize { get; private set; }
    public Sound notificationSound { get; private set; }
    public float soundDelay { get; private set; }

    public NotificationInfo(string text, Sprite sprite, Sound notificationSound, float soundDelay = 0.5f)
    {
        this.text = text;
        this.sprite = sprite;
        this.notificationSound = notificationSound;
        this.soundDelay = soundDelay;
    }

    public NotificationInfo(string text, Sprite sprite, Sound notificationSound, int maxFontSize, float soundDelay = 0.5f) :
        this(text, sprite, notificationSound, soundDelay)
    {
        this.maxFontSize = maxFontSize;
    }

    public NotificationInfo(Milestone milestone) : 
        this(milestone.GetFormattedRewardString(true), milestone.GetMilestoneRewardTargetSprite(), milestone.HasSpecialReward() ? Sound.GEMS_GAIN : Sound.GOAL_COMPLETE, milestone.HasSpecialReward() ? 1f : .5f)
    {

    }
}