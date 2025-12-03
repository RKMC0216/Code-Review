using UnityEngine;
using UnityEngine.UI;
public class ProfileSelect : MonoBehaviour
{
    [SerializeField] Image profileImage;
    Image sprite;
    private void Awake()
    {
        sprite = GetComponent<Image>();
    }
    public void SetProfileImage()
    {
            sprite.sprite = GetComponent<Image>().sprite; // Assuming you want to get the sprite from a SpriteRenderer component
            profileImage.sprite = sprite.sprite;
    }
}
