using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject clickHintPrefab, blockUIPrefab;

    [SerializeField]
    private GameObject smallAvatar, largeAvatar, languageSetting;

    [SerializeField]
    private RectTransform chatBubble;

    [SerializeField]
    private Image backgroundImg;

    [SerializeField]
    private Button explainContinueButton;

    [SerializeField]
    private TMP_Text explainTxt;

    [SerializeField]
    private TextMultiLang continueTxt;

    [SerializeField]
    private Animator explainAnimator, continueTxtAnimator;

    public Tutorial tutorial;

    private Coroutine animExplainText = null;
    private bool isExplainAnimPlaying = false;
    private bool speedUpExplain = false;

    private void Start()
    {
        ShowActiveStep();
    }

    private void GoToNextStep()
    {
        tutorial.StepCompleted();
        ShowActiveStep();
    }

    public void OnExplainContinueClicked()
    {
        if(isExplainAnimPlaying)
        {
            speedUpExplain = true;
        }
        else
        {
            explainContinueButton.interactable = false;

            if (!tutorial.HasNextStep() || tutorial.GetNextStep().GetType() != typeof(ExplainTutorialStep))
            {
                StartCoroutine(AnimateOutExplainAvatar());
            }
            else
            {
                GoToNextStep();
            }
        }
    }

    private void ShowActiveStep()
    {
        if(tutorial.IsTutorialCompleted())
        {
            // Tutorial is completed, destroy it
            StopTutorial();
            return;
        }

        languageSetting.SetActive(tutorial.GetActiveStep().showLanguageSettings);

        switch (tutorial.GetActiveStep())
        {
            case ExplainTutorialStep step:
                ShowExplainStep(step);
                break;
            case ClickHintTutorialStep step:
                StartCoroutine(ShowClickHint(step));
                break;
            case DelayTutorialStep step:
                StartCoroutine(WaitDelayStep(step));
                break;
            case WaitUntilTutorialStep step:
                StartCoroutine(WaitUntilConditionStep(step));
                break;
            case ActionTutorialStep step:
                step.action?.Invoke();
                GoToNextStep();
                return;
            default:
                // Uknown kind of step, skipping it
                GoToNextStep();
                return;
        }
    }

    private void ShowExplainStep(ExplainTutorialStep step)
    {
        if(!tutorial.HasPreviousStep() || tutorial.GetPreviousStep().GetType() != typeof(ExplainTutorialStep))
        {
            StartCoroutine(AnimateInExplainAvatar(step));
        }
        else
        {
            StartCoroutine(ShowExplainText(step.translationId));
        }
    }

    private IEnumerator AnimateInExplainAvatar(ExplainTutorialStep step)
    {
        if (tutorial.largeAvatar)
        {
            explainAnimator.Play("ShowPuppet");
        }
        else
        {
            explainAnimator.Play("ShowPuppetSmall");
        }

        // Wait a frame for animation to be playing
        yield return null;

        // Wait till animation is done
        yield return new WaitUntil(() => explainAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1);

        StartCoroutine(ShowExplainText(step.translationId));
    }

    private IEnumerator AnimateOutExplainAvatar()
    {
        if (tutorial.largeAvatar)
        {
            explainAnimator.Play("HidePuppet");
        }
        else
        {
            explainAnimator.Play("HidePuppetSmall");
        }

        // Wait a frame for animation to be playing
        yield return null;

        // Wait till animation is done
        yield return new WaitUntil(() => explainAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1);

        explainTxt.text = "";

        if (tutorial.HasNextStep())
        {
            // If the tutorial has a next step, wait a tiny duration to show it
            yield return DelayWait.oneFifthSecond;
        }

        GoToNextStep();
    }

    private IEnumerator ShowExplainText(Translation_Script translationId)
    {
        // Indiciate that the anim is playing
        isExplainAnimPlaying = true;

        continueTxtAnimator.Play("Hide");

        // Active the continue button to skip anim
        explainContinueButton.interactable = true;

        // Start showing the msg in the bubble
        animExplainText = StartCoroutine(AnimateExplainText(Translator.GetTranslationForId(translationId)));

        // Check every frame if the user clicked to speed up the anim
        while(animExplainText != null)
        {
            if(speedUpExplain)
            {
                // If so, stop the animation coroutine, show all the text and move on
                StopCoroutine(animExplainText);
                animExplainText = null;
                explainTxt.text = Translator.GetTranslationForId(translationId);
            }
            else
            {
                // Else wait a frame before checking again
                yield return null;
            }
        }

        // Tiny delay to prevent people from accidentaly skipping the explanation
        yield return DelayWait.oneFifthSecond;

        // Show "Click to continue/close"
        continueTxt.translationId = tutorial.completedSteps + 1 >= tutorial.steps.Count ? Translation_Inspector.TAP_TO_CLOSE : Translation_Inspector.TAP_TO_CONTINUE;
        continueTxtAnimator.Play("Show");

        // Indicate that the anim has finished playing
        isExplainAnimPlaying = false;

        // Reset the speed of the anim
        speedUpExplain = false;
    }

    private IEnumerator AnimateExplainText(string text)
    {
        char[] message = text.ToCharArray();
        string output = "";

        for (int i = 0; i < message.Length; i++)
        {
            output += message[i];
            explainTxt.text = output;

            // Check if the character should be followed with a long pause
            if (message[i].Equals('.') || message[i].Equals('!') || message[i].Equals('?'))
            {
                yield return DelayWait.halfSecond;
            }
            // Else check if the character should be followed with a short pause
            else if (message[i].Equals(','))
            {
                yield return DelayWait.oneFifthSecond;
            }
            else
            {
                // Else just do a minor delay before showing the next character
                yield return DelayWait.oneThirtiethSecond;
            }
        }

        animExplainText = null;
    }

    public void UpdateTranslation()
    {
        switch (tutorial.GetActiveStep())
        {
            case ExplainTutorialStep step:
                if(animExplainText != null)
                {
                    // Stop the anim 
                    StopCoroutine(animExplainText);
                    animExplainText = null;
                }

                // Update the translation
                explainTxt.text = Translator.GetTranslationForId(step.translationId);
                break;
        }
    }

    private IEnumerator ShowClickHint(ClickHintTutorialStep step)
    {
        GameObject hintObj = Instantiate(clickHintPrefab, step.parent);

        if(step.reversed)
        {
            hintObj.GetComponent<RectTransform>().rotation = Quaternion.Euler(0, 180, 0);
        }

        if(step.position != null)
        {
            hintObj.GetComponent<RectTransform>().position = step.position();
        }

        yield return new WaitUntil(() => hintObj == null || step.isCompleted());

        if(!step.isCompleted())
        {
            // Cancel tutorial when hintObj is destroyed but step is not completed
            StopTutorial();
            yield break;
        }
        else
        {
            // If step was completed, delete the click hint and go to the next step
            Destroy(hintObj);
            GoToNextStep();
        }
    }

    private IEnumerator WaitDelayStep(DelayTutorialStep step)
    {
        GameObject blockUI = Instantiate(blockUIPrefab, transform);
        yield return new WaitForSecondsRealtime(step.delay);
        Destroy(blockUI);

        GoToNextStep();
    }

    private IEnumerator WaitUntilConditionStep(WaitUntilTutorialStep step)
    {
        yield return new WaitUntil(step.condition);

        GoToNextStep();
    }

    private void StopTutorial()
    {
        Destroy(gameObject);
    }
}