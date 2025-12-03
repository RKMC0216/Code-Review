using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class Travel : MonoBehaviour
{
    public static LocationMetaData location;

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private TMP_Text progressTxt;

    [SerializeField]
    private Image loadingImg1, loadingImg2;

    private void Start()
    {
        // Set active location in Database to the location that is being traveled to
        Database.instance.activeLocation = Locations.CreateLocationForId(location.ID, Database.instance.locationDatas[location.ID]);

        loadingImg1.sprite = location.GetSpriteForLocation();
        loadingImg2.sprite = location.GetSpriteForLocation();

        StartCoroutine(LoadLocation());
    }

    private IEnumerator LoadLocation()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(Loading.SCENE_ID_GAME);
        load.allowSceneActivation = false;

        float progress = 0;
        while(progress < 1)
        {
            progress += Time.deltaTime / 1.5f;

            if (progress > load.progress + 0.1f)
            {
                progress = load.progress + 0.1f;
            }

            progressSlider.value = progress;
            progressTxt.text = Math.Ceiling(progress * 100) + "%";

            yield return null;
        }

        yield return DelayWait.oneFifthSecond;
        load.allowSceneActivation = true;
    }
}