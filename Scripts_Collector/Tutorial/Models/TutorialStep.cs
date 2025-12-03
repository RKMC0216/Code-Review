using UnityEngine;
using System;

public abstract class TutorialStep
{
    public bool showLanguageSettings = false;
}

public class ExplainTutorialStep : TutorialStep
{
    public Translation_Script translationId;

    public ExplainTutorialStep(Translation_Script translationId, bool showLanguageSettings = false)
    {
        this.translationId = translationId;
        this.showLanguageSettings = showLanguageSettings;
    }
}

public class ClickHintTutorialStep : TutorialStep
{
    public Transform parent;
    public Func<bool> isCompleted;
    public Func<Vector2> position;
    public bool reversed;

    public ClickHintTutorialStep(Transform parent, Func<bool> isCompleted, Func<Vector2> position = null, bool reversed = false)
    {
        this.parent = parent;
        this.isCompleted = isCompleted;
        this.position = position;
        this.reversed = reversed;
    }
}

public class DelayTutorialStep : TutorialStep
{
    public float delay { get; private set; }

    public DelayTutorialStep(float delay)
    {
        this.delay = delay;
    }
}

public class WaitUntilTutorialStep : TutorialStep
{
    public Func<bool> condition { get; private set; }

    public WaitUntilTutorialStep(Func<bool> condition)
    {
        this.condition = condition;
    }
}

public class ActionTutorialStep : TutorialStep
{
    public Action action;

    public ActionTutorialStep(Action action)
    {
        this.action = action;
    }
}