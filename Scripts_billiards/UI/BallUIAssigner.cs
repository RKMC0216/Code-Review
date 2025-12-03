using UnityEngine;
using UnityEngine.UI;

public class BallUIAssigner : MonoBehaviour
{
    public enum Group { Unknown, Solids, Stripes }

    [Header("Slots (where the bars should appear)")]
    public RectTransform leftSlot;   // Player-side slot
    public RectTransform rightSlot;  // AI-side slot

    [Header("Bars (the actual 7-ball rows)")]
    [Tooltip("The UI row that shows balls 1–7 icons")]
    public GameObject solidsBar;
    [Tooltip("The UI row that shows balls 9–15 icons")]
    public GameObject stripesBar;

    [Header("Optional: outlines/frames that sit behind the avatar")]
    public GameObject playerFrameGlow; // e.g., green border
    public GameObject aiFrameGlow;     // e.g., blue border

    [Header("Optional: start hidden until groups chosen")]
    public bool hideBarsOnStart = true;

    Group _playerGroup = Group.Unknown;
    Group _aiGroup = Group.Unknown;
    bool _initialized;

    void Awake()
    {
        if (hideBarsOnStart)
        {
            SafeSetActive(solidsBar, false);
            SafeSetActive(stripesBar, false);
        }
    }

    /// <summary>
    /// Call this once the game has determined who is Solids/Stripes.
    /// Example: uiAssigner.SetGroups(BallUIAssigner.Group.Stripes);   // player got stripes
    /// </summary>
    public void SetGroups(Group playerGroup)
    {
        _playerGroup = playerGroup;
        _aiGroup = (playerGroup == Group.Solids) ? Group.Stripes :
                   (playerGroup == Group.Stripes) ? Group.Solids : Group.Unknown;

        // Nothing to lay out if still unknown
        if (_playerGroup == Group.Unknown || _aiGroup == Group.Unknown)
        {
            LayoutUnknown();
            return;
        }

        // Place bars: by default Solids-left, Stripes-right; swap if needed
        if (_playerGroup == Group.Solids)
        {
            Place(solidsBar, leftSlot, true);
            Place(stripesBar, rightSlot, true);
        }
        else // player is Stripes → swap
        {
            Place(stripesBar, leftSlot, true);
            Place(solidsBar, rightSlot, true);
        }

        // Optional: frames visible now that sides are set
        SafeSetActive(playerFrameGlow, true);
        SafeSetActive(aiFrameGlow, true);

        _initialized = true;
    }

    /// <summary>
    /// Optional helper you can call on new rack/reset to hide everything again.
    /// </summary>
    public void ResetBarsHidden()
    {
        _initialized = false;
        _playerGroup = Group.Unknown;
        _aiGroup = Group.Unknown;

        // Return bars to their original parents? Not required; just hide them.
        SafeSetActive(solidsBar, false);
        SafeSetActive(stripesBar, false);

        SafeSetActive(playerFrameGlow, false);
        SafeSetActive(aiFrameGlow, false);
    }

    // ---------------- private helpers ----------------

    void LayoutUnknown()
    {
        // Hide until assigned
        SafeSetActive(solidsBar, false);
        SafeSetActive(stripesBar, false);

        SafeSetActive(playerFrameGlow, false);
        SafeSetActive(aiFrameGlow, false);
    }

    void Place(GameObject bar, RectTransform slot, bool makeActive)
    {
        if (!bar || !slot) return;

        var rt = bar.transform as RectTransform;
        rt.SetParent(slot, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();

        SafeSetActive(bar, makeActive);
    }

    void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }
}
