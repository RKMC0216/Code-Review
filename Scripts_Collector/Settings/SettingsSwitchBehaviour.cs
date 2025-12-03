using UnityEngine;
using TMPro;
using System;

public class SettingsSwitchBehaviour : MonoBehaviour
{
    [SerializeField]
    private TextMultiLang titleTxt;

    [SerializeField]
    private TMP_Text nonTranslatableSubtitleTxt;

    [SerializeField]
    private Switch switchObj;

    [HideInInspector]
    public Translation_Inspector title;
    [HideInInspector]
    public Func<string> nonTranslatableSubtitle;
    public Func<bool> currentValue;
    public Action<bool> changeValue;

    private void Start()
    {
        titleTxt.translationId = title;

        if(nonTranslatableSubtitle != null)
        {
            nonTranslatableSubtitleTxt.gameObject.SetActive(true);
            nonTranslatableSubtitleTxt.text = nonTranslatableSubtitle();
        }
        
        switchObj.IsOn = currentValue();
    }

    public void OnSwitchValueChanged(bool value)
    {
        changeValue?.Invoke(value);
        switchObj.IsOn = currentValue();

        if(nonTranslatableSubtitle != null)
        {
            nonTranslatableSubtitleTxt.text = nonTranslatableSubtitle();
        }
    }
}