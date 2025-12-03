using System.Collections;
using UnityEngine;
using TMPro;

public class StatBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject suffixObj;

    [SerializeField]
    private TMP_Text titleTxt, digitsTxt, suffixTxt;

    public Stat stat;

    private void Start()
    {
        titleTxt.text = stat.title;
    }

    private void OnEnable()
    {
        StartCoroutine(UpdateValue());
    }

    private IEnumerator UpdateValue()
    {
        yield return null;

        if(stat.IsDynamicValue())
        {
            while (true)
            {
                NumberFormatter.FormatInto(stat.valueFunc(), digitsTxt, suffixTxt, suffixObj);
                yield return DelayWait.oneFifthSecond;
            }
        }
        else
        {
            NumberFormatter.FormatInto(stat.value, digitsTxt, suffixTxt, suffixObj);
        }
    }
}