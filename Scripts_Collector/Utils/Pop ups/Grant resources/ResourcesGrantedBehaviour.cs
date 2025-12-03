using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResourcesGrantedBehaviour : GenericPopUpBehaviour
{
    [SerializeField]
    private GameObject resourcePrefab;

    [SerializeField]
    private TMP_Text titleTxt;

    [HideInInspector]
    public string title;
    [HideInInspector]
    public List<GrantedResource> resources;

    private void Start()
    {
        titleTxt.text = title;

        foreach (GrantedResource resource in resources)
        {
            CreateResource(resource);
        }
    }

    private void CreateResource(GrantedResource resource)
    {
        GameObject resourceGO = Instantiate(resourcePrefab, transform);
        resourceGO.GetComponent<ResourceBehaviour>().resource = resource;
    }
}

public class GrantedResource
{
    public Grant resource { get; private set; }
    public double value { get; private set; }

    public GrantedResource(Grant resource, double value)
    {
        this.resource = resource;
        this.value = value;
    }

    public void DoubleValue()
    {
        value = value * 2;
    }
}

public enum Grant
{
    ROCKS,
    SAPPHIRES,
    EMERALDS,
    RUBIES,
    DIAMONDS,
    PRESTIGE_POINTS,

    MULTIPLIER,
    TIME_WARP_24H,
    TIME_WARP_7D,
    TIME_WARP_14D,
    TIME_WARP_30D
}