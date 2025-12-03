using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays pocket-state for balls 1..15.
/// Works offline (listens to CustomCueBall.OnBallPocketed on the same client),
/// and also supports network control via TurnManager RPCs:
///   MarkPocketed(int n), UnmarkPocketed(int n), ResetAll().
/// 
/// Hook-up:
/// - Assign iconsByNumber[1..15] to the matching UI Images for balls 1..15.
/// - Alpha is used to indicate "pocketed" vs "still on table".
/// </summary>
public class BallIconsUI : MonoBehaviour
{
    [Tooltip("Index 0 unused. Assign icons 1..15 to their matching ball numbers.")]
    public Image[] iconsByNumber = new Image[16];

    [Header("Alpha")]
    [Range(0, 255)] public int defaultAlpha = 120;   // semi
    [Range(0, 255)] public int pocketedAlpha = 255;  // solid / fully visible

    void OnEnable()
    {
        // Initialize to default
        ApplyAlphaToAll((byte)defaultAlpha);

        // Offline / host-authoritative fallback: only fires where physics run.
        CustomCueBall.OnBallPocketed += HandlePocketedEvent;
    }

    void OnDisable()
    {
        CustomCueBall.OnBallPocketed -= HandlePocketedEvent;
    }

    // --- Event handler (offline / host) ---
    void HandlePocketedEvent(int ballNumber) => MarkPocketed(ballNumber);

    // --- Public API (network-driven) ---
    public void MarkPocketed(int ballNumber)
    {
        if (!Valid(ballNumber)) return;
        var img = iconsByNumber[ballNumber];
        if (!img) return;
        var c = img.color; c.a = pocketedAlpha / 255f; img.color = c;
    }

    public void UnmarkPocketed(int ballNumber)
    {
        if (!Valid(ballNumber)) return;
        var img = iconsByNumber[ballNumber];
        if (!img) return;
        var c = img.color; c.a = defaultAlpha / 255f; img.color = c;
    }

    public void ResetAll() => ApplyAlphaToAll((byte)defaultAlpha);

    // --- Helpers ---
    void ApplyAlphaToAll(byte a)
    {
        float af = a / 255f;
        for (int i = 1; i < iconsByNumber.Length; i++)
        {
            var img = iconsByNumber[i];
            if (!img) continue;
            var c = img.color; c.a = af; img.color = c;
        }
    }

    bool Valid(int n) => n >= 1 && n < iconsByNumber.Length;
}
