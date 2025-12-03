using UnityEngine;
using UnityEngine.EventSystems;
using static TurnManager8Ball;

public class CueBallPlacement : MonoBehaviour
{
    [Header("Placement Target")]
    public LayerMask tableMask;

    [Header("Geometry")]
    public float fixedY = 0.057f;
    public float minEdgePadding = 0.06f;
    public float ballRadius = 0.057f;

    [Header("Optional Bounds/Constraints")]
    public BoxCollider tableBounds;
    public KitchenConstraint kitchenConstraint;

    [Header("Ball Lookup")]
    public string ballTag = "Ball";

    Transform cueBall;
    bool pendingBallInHand = false;
    bool placing = false;
    bool kitchenMode = false;

    public void SetBallInHandPending(bool v) => pendingBallInHand = v;
    public bool HasPending() => pendingBallInHand;

    public void BeginBallInHand(Transform cueBallTransform)
    {
        cueBall = cueBallTransform;
        if (!cueBall) { pendingBallInHand = false; placing = false; return; }
        placing = true;
        pendingBallInHand = false;

        if (!cueBall.gameObject.activeSelf) cueBall.gameObject.SetActive(true);
        var p = cueBall.position; p.y = fixedY; cueBall.position = p;

        kitchenMode = false;
    }

    void Update()
    {
        if (!placing || cueBall == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Camera cam = Camera.main;
        if (!cam) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tableMask, QueryTriggerInteraction.Collide))
        {
            Vector3 target = hit.point;
            target.y = fixedY;

            if (tableBounds != null)
                target = ClampToBounds(target, tableBounds, minEdgePadding);

            if (kitchenMode && kitchenConstraint != null)
            {
                if (!kitchenConstraint.Contains(target))
                    target = PullPointInsideKitchen(target);
            }

            target = ResolveBallOverlap(target, 2f * ballRadius);

            cueBall.position = target;

            if (Input.GetMouseButtonDown(0))
            {
                if ((!kitchenMode || kitchenConstraint == null || kitchenConstraint.Contains(target))
                    && !OverlapsOtherBall(target, 2f * ballRadius))
                {
                    placing = false;
                }
            }
        }
    }

    Vector3 ClampToBounds(Vector3 p, BoxCollider bounds, float pad)
    {
        var t = bounds.transform;
        Vector3 local = t.InverseTransformPoint(p);
        Vector3 half = bounds.size * 0.5f;
        float px = Mathf.Max(0f, pad);
        float pz = Mathf.Max(0f, pad);
        local.x = Mathf.Clamp(local.x, -half.x + px, half.x - px);
        local.z = Mathf.Clamp(local.z, -half.z + pz, half.z - pz);
        local.y = 0f;
        return t.TransformPoint(local);
    }

    Vector3 ResolveBallOverlap(Vector3 desired, float minDistance)
    {
        var others = GameObject.FindGameObjectsWithTag(ballTag);
        Vector3 p = desired;

        for (int iter = 0; iter < 6; iter++)
        {
            bool adjusted = false;
            foreach (var go in others)
            {
                if (!go || go.transform == cueBall) continue;
                var b = go.GetComponent<CustomCueBall>();
                if (b == null || !b.gameObject.activeInHierarchy) continue;

                Vector3 d = new Vector3(p.x - go.transform.position.x, 0f, p.z - go.transform.position.z);
                float dist = d.magnitude;
                if (dist < minDistance && dist > 1e-5f)
                {
                    Vector3 n = d / dist;
                    float push = (minDistance - dist) + 0.001f;
                    p += n * push;
                    adjusted = true;
                }
                else if (dist <= 1e-5f)
                {
                    p += new Vector3(minDistance, 0f, 0f);
                    adjusted = true;
                }
            }
            if (!adjusted) break;

            if (tableBounds != null)
                p = ClampToBounds(p, tableBounds, minEdgePadding);
        }

        p.y = fixedY;
        return p;
    }

    bool OverlapsOtherBall(Vector3 desired, float minDistance)
    {
        var others = GameObject.FindGameObjectsWithTag(ballTag);
        foreach (var go in others)
        {
            if (!go || go.transform == cueBall) continue;
            var b = go.GetComponent<CustomCueBall>();
            if (b == null || !b.gameObject.activeInHierarchy) continue;

            Vector3 d = new Vector3(desired.x - go.transform.position.x, 0f, desired.z - go.transform.position.z);
            if (d.sqrMagnitude < (minDistance * minDistance))
                return true;
        }
        return false;
    }

    Vector3 PullPointInsideKitchen(Vector3 p)
    {
        if (kitchenConstraint == null) return p;
        var projected = kitchenConstraint.ProjectInside(p);
        projected.y = fixedY;
        return projected;
    }

    public void EnableKitchenMode(bool on)
    {
        kitchenMode = on && kitchenConstraint != null;
        if (kitchenConstraint != null) kitchenConstraint.EnableKitchenMode(on);
    }
}
