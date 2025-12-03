using System.Collections;
using UnityEngine;
using TMPro;

public abstract class UpdateCounter : MonoBehaviour
{
    [SerializeField]
    private TMP_Text text;

    [HideInInspector]
    protected LocationData data;

    protected abstract string CurrentValue();

    private void OnEnable()
    {
        data = Database.instance.activeLocation.data;
        text.text = CurrentValue();
        StartCoroutine(UpdateText());
    }

    private IEnumerator UpdateText()
    {
        yield return null;

        while(true)
        {
            text.text = CurrentValue();
            yield return DelayWait.oneFifthSecond;
        }
    }
}