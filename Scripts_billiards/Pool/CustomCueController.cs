using UnityEngine;

public class CustomCueController : MonoBehaviour
{
    [Header("Refs")]
    public CustomCueBall cueBall;          // must be the cue ball instance (script attached)
    public CustomTrajectory trajectory;    // the same trajectory used for preview (has LRs)

    [Header("Input / Power")]
    public float maxPower = 5f;            // cap for shot speed
    public float powerScale = 5f;          // pixels-to-speed scale

    [Header("Spin (UI)")]
    [Range(-1f, 1f)] public float sideSpinInput = 0f;     // -1 left, +1 right
    [Range(-1f, 1f)] public float topBackSpinInput = 0f;  // -1 draw, +1 follow
    public float spinMaxOmega = 80f;                      // rad/s scale

    [Header("Quality of life")]
    public bool redrawWhileDragging = true;               // live preview while dragging
    public bool redrawOnRelease = true;                   // ensure preview is fresh before Shoot

    private Vector3 dragStart;
    private bool dragging;

    void Update()
    {
        if (!cueBall || !trajectory) return;

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = GetMousePointOnTable();
            dragging = true;
        }

        if (Input.GetMouseButton(0) && dragging && redrawWhileDragging)
        {
            // live preview as you drag
            Vector3 now = GetMousePointOnTable();
            Vector3 aim = dragStart - now;

            float power = Mathf.Clamp(aim.magnitude * powerScale, 0f, maxPower);
            Vector3 fwd = aim.sqrMagnitude > 1e-8f ? aim.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // spin basis matches CustomCueBall/CustomTrajectory expectation
            Vector3 omegaWorld = right * (sideSpinInput * spinMaxOmega)
                               + fwd * (topBackSpinInput * spinMaxOmega);

            trajectory.DrawTrajectoryWithSpin(cueBall.transform.position, fwd * power, omegaWorld);
        }

        if (Input.GetMouseButtonUp(0) && dragging)
        {
            // Final aim compute (so what you shoot equals what you see)
            Vector3 now = GetMousePointOnTable();
            Vector3 aim = dragStart - now;

            float power = Mathf.Clamp(aim.magnitude * powerScale, 0f, maxPower);
            Vector3 fwd = aim.sqrMagnitude > 1e-8f ? aim.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            Vector3 omegaWorld = right * (sideSpinInput * spinMaxOmega)
                               + fwd * (topBackSpinInput * spinMaxOmega);

            if (redrawOnRelease)
                trajectory.DrawTrajectoryWithSpin(cueBall.transform.position, fwd * power, omegaWorld);

            // This triggers Simple Push Mode inside CustomCueBall (if enabled there)
            cueBall.Shoot(fwd, power, omegaWorld);

            dragging = false;
        }
    }

    Vector3 GetMousePointOnTable()
    {
        var cam = Camera.main;
        if (!cam) return cueBall ? cueBall.transform.position : Vector3.zero;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane table = new Plane(Vector3.up, cueBall ? cueBall.transform.position : Vector3.zero);
        return table.Raycast(ray, out float t) ? ray.GetPoint(t) : (cueBall ? cueBall.transform.position : Vector3.zero);
    }
}
