using UnityEngine;
using UnityEngine.EventSystems;

public class CueSpinUI : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("UI")]
    public RectTransform cueBallIcon;  // circular area
    public RectTransform redDot;       // movable dot

    [Header("Output (normalized -1..1)")]
    public Vector2 spin;               // x: left/right, y: top/back

    float radius;

    void Awake()
    {
        if (cueBallIcon != null)
        {
            var r = cueBallIcon.rect;
            radius = Mathf.Min(r.width, r.height) * 0.5f;
        }
    }

    public void OnPointerDown(PointerEventData eventData) => UpdateDot(eventData);
    public void OnDrag(PointerEventData eventData) => UpdateDot(eventData);

    void UpdateDot(PointerEventData data)
    {
        if (cueBallIcon == null || redDot == null) return;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            cueBallIcon, data.position, data.pressEventCamera, out local);

        // Clamp inside the circle
        Vector2 clamped = local;
        if (clamped.magnitude > radius) clamped = clamped.normalized * radius;

        redDot.anchoredPosition = clamped;

        // Normalize to -1..1
        spin = clamped / radius; // x: left/right, y: up is topspin, down is backspin
    }

    /// Convert the 2D spin into a world angular velocity vector (rad/s),
    /// aligned with the given shot direction on the table.
    public Vector3 SpinToWorld(Vector3 shotDir, float spinStrengthRadPerSec)
    {
        Vector3 fwd = shotDir.normalized;                 // topspin/backspin axis
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized; // sidespin axis

        // y (up in UI) = topspin (+) / backspin (-)
        // x (right in UI) = right english (+) / left (-)
        Vector3 omega = fwd * spin.y + right * spin.x;
        return omega * spinStrengthRadPerSec; // radians per second
    }
}
