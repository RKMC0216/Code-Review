using TMPro;
using UnityEngine;

public class LeaderboardRowUI : MonoBehaviour
{
    public TextMeshProUGUI placeText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI winsText;

    public void Set(int place, string name, int wins)
    {
        if (placeText) placeText.text = $"#{place}";
        if (nameText) nameText.text = name;
        if (winsText) winsText.text = "Wins: "+  wins.ToString();
    }
}