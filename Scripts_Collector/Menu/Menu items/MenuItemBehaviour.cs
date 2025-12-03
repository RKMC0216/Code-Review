using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuItemBehaviour : MonoBehaviour
{
    [SerializeField]
    private RectTransform contentRect, bodyContentRect, fragmentButtonsContainer, headerRect, bodyContainerRect;

    [SerializeField]
    private TextMultiLang titleTxt;

    [SerializeField]
    private GameObject fragmentButtonPrefab;

    [HideInInspector]
    public MenuBehaviour menu;
    [HideInInspector]
    public GameObject contentPrefab;
    [HideInInspector]
    public int overrideInitialFragmentIndex = -1;

    private GenericMenuItem contentItem;
    private List<Button> fragmentButtons;
    private int currentFragmentIndex = 0;

    private void Start()
    {
        ApplySafeArea();

        GameObject content = Instantiate(contentPrefab, bodyContentRect);
        contentItem = content.GetComponent<GenericMenuItem>();
        contentItem.menu = menu;

        if (contentItem.fragments == null || contentItem.fragments.Length == 0)
        {
            titleTxt.gameObject.SetActive(true);
            titleTxt.translationId = contentItem.title;
        }
        else
        {
            titleTxt.gameObject.SetActive(false);
            fragmentButtons = new List<Button>();

            for (int i = 0; i < contentItem.fragments.Length; i++)
            {
                Fragment f = contentItem.fragments[i];

                GameObject fragButton = Instantiate(fragmentButtonPrefab, fragmentButtonsContainer);
                fragButton.GetComponentInChildren<TextMultiLang>().translationId = f.title;
                // 0 is the Image of this GameObject, 1 is the next Image found in the children, etc...
                fragButton.GetComponentsInChildren<Image>()[4].sprite = f.image;

                fragmentButtons.Add(fragButton.GetComponent<Button>());

                // This extra index variable is needed to remove the ref to i
                int index = i;
                fragmentButtons[i].onClick.AddListener(() => ShowFragment(index, true));
            }

            if(overrideInitialFragmentIndex >= 0 && overrideInitialFragmentIndex < contentItem.fragments.Length)
            {
                ShowFragment(overrideInitialFragmentIndex, false);
            }
            else
            {
                if (contentItem.initialFragmentIndex < 0 || contentItem.initialFragmentIndex >= contentItem.fragments.Length)
                {
                    ShowFragment(0, false);
                }
                else
                {
                    ShowFragment(contentItem.initialFragmentIndex, false);
                }
            }
        }

        StartCoroutine(Open());
    }

    private void ApplySafeArea()
    {
        // Set the height of the header and the top distance of the body to comply with the safe area
        headerRect.sizeDelta += new Vector2(0, SafeAreaHelper.PaddingTop(transform.root.GetComponent<RectTransform>()));
        bodyContainerRect.offsetMax = new Vector2(0, -headerRect.sizeDelta.y);
    }

    public void ShowFragment(int index, bool triggerCallback)
    {
        if (contentItem.fragments == null || fragmentButtons == null || index < 0 || index >= contentItem.fragments.Length)
        {
            return;
        }

        for (int i = 0; i < fragmentButtons.Count; i++)
        {
            fragmentButtons[i].interactable = i != index;
            contentItem.fragments[i].content.SetActive(i == index);
        }

        currentFragmentIndex = index;

        if(triggerCallback)
        {
            contentItem.OnFragmentSwitched(index);
        }
    }

    public void OnCloseButtonClick()
    {
        StartCoroutine(Close());
    }

    private bool isAnimating = false;

    private IEnumerator Open()
    {
        if(!isAnimating)
        {
            isAnimating = true;

            yield return TransformAnimationHelper.MoveAnchoredPosition(contentRect, 
                new Vector2(0, -contentRect.rect.height - headerRect.sizeDelta.y), 
                new Vector2(0, -headerRect.sizeDelta.y), 
                .2f);

            isAnimating = false;
            contentItem.OnOpened(currentFragmentIndex);
        }
    }

    private IEnumerator Close()
    {
        if (!isAnimating && contentItem.IsAllowedToClose())
        {
            isAnimating = true;

            yield return TransformAnimationHelper.MoveAnchoredPosition(contentRect, 
                new Vector2(0, -headerRect.sizeDelta.y), 
                new Vector2(0, -contentRect.rect.height - headerRect.sizeDelta.y), 
                .2f);

            isAnimating = false;
            Destroy(gameObject);

            MenuBehaviour.menuItemsOpen--;
            menu.menuNotification.ForceUpdateNotification();

            if(MenuBehaviour.menuItemsOpen == 0)
            {
                // This would be a nice time to show an interstitial ad
                menu.game.InterstitialAdOpportunity();
            }
        }
    }
}