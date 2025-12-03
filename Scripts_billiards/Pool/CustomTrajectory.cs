using UnityEngine;
using System.Collections.Generic;

public class CustomTrajectory : MonoBehaviour
{
    [Header("Line Renderers")]
    public LineRenderer cueBeforeLR;   // cue before impact
    public LineRenderer cueAfterLR;    // cue after impact
    public LineRenderer objectLR;      // object ball after impact
    public LineRenderer contactRingLR; // visual ring at contact

    [Header("Simulation")]
    public float ballRadius = 0.057f;
    public float simulationStep = 0.02f;
    public int maxSteps = 150;
    public float simulationFriction = 0.985f;
    public float spinPreviewDecay = 0.98f;
    public LayerMask collisionMask;
    public float yOffset = 0.01f;

    [Header("Spin Coupling (match physics)")]
    public float sideSpinCurveCoeff = 0.20f;
    public float topBackGainCoeff = 0.15f;

    [Header("Impact Throw")]
    public float throwCoeff = 0.04f; // cue side-spin → OB lateral throw

    [Header("Bounce Limits")]
    public int cueBeforeMaxBounces = 2;
    public int cueAfterMaxBounces = 1;
    public int objectMaxBounces = 1;

    [Header("Uniform Path Density")]
    [Tooltip("Max physical distance between consecutive preview points.")]
    public float maxPreviewSegmentLength = 0.01f; // 1 cm

    [Header("Post-Impact Preview Limits")]
    public bool limitCueAfterPreview = true;
    public float cueAfterPreviewDistance = 0.35f;
    public bool limitObjectPreview = true;
    public float objectPreviewDistance = 0.35f;

    [Header("Ghost Ball (first-trajectory contact)")]
    [Tooltip("Ghost ball prefab (Sphere). If empty, a temporary sphere will be created at runtime.")]
    public GameObject ghostBallPrefab;
    [Tooltip("Multiplier relative to THIS cue ball's current localScale.")]
    public float ghostBallScale = 1.0f;
    public Color ghostBallColor = new Color(1f, 1f, 1f, 0.25f);
    public bool showGhostThroughLine = true;
    public float ghostThroughHalfLength = 0.05f;
    public bool showGhostSideTick = true;
    public float ghostSideTickLength = 0.025f;
    public LineRenderer ghostThroughLR;
    public LineRenderer ghostSideLR;

    // Exports for guided playback
    public List<Vector3> LastCueBeforePts { get; private set; } = new List<Vector3>();
    public List<Vector3> LastCueAfterPts { get; private set; } = new List<Vector3>();
    public List<Vector3> LastObjectPts { get; private set; } = new List<Vector3>();
    public Vector3? LastImpactPoint { get; private set; }
    public Transform LastHitBall { get; private set; }

    // The cue ball reads this to time playback
    public static float LastUsedSimulationStep { get; private set; } = 0.02f;

    // NEW: post-impact speeds exported for time-accurate rails
    public float LastCueAfterSpeed { get; private set; } = 0f; // m/s
    public float LastObjectSpeed { get; private set; } = 0f; // m/s

    // runtime ghost objects
    Transform ghostBall;
    Material ghostBallMat;

    void Awake()
    {
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        EnsureGhostVisuals();
    }

    // ---------- Public API ----------
    public void DrawTrajectory(Vector3 startPos, Vector3 velocity)
    {
        DrawTrajectoryWithSpin(startPos, velocity, Vector3.zero);
    }

    public void DrawTrajectoryWithSpin(Vector3 startPos, Vector3 velocity, Vector3 omegaWorld)
    {
        ClearAll();
        LastUsedSimulationStep = Mathf.Max(0.0001f, simulationStep);
        LastCueAfterSpeed = 0f;
        LastObjectSpeed = 0f;

        // 1) Trace cue BEFORE impact (spin-aware)
        var before = new List<Vector3>();
        Vector3 pos = startPos;
        Vector3 vel = velocity;
        Vector3 omg = omegaWorld;

        before.Add(pos + Vector3.up * yOffset);

        Vector3? impactPoint = null;
        Vector3 impactNormal = Vector3.zero;
        Transform hitBall = null;
        Vector3 incomingDirAtContact = Vector3.zero;

        int bounces = 0;

        for (int i = 0; i < maxSteps && vel.magnitude > 0.05f; i++)
        {
            Vector3 step = vel * simulationStep;
            Vector3 dir = vel.normalized;

            if (Physics.SphereCast(pos, ballRadius, dir, out RaycastHit hit, step.magnitude, collisionMask))
            {
                // Cue center at contact
                Vector3 cueCenterAtContact = hit.point - dir * ballRadius;
                pos = cueCenterAtContact;
                before.Add(pos + Vector3.up * yOffset);

                if (hit.collider.CompareTag("Ball"))
                {
                    impactPoint = hit.point;
                    incomingDirAtContact = dir;

                    // Robust contact normal: center-to-center at contact
                    Vector3 obCenter = hit.collider.transform.position;
                    impactNormal = (obCenter - cueCenterAtContact); impactNormal.y = 0f;
                    if (impactNormal.sqrMagnitude < 1e-8f) impactNormal = dir;
                    else impactNormal.Normalize();

                    hitBall = hit.collider.transform;

                    // Seal BEFORE array with exact contact center
                    before[before.Count - 1] = cueCenterAtContact + Vector3.up * yOffset;

                    // ---- GHOST BALL VISUALS ----
                    DrawGhostBall(cueCenterAtContact, incomingDirAtContact, impactNormal, obCenter);
                    break;
                }

                if (hit.collider.CompareTag("Wall"))
                {
                    bounces++;
                    if (bounces >= cueBeforeMaxBounces) break;
                    vel = Vector3.Reflect(vel, hit.normal);
                    pos += hit.normal * 0.001f;
                }
                else break;
            }
            else
            {
                pos += step;
                before.Add(pos + Vector3.up * yOffset);
            }

            // preview swerve + follow/draw
            if (vel.sqrMagnitude > 1e-6f)
            {
                Vector3 fwd = vel.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float side = Vector3.Dot(omg, right);
                float along1 = Vector3.Dot(omg, fwd);
                vel += right * (side * sideSpinCurveCoeff) * simulationStep * vel.magnitude;
                vel += fwd * (along1 * topBackGainCoeff) * simulationStep;
            }

            vel *= simulationFriction;
            omg *= spinPreviewDecay;
        }

        // Densify BEFORE
        var beforeDense = ResampleUniform(before, maxPreviewSegmentLength);

        if (cueBeforeLR)
        {
            cueBeforeLR.enabled = true;
            cueBeforeLR.positionCount = beforeDense.Count;
            cueBeforeLR.SetPositions(beforeDense.ToArray());
        }
        LastCueBeforePts = RemoveYOffset(beforeDense);

        LastImpactPoint = impactPoint;
        LastHitBall = hitBall;

        if (!impactPoint.HasValue || hitBall == null)
        {
            LastCueAfterPts.Clear();
            LastObjectPts.Clear();
            if (contactRingLR) { contactRingLR.enabled = false; contactRingLR.positionCount = 0; }
            HideGhostBall();
            return;
        }

        // 2) Contact ring
        DrawContactRing(impactPoint.Value, hitBall.position, 24);

        // 3) Decompose cue velocity at impact (using robust normal)
        Vector3 n = impactNormal;      // normalized
        Vector3 v = vel;               // cue vel at last step

        float vNmag = Mathf.Max(0f, Vector3.Dot(v, n));
        Vector3 vN = vNmag * n;        // normal to OB
        Vector3 vT = v - vN;           // tangential for cue

        // OB gets side-throw from cue side spin
        Vector3 tAxis = Vector3.Cross(Vector3.up, n).normalized;
        float sideSpin = Vector3.Dot(omg, tAxis);
        Vector3 obVel = vN + tAxis * (sideSpin * throwCoeff * v.magnitude);

        // Cue after with slight follow/draw
        float along = (v.sqrMagnitude > 1e-6f) ? Vector3.Dot(omg, v.normalized) : 0f;
        Vector3 cueAfterVel = vT - n * (along * topBackGainCoeff * 0.5f);

        // Export starting speeds for time-accurate rails
        LastCueAfterSpeed = cueAfterVel.magnitude;
        LastObjectSpeed = obVel.magnitude;

        // Centers at contact
        Vector3 cueContactCenter = impactPoint.Value - n * ballRadius;
        Vector3 obStart = hitBall.position + n * ballRadius;

        // 4) Object path
        var obPtsRaw = TracePath(obStart, obVel, Vector3.zero, objectMaxBounces);
        var obPts = ResampleUniform(AddYOffset(obPtsRaw), maxPreviewSegmentLength);

        if (limitObjectPreview && objectPreviewDistance > 0f)
            obPts = TrimPathByDistance(obPts, objectPreviewDistance);

        if (objectLR)
        {
            objectLR.enabled = true;
            objectLR.positionCount = obPts.Count;
            objectLR.SetPositions(obPts.ToArray());
        }
        LastObjectPts = RemoveYOffset(obPts);

        // 5) Cue after path
        var cueAfterRaw = TracePath(cueContactCenter, cueAfterVel, Vector3.zero, cueAfterMaxBounces);
        var cueAfterPts = ResampleUniform(AddYOffset(cueAfterRaw), maxPreviewSegmentLength);

        if (limitCueAfterPreview && cueAfterPreviewDistance > 0f)
            cueAfterPts = TrimPathByDistance(cueAfterPts, cueAfterPreviewDistance);

        if (cueAfterLR)
        {
            cueAfterLR.enabled = true;
            cueAfterLR.positionCount = cueAfterPts.Count;
            cueAfterLR.SetPositions(cueAfterPts.ToArray());
        }
        LastCueAfterPts = RemoveYOffset(cueAfterPts);
    }

    public void Hide()
    {
        if (cueBeforeLR) cueBeforeLR.enabled = false;
        if (cueAfterLR) cueAfterLR.enabled = false;
        if (objectLR) objectLR.enabled = false;
        if (contactRingLR) contactRingLR.enabled = false;
        HideGhostBall();
    }

    // ---------- Helpers ----------
    void ClearAll()
    {
        LastCueBeforePts.Clear();
        LastCueAfterPts.Clear();
        LastObjectPts.Clear();
        LastImpactPoint = null;
        LastHitBall = null;
        LastCueAfterSpeed = 0f;
        LastObjectSpeed = 0f;

        if (cueBeforeLR) { cueBeforeLR.positionCount = 0; cueBeforeLR.enabled = false; }
        if (cueAfterLR) { cueAfterLR.positionCount = 0; cueAfterLR.enabled = false; }
        if (objectLR) { objectLR.positionCount = 0; objectLR.enabled = false; }
        if (contactRingLR) { contactRingLR.positionCount = 0; contactRingLR.enabled = false; }

        HideGhostBall();
    }

    List<Vector3> TracePath(Vector3 start, Vector3 velocity, Vector3 omega, int maxBounces)
    {
        var pts = new List<Vector3>();
        Vector3 pos = start;
        Vector3 vel = velocity;
        Vector3 omg = omega;

        pts.Add(pos);

        int b = 0;

        for (int i = 0; i < maxSteps && vel.magnitude > 0.05f; i++)
        {
            Vector3 step = vel * simulationStep;
            Vector3 dir = vel.normalized;

            if (Physics.SphereCast(pos, ballRadius, dir, out RaycastHit hit, step.magnitude, collisionMask))
            {
                pos = hit.point - dir * ballRadius;
                pts.Add(pos);

                if (hit.collider.CompareTag("Ball")) break;

                b++; if (b >= maxBounces) break;
                vel = Vector3.Reflect(vel, hit.normal);
                pos += hit.normal * 0.001f;
            }
            else
            {
                pos += step;
                pts.Add(pos);
            }

            if (vel.sqrMagnitude > 1e-6f)
            {
                Vector3 fwd = vel.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float side = Vector3.Dot(omg, right);
                float along = Vector3.Dot(omg, fwd);
                vel += right * (side * sideSpinCurveCoeff) * simulationStep * vel.magnitude;
                vel += fwd * (along * topBackGainCoeff) * simulationStep;
            }

            vel *= simulationFriction;
            omg *= spinPreviewDecay;
        }

        return pts;
    }

    void DrawContactRing(Vector3 impactPoint, Vector3 ballCenter, int segments)
    {
        if (!contactRingLR) return;
        contactRingLR.enabled = true;
        if (segments < 8) segments = 8;

        var pts = new Vector3[segments + 1];
        float step = 360f / segments;

        Vector3 center = ballCenter; center.y = impactPoint.y;
        float r = ballRadius;

        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Deg2Rad * (i * step);
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r) + center;
            pts[i] = p + Vector3.up * yOffset;
        }
        contactRingLR.positionCount = pts.Length;
        contactRingLR.SetPositions(pts);
    }

    // --- GHOST BALL: creation & drawing ---
    void EnsureGhostVisuals()
    {
        // Spawn ghost sphere (if not provided)
        if (!ghostBall && ghostBallPrefab)
        {
            ghostBall = Instantiate(ghostBallPrefab, transform).transform;
            ghostBall.name = "[GhostBall]";
            ghostBall.gameObject.SetActive(false);
        }
        else if (!ghostBall)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "[GhostBall]";
            sphere.transform.SetParent(transform, false);
            // remove collider to avoid interfering with physics
            var col = sphere.GetComponent<Collider>(); if (col) Destroy(col);
            ghostBall = sphere.transform;
            ghostBall.gameObject.SetActive(false);
        }

        // Ghost material (URP-safe). Fallback if URP not present.
        if (ghostBall && !ghostBallMat)
        {
            var r = ghostBall.GetComponentInChildren<MeshRenderer>();
            if (r)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (!s) s = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                ghostBallMat = new Material(s);
                ghostBallMat.color = ghostBallColor;
                r.sharedMaterial = ghostBallMat;
            }
        }

        // Through-line LR
        if (!ghostThroughLR)
        {
            var go = new GameObject("[GhostThroughLR]");
            go.transform.SetParent(transform, false);
            ghostThroughLR = go.AddComponent<LineRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (!s) s = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            ghostThroughLR.material = new Material(s);
            ghostThroughLR.widthMultiplier = 0.01f;
            ghostThroughLR.positionCount = 0;
            ghostThroughLR.enabled = false;
            ghostThroughLR.numCapVertices = 4;
        }

        // Side-tick LR
        if (!ghostSideLR)
        {
            var go = new GameObject("[GhostSideLR]");
            go.transform.SetParent(transform, false);
            ghostSideLR = go.AddComponent<LineRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (!s) s = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            ghostSideLR.material = new Material(s);
            ghostSideLR.widthMultiplier = 0.01f;
            ghostSideLR.positionCount = 0;
            ghostSideLR.enabled = false;
            ghostSideLR.numCapVertices = 4;
        }
    }

    void DrawGhostBall(Vector3 cueContactCenter, Vector3 incomingDir, Vector3 contactNormal, Vector3 objectCenter)
    {
        EnsureGhostVisuals();

        // Place + scale the ghost sphere at cue center on contact
        if (ghostBall)
        {
            Vector3 p = cueContactCenter; p.y += yOffset;
            ghostBall.position = p;

            // Auto-match THIS cue ball's scale (script is attached to cue ball)
            ghostBall.localScale = Vector3.Scale(transform.localScale, Vector3.one * Mathf.Max(0.001f, ghostBallScale));

            ghostBall.gameObject.SetActive(true);
            if (ghostBallMat) ghostBallMat.color = ghostBallColor;
        }

        // Small line through the ghost ball along incoming direction
        if (showGhostThroughLine && ghostThroughLR)
        {
            Vector3 dir = (incomingDir.sqrMagnitude > 1e-8f) ? incomingDir.normalized : Vector3.forward;
            float half = Mathf.Max(0.001f, ghostThroughHalfLength);
            Vector3 a = cueContactCenter - dir * half + Vector3.up * yOffset;
            Vector3 b = cueContactCenter + dir * half + Vector3.up * yOffset;

            ghostThroughLR.enabled = true;
            ghostThroughLR.positionCount = 2;
            ghostThroughLR.SetPosition(0, a);
            ghostThroughLR.SetPosition(1, b);
        }
        else if (ghostThroughLR)
        {
            ghostThroughLR.enabled = false;
            ghostThroughLR.positionCount = 0;
        }

        // Side tick: indicate side (tangent) being struck
        if (showGhostSideTick && ghostSideLR)
        {
            Vector3 n = (contactNormal.sqrMagnitude > 1e-8f) ? contactNormal.normalized : Vector3.forward;
            Vector3 tAxis = Vector3.Cross(Vector3.up, n).normalized;
            float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(incomingDir, n), Vector3.up));
            Vector3 sideDir = (sign >= 0f ? tAxis : -tAxis);

            float len = Mathf.Max(0.001f, ghostSideTickLength);
            Vector3 c = cueContactCenter + Vector3.up * yOffset;
            Vector3 s0 = c;
            Vector3 s1 = c + sideDir * len;

            ghostSideLR.enabled = true;
            ghostSideLR.positionCount = 2;
            ghostSideLR.SetPosition(0, s0);
            ghostSideLR.SetPosition(1, s1);
        }
        else if (ghostSideLR)
        {
            ghostSideLR.enabled = false;
            ghostSideLR.positionCount = 0;
        }
    }

    void HideGhostBall()
    {
        if (ghostBall) ghostBall.gameObject.SetActive(false);
        if (ghostThroughLR) { ghostThroughLR.enabled = false; ghostThroughLR.positionCount = 0; }
        if (ghostSideLR) { ghostSideLR.enabled = false; ghostSideLR.positionCount = 0; }
    }

    // Resampling (uniform segment length)
    public List<Vector3> ResampleUniform(List<Vector3> pts, float maxSegLen)
    {
        var outPts = new List<Vector3>();
        if (pts == null || pts.Count == 0) return outPts;
        outPts.Add(pts[0]);

        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = outPts[outPts.Count - 1];
            Vector3 b = pts[i];
            float dist = Vector3.Distance(a, b);

            if (dist <= maxSegLen) { outPts.Add(b); continue; }

            int slices = Mathf.CeilToInt(dist / maxSegLen);
            for (int s = 1; s <= slices; s++)
            {
                float t = (float)s / slices;
                Vector3 p = Vector3.Lerp(a, b, t);
                if (Vector3.Distance(outPts[outPts.Count - 1], p) > 1e-5f)
                    outPts.Add(p);
            }
        }
        return outPts;
    }

    // Trim path to an exact max distance from its start
    List<Vector3> TrimPathByDistance(List<Vector3> pts, float maxDistance)
    {
        if (pts == null || pts.Count == 0) return new List<Vector3>();
        if (maxDistance <= 0f) return new List<Vector3> { pts[0] };

        var outPts = new List<Vector3> { pts[0] };
        float acc = 0f;

        for (int i = 1; i < pts.Count; i++)
        {
            Vector3 a = outPts[outPts.Count - 1];
            Vector3 b = pts[i];
            float seg = Vector3.Distance(a, b);

            if (acc + seg < maxDistance - 1e-6f)
            {
                outPts.Add(b);
                acc += seg;
            }
            else
            {
                float remaining = Mathf.Max(0f, maxDistance - acc);
                if (seg > 1e-6f)
                {
                    float t = remaining / seg;
                    Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(t));
                    if (Vector3.Distance(outPts[outPts.Count - 1], p) > 1e-6f)
                        outPts.Add(p);
                }
                break;
            }
        }

        return outPts;
    }

    List<Vector3> AddYOffset(List<Vector3> pts)
    {
        var list = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
            list.Add(pts[i] + Vector3.up * yOffset);
        return list;
    }

    List<Vector3> RemoveYOffset(List<Vector3> ptsWithOffset)
    {
        var outPts = new List<Vector3>(ptsWithOffset.Count);
        for (int i = 0; i < ptsWithOffset.Count; i++)
        {
            var p = ptsWithOffset[i]; p.y = 0f;
            outPts.Add(p);
        }
        return outPts;
    }

    // Flatten for playback (force Y later)
    public List<Vector3> GetCueBeforePointsFlattened(float y) => Flatten(LastCueBeforePts, y);
    public List<Vector3> GetCueAfterPointsFlattened(float y) => Flatten(LastCueAfterPts, y);
    public List<Vector3> GetObjectPointsFlattened(float y) => Flatten(LastObjectPts, y);
    List<Vector3> Flatten(List<Vector3> pts, float y)
    {
        var outPts = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++) { var p = pts[i]; p.y = y; outPts.Add(p); }
        return outPts;
    }
}
