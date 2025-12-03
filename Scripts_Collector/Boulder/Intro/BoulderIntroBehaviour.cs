using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class BoulderIntroBehaviour : MonoBehaviour
{
    private const string CLOSE_ANIMATION = "Close intro";
    private const float SHOW_CLICK_HINT_AFTER_IDLE_FOR_SECONDS = 5;

    [SerializeField]
    private BoulderBehaviour boulder;

    [SerializeField]
    private Transform boulderTransform;

    private Game game;
#pragma warning disable IDE0051 // Remove unused private members
    private bool isShowingClickHint = false;
#pragma warning restore IDE0051 // Remove unused private members

    private void Start()
    {
        game = transform.root.GetComponent<Game>();

        // Scale down the gain prefabs a bit so they dont look huge
        boulder.gainPrefab.GetComponent<RectTransform>().localScale = new Vector3(.83f, .83f, 1);

        StartCoroutine(ShowTutorial());
    }

    private IEnumerator ShowTutorial()
    {
        // Show explanation
        game.ShowTutorial(new Tutorial(new List<TutorialStep>
        {
            new DelayTutorialStep(1),
            // Show tutorial with a language selector in the bottom of the screen, which will only be available if the user's
            // system language is set to a language that is supported
            new ExplainTutorialStep(Translation_Script.TUT_INTRO_1, Translations.GetSystemLanguage() != Language.EN),
            new ExplainTutorialStep(Translation_Script.TUT_INTRO_2, Translations.GetSystemLanguage() != Language.EN)
        }, true));

        // Wait till explanation is finished
        yield return new WaitUntil(() => !game.IsTutorialShowing());

        // Store current clicks
        double currentClicks = Database.instance.activeLocation.data.lifetimeBoulderClicks;
        // Make sure the first click hint is (almost) immediately shown
        float timeSinceLastClick = SHOW_CLICK_HINT_AFTER_IDLE_FOR_SECONDS - .2f;

        // Loop till intro is finished
        while(BoulderBehaviour.CalcLevel() < 2)
        {
            // Check if user has gained any rocks since last frame
            if(currentClicks == Database.instance.activeLocation.data.lifetimeBoulderClicks)
            {
                // If not, increase the idle time
                timeSinceLastClick += Time.unscaledDeltaTime;
                
                // If user is idle for too long, show the click hint
                if(timeSinceLastClick > SHOW_CLICK_HINT_AFTER_IDLE_FOR_SECONDS)
                {
                    // Show the click hint
                    game.ShowTutorial(new Tutorial(new List<TutorialStep>
                    {
                        new ClickHintTutorialStep(boulderTransform, () => Database.instance.activeLocation.data.lifetimeBoulderClicks > currentClicks)
                    }));

                    // Wait till the click hint is gone (after the user clicked the rock again)
                    yield return new WaitUntil(() => !game.IsTutorialShowing());
                }
                else
                {
                    // Go to next frame
                    yield return null;
                }
            }
            else
            {
                // Reset the idle time
                timeSinceLastClick = 0;

                // Update the current clicks
                currentClicks = Database.instance.activeLocation.data.lifetimeBoulderClicks;

                // Go to next frame
                yield return null;
            }
        }

        // Disable the boulder clicks
        boulder.clickable = false;

        // Reset the gains prefab scale to normal (1)
        boulder.gainPrefab.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 1);

        // Start anim
        Animator anim = GetComponent<Animator>();
        anim.Play(CLOSE_ANIMATION);

        // Wait for anim to finish
        yield return DelayWait.oneSecond;

        // Close this screen and update the boulder on the game screen
        game.BoulderIntroFinished();
        Destroy(gameObject);
    }
}