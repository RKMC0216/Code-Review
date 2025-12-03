using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuyCollectorWithSapphireBehaviour : Observer
{
    [SerializeField]
    private Slider milestoneSlider;

    [SerializeField]
    private Image collectorImg;

    [SerializeField]
    private TMP_Text amountTxt, allTxt;

    [SerializeField]
    private Button oneButton, tenButton, hundredButton, allButton;

    [HideInInspector]
    public CollectorBehaviour collector;

    private void Start()
    {
        collectorImg.sprite = Database.instance.activeLocation.GetSpriteForCollector(collector.collector.ID);
        if(collector.collector.amountTotal == 0)
        {
            collectorImg.color = new Color(.2f, .2f, .2f);
        }
        
        UpdateAmount();
        UpdateButtonAvailability();
    }

    private void UpdateAmount()
    {
        milestoneSlider.value = collector.GetMilestoneProgress();
        amountTxt.text = collector.collector.amountTotal.ToString();
    }

    public void UpdateButtonAvailability()
    {
        oneButton.interactable = collector.collector.amountTotal > 0 && Database.instance.Sapphires >= 1;
        tenButton.interactable = collector.collector.amountTotal > 0 && Database.instance.Sapphires >= 10;
        hundredButton.interactable = collector.collector.amountTotal > 0 && Database.instance.Sapphires >= 100;
        allButton.interactable = collector.collector.amountTotal > 0 && Database.instance.Sapphires >= 1;
        allTxt.text = "+" + Database.instance.Sapphires;
    }

    public void OnOneButtonClicked()
    {
        Buy(1);
    }

    public void OnTenButtonClicked()
    {
        Buy(10);
    }

    public void OnHundredButtonClicked()
    {
        Buy(100);
    }

    public void OnAllButtonClicked()
    {
        Buy(Database.instance.Sapphires);
    }

    private void Buy(double amount)
    {
        if(Database.instance.Sapphires >= amount)
        {
            Database.instance.SubstractSapphires(amount, true);
            collector.Buy(amount, false);
            UpdateAmount();
        }
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.sapphiresObservers;
    }

    public override void OnValueChanged()
    {
        UpdateButtonAvailability();
    }
}