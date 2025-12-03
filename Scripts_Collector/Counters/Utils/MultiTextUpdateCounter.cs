using System.Collections;
using UnityEngine;
using TMPro;

public abstract class MultiTextUpdateCounter : MonoBehaviour
{
    [SerializeField]
    private TMP_Text digitsTxt, suffixTxt;

    [SerializeField]
    private GameObject suffixObj;

    [HideInInspector]
    protected LocationData data;

    protected abstract double CurrentValue();

    private void OnEnable()
    {
        data = Database.instance.activeLocation.data;
        NumberFormatter.FormatInto(CurrentValue(), digitsTxt, suffixTxt, suffixObj);
        StartCoroutine(UpdateText());
    }

    private IEnumerator UpdateText()
    {
        yield return null;

        while (true)
        {
            NumberFormatter.FormatInto(CurrentValue(), digitsTxt, suffixTxt, suffixObj);
            yield return DelayWait.oneFifthSecond;
        }
    }
}