using System.Collections.Generic;

public class Tutorial
{
    public List<TutorialStep> steps { get; private set; }
    public int completedSteps { get; private set; } = 0;
    public bool largeAvatar;

    public Tutorial(List<TutorialStep> steps, bool largeAvatar = false)
    {
        this.steps = steps;
        this.largeAvatar = largeAvatar;
    }

    public bool IsTutorialCompleted()
    {
        return GetActiveStep() == null;
    }

    public TutorialStep GetActiveStep()
    {
        return GetStepForIndex(completedSteps);
    }

    public TutorialStep GetNextStep()
    {
        return GetStepForIndex(completedSteps + 1);
    }

    public TutorialStep GetPreviousStep()
    {
        return GetStepForIndex(completedSteps - 1);
    }

    public bool HasNextStep()
    {
        return GetNextStep() != null;
    }

    public bool HasPreviousStep()
    {
        return GetPreviousStep() != null;
    }

    public void StepCompleted()
    {
        completedSteps++;
    }

    private TutorialStep GetStepForIndex(int index)
    {
        if(index < 0 || index >= steps.Count)
        {
            // Index is out of range
            return null;
        }

        return steps[index];
    }
}