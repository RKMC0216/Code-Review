using UnityEngine;
using System;

public class SettingsButtonBehaviour : MonoBehaviour
{
    [SerializeField]
    private TextMultiLang titleTxt;

    [HideInInspector]
    public Translation_Inspector title;
    public Action callback;

    private void Start()
    {
        titleTxt.translationId = title;
    }

    public void OnButtonClicked()
    {
        callback?.Invoke();
    }
}