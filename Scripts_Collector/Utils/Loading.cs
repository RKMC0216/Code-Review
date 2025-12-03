using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;

public class Loading : MonoBehaviour
{
    public const int SCENE_ID_LOADING = 0;
    public const int SCENE_ID_GAME = 1;
    public const int SCENE_ID_TRAVEL = 2;

    [SerializeField]
    private GameObject loadingInfoContainer;

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private TMP_Text progressTxt, loadingInfoTxt, tipTxt;

    [SerializeField]
    private Image loadingImg;

    [SerializeField]
    private Sprite[] loadingSprites;

    private void Awake()
    {
        // Init translation service
        Translator.Initialize();
        // Init number formatter, has to be called after translator since it relies on the initialized language
        NumberFormatter.Initialize();
    }

    private void Start()
    {
        // Turn on battery saver mode if it is enabled, or set default settings
        SettingsBehaviour.SetBatterySaverMode(Database.instance.batterySaver);

        // Set a random loading sprite
        loadingImg.sprite = loadingSprites[UnityEngine.Random.Range(0, loadingSprites.Length)];

        // Show a random tip
        ShowRandomTip(false);

        // Start loading the game
        StartCoroutine(LoadGame());
    }

    private IEnumerator LoadGame()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(SCENE_ID_GAME);
        load.allowSceneActivation = false;

        float progress = 0;
        while (progress < 1)
        {
            progress += Time.deltaTime / 3f;

            if (progress > load.progress + 0.1f)
            {
                progress = load.progress + 0.1f;
            }

            progressSlider.value = 1 - progress;
            progressTxt.text = Math.Ceiling(progress * 100) + "%";

            if (progress >= 1)
            {
                yield return DelayWait.oneFifthSecond;

                if (!Database.instance.isInitialized)
                {
                    loadingInfoContainer.SetActive(true);
                    loadingInfoTxt.text = Translator.GetTranslationForId(Translation_Script.LOADING_LOADING_DATA);
                    yield return new WaitUntil(() => Database.instance.isInitialized);
                }

                if (!TimeManager.instance.IsOnline())
                {
                    loadingInfoContainer.SetActive(true);
                    loadingInfoTxt.text = Translator.GetTranslationForId(Translation_Script.LOADING_CONNECTING);
                    ShowRandomTip(true);
                    yield return new WaitUntil(TimeManager.instance.IsOnline);
                }

                if (!Advertisement.instance.IsConsentGathered)
                {
                    loadingInfoContainer.SetActive(true);
                    loadingInfoTxt.text = Translator.GetTranslationForId(Translation_Script.LOADING_CONSENT);
                    yield return new WaitUntil(() => Advertisement.instance.IsConsentGathered);
                    yield return DelayWait.halfSecond;
                }

                load.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    public void ShowRandomTip(bool showInternetTip)
    {
        tipTxt.text = Translator.GetTranslationForId(
            showInternetTip
            ?
            Translation_Script.LOADING_TIP_1
            :
            (Translation_Script)UnityEngine.Random.Range((int)Translation_Script.LOADING_TIP_1, 
                (int)((Database.instance.batterySaver ? Translation_Script.LOADING_TIP_11 : Translation_Script.LOADING_TIP_12) + 1))
            );
    }
}