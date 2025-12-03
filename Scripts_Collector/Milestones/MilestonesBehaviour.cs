using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MilestonesBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject milestonePrefab;

    [SerializeField]
    private RectTransform content;

    [SerializeField]
    private TMP_Text totalCompletedTxt;

    private void Start()
    {
        // Apply safe area to the bottom padding of the scrollview's content
        content.GetComponent<VerticalLayoutGroup>().padding.bottom += (int) SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());

        totalCompletedTxt.text = CalcAmountOfCompletedMilestones() + "/" + Database.instance.activeLocation.milestones.Count;

        for(int i = 0; i <= Database.instance.activeLocation.collectors.Count; i++)
        {
            Milestone milestone = Database.instance.activeLocation.GetNextMilestoneForCollector(i);

            if(milestone != null)
            {
                GameObject msGO = Instantiate(milestonePrefab, content);
                msGO.GetComponent<MilestoneBehaviour>().milestone = milestone;
            }
        }
    }

    public static int CalcAmountOfCompletedMilestones()
    {
        int milestonesCompleted = 0;
        foreach (Milestone milestone in Database.instance.activeLocation.milestones)
        {
            if (milestone.isCompleted)
            {
                milestonesCompleted++;
            }
        }

        return milestonesCompleted;
    }
}