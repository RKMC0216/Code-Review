using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class OfflineIncomeBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject resourcePrefab;

    [SerializeField]
    private GameObject watchVidObj, loadingVidObj;

    [SerializeField]
    private RectTransform resourcesContainer;

    [SerializeField]
    private Button doubleButton;

    [SerializeField]
    private TMP_Text timeTxt;

    [HideInInspector]
    public float time;
    public Dictionary<Mineral, double> income;
    public Action<bool> confirmCallback;

    private void Start()
    {
        timeTxt.text = TimeManager.FormatSeconds(time);
        CreateList();

        if (!Advertisement.instance.IsRewardedAdReady())
        {
            StartCoroutine(WaitForAdReady());
        }
        else
        {
            doubleButton.interactable = true;
        }
    }

    // Only call this function if income hasnt been initialized or set before
    public void SetIncome(Dictionary<Mineral, double> income)
    {
        this.income = income;
        CreateList();
    }

    private void CreateList()
    {
        if (income == null)
        {
            // If income is not yet set, cancel this function
            return;
        }

        if(IncomeIsEmpty())
        {
            // If income is set, but its empty, destroy the pop-up
            Destroy(gameObject);
        }

        foreach (GrantedResource resource in Game.ConvertListOfIncome(income))
        {
            GameObject resourceGO = Instantiate(resourcePrefab, resourcesContainer);
            resourceGO.GetComponent<ResourceBehaviour>().resource = resource;
            resourceGO.GetComponent<ResourceBehaviour>().invertColors = true;
        }
    }

    private bool IncomeIsEmpty()
    {
        foreach(KeyValuePair<Mineral, double> pair in income)
        {
            if(pair.Value > 0)
            {
                return false;
            }
        }

        return true;
    }

    public void OnDoubleButtonClicked()
    {
        confirmCallback?.Invoke(true);
        Destroy(gameObject);
    }

    public void OnContinueButtonClicked()
    {
        confirmCallback?.Invoke(false);
        Destroy(gameObject);
    }

    private IEnumerator WaitForAdReady()
    {
        doubleButton.interactable = false;
        watchVidObj.SetActive(false);
        loadingVidObj.SetActive(true);

        yield return new WaitUntil(Advertisement.instance.IsRewardedAdReady);

        loadingVidObj.SetActive(false);
        watchVidObj.SetActive(true);
        doubleButton.interactable = true;
    }
}