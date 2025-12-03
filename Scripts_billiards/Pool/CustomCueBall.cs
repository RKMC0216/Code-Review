using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Custom cue/balls controller with:
/// • Fixed-step sim for stability
/// • Simple Push (accurate to trajectory), implemented as real collision then override
/// • Strict/Hybrid rails playback kept
/// • Robust pocket handling (no double triggers, clean scratch)
/// </summary>
public class CustomCueBall : MonoBehaviour
{
    [Header("Physics")]
    public float frictionPerSecond = 0.985f;
    public float spinDecayPerSecond = 0.98f;
    public float ballRadius = 0.057f;
    public LayerMask collisionMask;
    public LayerMask holeLayerMask;

    [Header("Spin Coupling (subtle/visual)")]
    public float sideSpinCurveCoeff = 0.20f;
    public float topBackGainCoeff = 0.15f;

    [Header("Rack Explosion (optional)")]
    public bool enableRackExplosion = true;
    public float rackDetectRadius = 0.22f;
    public int rackMinCount = 6;
    public float rackExplosionEnergyScale = 1.15f;
    public float rackRandomSpread = 0.15f;

    [Header("Pocket FX")]
    public float pocketDuration = 0.6f;
    public float pocketDepth = 0.35f;
    public float spiralStartRadius = 0.08f;
    public float spiralTurns = 2.0f;
    public float wobbleAmplitude = 0.01f;
    public float wobbleHz = 8f;
    public float pocketSpinDegPerSec = 360f;

    [Header("Collision Robustness")]
    public float wallSkin = 0.0015f;
    public float minSweep = 0.0005f;
    public int maxSweepsPerStep = 4;
    public bool collideWithTriggers = false;

    [Header("Ball Identity")]
    public int ballNumber = 0; // 0 = cue

    [Header("Pocket Detect")]
    public float pocketDetectRadiusMult = 1.15f;
    public float pocketRayDepth = 0.12f;
    public bool pocketDebug = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sfxBallWall, sfxBallBall, sfxPocket;
    public AnimationCurve volumeBySpeed;
    public Vector2 pitchJitter = new Vector2(0.98f, 1.03f);
    public float minImpactSpeed = 0.20f, maxImpactSpeed = 8.00f;
    public bool muteSfx = false;

    [Header("Frame-Rate Independence")]
    public bool useFixedStep = true;
    public float simHz = 120f;
    public float maxAccumulatedTime = 0.2f;

    // ===== Simple Push (priority) =====
    [Header("Simple Push (Priority)")]
    public bool simplePushMode = true;
    public CustomTrajectory trajectory;
    public float defaultPushSpeed = 6f;
    public bool stopCueAtContact = true;
    [Range(0f, 1f)] public float cueAfterSpeedFraction = 0.6f;
    public bool fallbackToShootDirection = true;
    public float contactSeparationEpsilon = 0.0010f;

    // ===== Guided playback options (kept) =====
    public enum GuidedMode { Interpolate, SnapPerVertex }
    [Header("Guided Playback Strictness")]
    public GuidedMode guidedMode = GuidedMode.SnapPerVertex;
    public bool useFixedUpdateDuringPlayback = true;
    public float maxPlaybackSegmentLength = 0.01f;
    public bool disableOtherBallCollidersDuringPlayback = true;
    public string ballsTag = "Ball";

    // ===== Events =====
    public static System.Action<int> OnBallPocketed;
    public static System.Action OnCueScratch;
    public static System.Action OnAnyCushionContact;
    public static System.Action<int> OnFirstHitBall;

    // ===== State =====
    [System.NonSerialized] public Vector3 velocity;
    [System.NonSerialized] public Vector3 omega;
    [System.NonSerialized] public bool isMoving = false;

    bool isPocketed = false;
    bool pocketingInProgress = false;    // NEW: prevent double pocket coroutines
    Coroutine pocketCo = null;

    bool rackExplosionTriggered = false;
    bool guidedPlaybackActive = false;
    Collider[] myCols;

    // Simple-Push post-collision override plan
    CustomCueBall plannedHitBall;
    Vector3 plannedObjDir; float plannedObjSpeed;
    bool plannedStopCue; Vector3 plannedCueDir; float plannedCueSpeed;
    bool plannedOverrideActive;

    // fixed-step accumulator
    float accum = 0f;
    float StepDt => (simHz <= 0f ? 1f / 120f : 1f / Mathf.Max(15f, simHz));
    float fixedY => ballRadius;

    public bool IsPocketed => isPocketed;
    public bool IsInGuidedPlayback => guidedPlaybackActive;

    void Awake() { myCols = GetComponentsInChildren<Collider>(true); }
    void Start() { LockY(); }

    void Update()
    {
        if (guidedPlaybackActive) return;

        if (!useFixedStep)
        {
            SimulateVariable(Time.deltaTime);
            return;
        }

        float dt = Mathf.Min(maxAccumulatedTime, Time.deltaTime);
        accum += dt;

        float simDt = StepDt;
        int safety = 0;
        while (accum >= simDt && safety++ < 100)
        {
            SimulateFixed(simDt);
            accum -= simDt;
        }
    }

    // ------------------- Fixed-step physics -------------------
    void SimulateFixed(float dt)
    {
        if (!isPocketed && isMoving) IntegrateWithSubsteps(dt);

        if (velocity.sqrMagnitude > 0f)
            velocity *= Mathf.Pow(Mathf.Clamp01(frictionPerSecond), dt);

        if (omega.sqrMagnitude > 0f)
            omega *= Mathf.Pow(Mathf.Clamp01(spinDecayPerSecond), dt);

        if (velocity.sqrMagnitude < 0.0025f * 0.0025f)
        {
            velocity = Vector3.zero; omega = Vector3.zero; isMoving = false;
            rackExplosionTriggered = false;
        }
    }

    void SimulateVariable(float dt)
    {
        if (isPocketed || !isMoving) return;
        IntegrateWithSubsteps(dt);
        velocity *= Mathf.Clamp01(frictionPerSecond);
        omega *= Mathf.Clamp01(spinDecayPerSecond);

        if (velocity.sqrMagnitude < 0.0025f * 0.0025f)
        {
            velocity = Vector3.zero; omega = Vector3.zero; isMoving = false;
            rackExplosionTriggered = false;
        }
    }

    void IntegrateWithSubsteps(float dt)
    {
        float sq = velocity.sqrMagnitude;
        int sub = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(sq) * 0.3f));
        float dtSub = dt / sub;
        for (int i = 0; i < sub; i++) Step(dtSub);
    }

    void Step(float dt)
    {
        Vector3 pos = transform.position;
        ResolveInitialOverlap(ref pos);

        Vector3 v = velocity;
        float remaining = v.magnitude * dt;
        int sweeps = 0;

        while (remaining > minSweep && sweeps++ < maxSweepsPerStep)
        {
            if (v.sqrMagnitude < 1e-12f) break;
            Vector3 dir = v.normalized;

            RaycastHit hit;
            bool gotHit = collideWithTriggers
                ? Physics.SphereCast(pos, ballRadius, dir, out hit, remaining, collisionMask, QueryTriggerInteraction.Collide)
                : Physics.SphereCast(pos, ballRadius, dir, out hit, remaining, collisionMask);

            if (!gotHit)
            {
                pos += dir * remaining;
                remaining = 0f;
                break;
            }

            pos = hit.point - dir * ballRadius;

            if (hit.collider.CompareTag("Wall"))
            {
                float approach = Mathf.Max(0f, Vector3.Dot(v, -hit.normal));
                PlayWallHit(approach);
                OnAnyCushionContact?.Invoke();

                v = Vector3.Reflect(v, hit.normal);
                pos += hit.normal * wallSkin;
                remaining -= hit.distance;
                if (v.sqrMagnitude < 1e-10f) { remaining = 0f; break; }
                continue;
            }

            if (hit.collider.CompareTag("Ball") && hit.collider.TryGetComponent(out CustomCueBall other))
            {
                Vector3 n = (other.transform.position - pos); n.y = 0f;
                if (n.sqrMagnitude < 1e-10f) n = dir; else n.Normalize();

                if (ballNumber == 0) OnFirstHitBall?.Invoke(other.ballNumber);

                if (enableRackExplosion && !rackExplosionTriggered && TryRackExplosion(other, v))
                {
                    rackExplosionTriggered = true;
                    v *= 0.25f;
                    PlayBallBallHit(v.magnitude);
                }
                else
                {
                    float rel = Vector3.Dot(v - other.velocity, n);
                    if (rel > 0f)
                    {
                        PlayBallBallHit(rel);
                        Vector3 j = n * rel;
                        other.velocity += j;
                        other.isMoving = true;
                        v -= j;
                    }
                }

                // Apply Simple-Push plan right AFTER real contact (keeps rules correct)
                if (plannedOverrideActive && other == plannedHitBall)
                {
                    other.velocity = plannedObjDir * Mathf.Max(0.001f, plannedObjSpeed);
                    other.isMoving = true;
                    other.transform.position += plannedObjDir * contactSeparationEpsilon;
                    other.LockY();

                    if (plannedStopCue) v = Vector3.zero;
                    else v = plannedCueDir.normalized * Mathf.Max(0.001f, plannedCueSpeed);

                    plannedOverrideActive = false; // consume plan
                }

                // tiny depen to avoid instant re-collide
                other.transform.position += n * wallSkin * 1.2f;
                other.LockY();

                remaining = 0f;
                break;
            }

            remaining = 0f;
            break;
        }

        transform.position = pos;
        velocity = v;

        // visuals
        if (velocity.sqrMagnitude > 1e-6f)
        {
            Vector3 rollAxis = Vector3.Cross(Vector3.up, velocity.normalized);
            float rollSpd = velocity.magnitude / Mathf.Max(1e-6f, ballRadius);
            transform.Rotate(rollAxis, rollSpd * Mathf.Rad2Deg * dt, Space.World);
        }

        if (omega.sqrMagnitude > 1e-6f)
            transform.Rotate(omega.normalized, omega.magnitude * Mathf.Rad2Deg * dt, Space.World);

        // very mild spin coupling
        if (velocity.sqrMagnitude > 1e-6f)
        {
            Vector3 fwd = velocity.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            float side = Vector3.Dot(omega, right);
            float along = Vector3.Dot(omega, fwd);
            velocity += right * (side * sideSpinCurveCoeff) * dt * velocity.magnitude;
            velocity += fwd * (along * topBackGainCoeff) * dt;
        }

        LockY();
        CheckHole();
    }

    // ===== Public API =====

    public void OnCueBallPlaced(Vector3 finalPos)
    {
        // Reset pocket state and re-enable
        pocketingInProgress = false;
        plannedOverrideActive = false;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        guidedPlaybackActive = false;
        isPocketed = false;
        isMoving = false;
        velocity = Vector3.zero;
        omega = Vector3.zero;

        var p = finalPos; p.y = fixedY;
        transform.position = p;

        ToggleColliders(true);
        LockY();
    }

    public void Shoot(Vector3 direction, float power, Vector3 omegaWorld)
    {
        if (isPocketed || pocketingInProgress) return;

        if (simplePushMode && trajectory && trajectory.LastHitBall)
        {
            float pushSpeed = (power > 0.001f ? power : defaultPushSpeed);
            SimplePush_PhysicsCollisionThenOverride(trajectory, direction, pushSpeed);
            return;
        }

        // Legacy physics
        velocity = direction.normalized * Mathf.Max(0f, power);
        omega = omegaWorld;
        isMoving = true;
        rackExplosionTriggered = false;
    }

    public bool IsMoving() => isMoving;

    public bool IsAnyBallMovingByTag(string tag)
    {
        const float eps = 0.02f;
        var arr = GameObject.FindGameObjectsWithTag(tag);
        foreach (var go in arr)
        {
            var b = go.GetComponent<CustomCueBall>();
            if (!b || !b.isActiveAndEnabled) continue;
            if (b.guidedPlaybackActive) return true;
            if (b.isMoving && b.velocity.sqrMagnitude > eps * eps) return true;
        }
        return false;
    }

    public void RespawnAt(Vector3 worldPos)
    {
        pocketingInProgress = false;
        plannedOverrideActive = false;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        isPocketed = false; isMoving = false; guidedPlaybackActive = false;
        velocity = omega = Vector3.zero;
        transform.position = worldPos; LockY();
        ToggleColliders(true);
    }

    public void ForceStopNow()
    {
        isMoving = false; velocity = Vector3.zero; omega = Vector3.zero;
        var p = transform.position; p.y = fixedY; transform.position = p;
    }

    // ===== Audio =====
    void PlayWallHit(float approachSpeed)
    {
        if (muteSfx || audioSource == null || sfxBallWall == null) return;
        float norm = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, approachSpeed);
        if (norm <= 0f) return;
        float vol = (volumeBySpeed != null && volumeBySpeed.keys.Length > 0)
            ? volumeBySpeed.Evaluate(norm) : norm;
        audioSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        audioSource.PlayOneShot(sfxBallWall, Mathf.Clamp01(vol));
    }
    void PlayBallBallHit(float relativeSpeed)
    {
        if (muteSfx || audioSource == null || sfxBallBall == null) return;
        float norm = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, relativeSpeed);
        if (norm <= 0f) return;
        float vol = (volumeBySpeed != null && volumeBySpeed.keys.Length > 0)
            ? volumeBySpeed.Evaluate(norm) : norm;
        audioSource.pitch = Random.Range(pitchJitter.x, pitchJitter.y);
        audioSource.PlayOneShot(sfxBallBall, Mathf.Clamp01(vol));
    }

    // ===== Overlap helpers =====
    bool TryClosestPoint(Collider c, Vector3 pos, out Vector3 closest)
    {
        if (c is MeshCollider mc)
        {
            if (mc.convex) { closest = c.ClosestPoint(pos); return true; }
            closest = c.bounds.ClosestPoint(pos); return false;
        }
        if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider)
        {
            closest = c.ClosestPoint(pos); return true;
        }
        closest = c.bounds.ClosestPoint(pos); return false;
    }

    void ResolveInitialOverlap(ref Vector3 pos)
    {
        Collider[] cols = Physics.OverlapSphere(pos, ballRadius * 1.05f, collisionMask);
        foreach (var c in cols)
        {
            if (!c) continue;

            if (c.CompareTag("Wall"))
            {
                Vector3 cp; TryClosestPoint(c, pos, out cp);
                Vector3 n = pos - cp; n.y = 0f;
                if (n.sqrMagnitude > 1e-10f) pos += n.normalized * wallSkin;
            }
            else if (c.CompareTag("Ball") && c.TryGetComponent(out CustomCueBall other) && other != this)
            {
                Vector3 d = pos - other.transform.position; d.y = 0f;
                float dist = d.magnitude, tgt = 2f * ballRadius + wallSkin;
                if (dist < tgt)
                {
                    Vector3 n = (dist > 1e-7f) ? (d / dist) : Vector3.right;
                    pos += n * ((tgt - dist) * 0.5f);
                }
            }
        }
    }

    bool TryRackExplosion(CustomCueBall firstHit, Vector3 currentV)
    {
        Vector3 center = Vector3.zero; int count = 0;
        Collider[] cluster = Physics.OverlapSphere(firstHit.transform.position, rackDetectRadius, collisionMask);
        var list = new System.Collections.Generic.List<CustomCueBall>(16);
        foreach (var col in cluster)
        {
            if (!col || !col.CompareTag("Ball")) continue;
            if (!col.TryGetComponent(out CustomCueBall b)) continue;
            list.Add(b); center += b.transform.position; count++;
        }

        if (count < rackMinCount) return false;
        center /= count;

        float cueSpeed = currentV.magnitude;
        if (cueSpeed <= 0.001f) return false;

        float energyPerBall = cueSpeed * rackExplosionEnergyScale / count;

        foreach (var b in list)
        {
            Vector3 toEdge = (b.transform.position - center);
            float mag = toEdge.magnitude;
            Vector3 dir = (mag > 1e-4f) ? (toEdge / mag) : Random.onUnitSphere;
            dir.y = 0f; dir.Normalize();

            if (rackRandomSpread > 0f)
                dir = (dir + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * rackRandomSpread).normalized;

            b.velocity += dir * energyPerBall;
            b.isMoving = true;
            b.transform.position += dir * 0.005f;
            b.LockY();
        }
        return true;
    }

    void LockY()
    {
        if (isPocketed) return;
        var p = transform.position; p.y = fixedY; transform.position = p;
    }

    // ================== Pocket detection & flow (robust) ==================
    void CheckHole()
    {
        if (isPocketed || pocketingInProgress) return;

        float r = ballRadius * Mathf.Max(0.95f, pocketDetectRadiusMult);
        Collider[] holes = Physics.OverlapSphere(transform.position, r, holeLayerMask, QueryTriggerInteraction.Collide);

        if (holes != null && holes.Length > 0)
        {
            // choose nearest hole center to avoid multiple starts
            Transform best = null; float bestD2 = float.PositiveInfinity;
            foreach (var h in holes)
            {
                if (!h || !h.CompareTag("Hole")) continue;
                float d2 = (h.transform.position - transform.position).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; best = h.transform; }
            }
            if (best)
            {
                StartPocketSequence(best.position, spiral: true);
                return;
            }
        }

        // fallback ray
        Vector3 from = transform.position + Vector3.up * 0.05f;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, Mathf.Max(0.02f, pocketRayDepth), holeLayerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider && hit.collider.CompareTag("Hole"))
            {
                StartPocketSequence(hit.point, spiral: false);
                return;
            }
        }
    }

    void StartPocketSequence(Vector3 holeCenter, bool spiral)
    {
        if (pocketingInProgress || isPocketed) return;

        // Cancel any pending Simple-Push override to avoid wrong post-events
        plannedOverrideActive = false;

        pocketingInProgress = true;
        isPocketed = true;
        isMoving = false;
        velocity = Vector3.zero;
        omega = Vector3.zero;

        // Disable own colliders so we don't keep re-triggering
        ToggleColliders(false);

        // Scratch handling for cue ball; others pocket as normal
        if (ballNumber == 0)
        {
            // Fire scratch once
            OnCueScratch?.Invoke();

            // optional audio
            if (audioSource && sfxPocket) audioSource.PlayOneShot(sfxPocket, 0.9f);

            // hide immediately (avoid weird visuals near pocket)
            if (pocketCo != null) StopCoroutine(pocketCo);
            pocketCo = StartCoroutine(HideCueBallNextFrame());
            return;
        }

        // Non-cue: play spiral or straight drop, then emit OnBallPocketed
        if (pocketCo != null) StopCoroutine(pocketCo);
        pocketCo = StartCoroutine(spiral ? PocketSpiralRoutine(holeCenter) : PocketRoutine());
    }

    IEnumerator HideCueBallNextFrame()
    {
        // Let one frame pass so listeners can safely react
        yield return null;
        if (gameObject) gameObject.SetActive(false);
    }

    IEnumerator PocketSpiralRoutine(Vector3 holeCenter)
    {
        Vector3 start = transform.position; float startY = start.y;
        Vector3 dir0 = (start - holeCenter); dir0.y = 0f; if (dir0.sqrMagnitude < 1e-6f) dir0 = Vector3.forward; else dir0.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir0);

        float Dur = Mathf.Max(0.15f, pocketDuration);
        float startRad = Mathf.Max(0.01f, spiralStartRadius);
        float turns = Mathf.Max(0.5f, spiralTurns);

        if (audioSource && sfxPocket) audioSource.PlayOneShot(sfxPocket, 0.9f);

        for (float t = 0f; t < Dur; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / Dur);
            float eased = 1f - Mathf.Pow(1f - u, 2f);
            float angle = eased * turns * Mathf.PI * 2f;
            float radius = Mathf.Lerp(startRad, 0f, eased);

            Vector3 circle = holeCenter + (Mathf.Cos(angle) * dir0 + Mathf.Sin(angle) * right) * radius;
            float y = Mathf.Lerp(startY, startY - pocketDepth, eased);
            transform.position = new Vector3(circle.x, y, circle.z);
            transform.Rotate(Vector3.up, pocketSpinDegPerSec * Time.deltaTime, Space.World);
            yield return null;
        }

        OnBallPocketed?.Invoke(ballNumber);
        yield return null;
        if (gameObject) gameObject.SetActive(false);
    }

    IEnumerator PocketRoutine()
    {
        if (audioSource && sfxPocket) audioSource.PlayOneShot(sfxPocket, 0.9f);

        float dur = Mathf.Max(0.12f, pocketDuration * 0.6f);
        Vector3 a = transform.position, b = a - Vector3.up * pocketDepth;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(a, b, t / dur);
            yield return null;
        }
        OnBallPocketed?.Invoke(ballNumber);
        yield return null;
        if (gameObject) gameObject.SetActive(false);
    }

    // =======================================================================
    // SIMPLE PUSH that causes REAL COLLISION, then overrides to LR (accurate)
    // =======================================================================
    void SimplePush_PhysicsCollisionThenOverride(CustomTrajectory traj, Vector3 shootDir, float pushSpeed)
    {
        var objTr = traj.LastHitBall;
        if (!objTr)
        {   // Fallback to legacy if no target
            velocity = shootDir.normalized * Mathf.Max(0f, pushSpeed);
            omega = Vector3.zero;
            isMoving = true;
            return;
        }
        var objBall = objTr.GetComponent<CustomCueBall>();
        if (!objBall) return;

        // Object direction (from objectLR, else fallback)
        Vector3 objDir;
        if (!TryGetFirstSegmentDirectionXZ(traj.objectLR, out objDir))
            objDir = (fallbackToShootDirection ? shootDir : Vector3.forward);
        objDir.y = 0f; if (objDir.sqrMagnitude < 1e-8f) objDir = Vector3.forward; objDir.Normalize();

        // Cue-after direction (from cueAfterLR, else opposite of object dir)
        Vector3 cueAfterDir = objDir * -1f;
        TryGetFirstSegmentDirectionXZ(traj.cueAfterLR, out cueAfterDir);
        cueAfterDir.y = 0f; if (cueAfterDir.sqrMagnitude < 1e-8f) cueAfterDir = -objDir; cueAfterDir.Normalize();

        // ✅ Use the FIRST segment of cueBeforeLR so we roll the same way your preview is facing
        Vector3 approachDir = shootDir.sqrMagnitude > 1e-8f ? shootDir.normalized : transform.forward;
        if (traj.cueBeforeLR && traj.cueBeforeLR.positionCount >= 2)
        {
            Vector3 a = traj.cueBeforeLR.GetPosition(0);
            Vector3 b = traj.cueBeforeLR.GetPosition(1);
            approachDir = (b - a); approachDir.y = 0f;
            if (approachDir.sqrMagnitude > 1e-10f) approachDir.Normalize();
        }
        else
        {
            // Fallback aim toward the object ball from the current cue position
            Vector3 guess = (objBall.transform.position - transform.position); guess.y = 0f;
            if (guess.sqrMagnitude > 1e-8f) approachDir = guess.normalized;
        }

        // 🚫 No snapping/teleport: start rolling from the CURRENT position at power-derived speed
        velocity = approachDir * Mathf.Max(0.001f, pushSpeed);
        omega = Vector3.zero;
        isMoving = true;

        // Plan post-collision override (applied immediately AFTER the first real contact in Step())
        plannedHitBall = objBall;
        plannedObjDir = objDir;
        plannedObjSpeed = pushSpeed;
        plannedStopCue = stopCueAtContact;
        plannedCueDir = cueAfterDir;
        plannedCueSpeed = pushSpeed * Mathf.Clamp01(cueAfterSpeedFraction);
        plannedOverrideActive = true;
    }


    // Helpers
    bool TryGetFirstSegmentDirectionXZ(LineRenderer lr, out Vector3 dir)
    {
        dir = Vector3.zero;
        if (!lr || lr.positionCount < 2) return false;
        Vector3 a = lr.GetPosition(0), b = lr.GetPosition(1);
        a.y = 0f; b.y = 0f;
        Vector3 d = b - a; d.y = 0f;
        float m = d.magnitude; if (m < 1e-6f) return false;
        dir = d / m; return true;
    }

    // =======================================================================
    // Guided playback (kept)
    // =======================================================================

    public void ExecuteGuidedShotFromTrajectory(CustomTrajectory traj, float playbackTimeScale = 1f)
    {
        if (traj == null) return;

        var cueBefore = traj.GetCueBeforePointsFlattened(fixedY);
        var cueAfter = traj.GetCueAfterPointsFlattened(fixedY);
        var objPath = traj.GetObjectPointsFlattened(fixedY);
        var hitBallTr = traj.LastHitBall;

        // Densify for smoothness
        cueBefore = ResampleUniform(cueBefore, maxPlaybackSegmentLength);
        cueAfter = ResampleUniform(cueAfter, maxPlaybackSegmentLength);
        objPath = ResampleUniform(objPath, maxPlaybackSegmentLength);

        StopAllCoroutines();
        PrepareForGuidedPlayback(true);
        List<Collider> reenableList = null;

        CustomCueBall obBall = null;
        if (hitBallTr && hitBallTr != transform)
        {
            obBall = hitBallTr.GetComponent<CustomCueBall>();
            if (obBall) obBall.PrepareForGuidedPlayback(true);
        }

        if (disableOtherBallCollidersDuringPlayback)
            reenableList = DisableOtherBallColliders(obBall);

        StartCoroutine(GuidedRoutine(traj, cueBefore, cueAfter, objPath, obBall, playbackTimeScale, reenableList));
    }

    IEnumerator GuidedRoutine(
        CustomTrajectory traj,
        List<Vector3> cueBefore,
        List<Vector3> cueAfter,
        List<Vector3> objectPath,
        CustomCueBall obBall,
        float timeScale,
        List<Collider> toReenable)
    {
        if (cueBefore != null && cueBefore.Count > 0)
            yield return FollowPathPoints(cueBefore, timeScale);

        Coroutine obCo = null, cueCo = null;

        if (obBall != null && objectPath != null && objectPath.Count > 0)
            obCo = StartCoroutine(obBall.FollowPathPoints(objectPath, timeScale));

        if (cueAfter != null && cueAfter.Count > 0)
            cueCo = StartCoroutine(FollowPathPoints(cueAfter, timeScale));

        if (cueCo != null) yield return cueCo;
        if (obCo != null) yield return obCo;

        traj.Hide();

        guidedPlaybackActive = false;
        ForceStopNow();

        if (obBall)
        {
            obBall.guidedPlaybackActive = false;
            obBall.ForceStopNow();
        }

        if (toReenable != null)
            foreach (var c in toReenable) if (c) c.enabled = true;
        ToggleColliders(true);
        if (obBall) obBall.ToggleColliders(true);
    }

    public IEnumerator FollowPathPoints(List<Vector3> worldPoints, float timeScale)
    {
        if (worldPoints == null || worldPoints.Count == 0) yield break;

        Vector3 p0 = worldPoints[0]; p0.y = fixedY;
        transform.position = p0;

        float stepTime = Mathf.Max(0.0001f, CustomTrajectory.LastUsedSimulationStep) / Mathf.Max(0.01f, timeScale);

        for (int i = 1; i < worldPoints.Count; i++)
        {
            Vector3 p = worldPoints[i]; p.y = fixedY;
            RollAlongPath(transform.position, p);
            transform.position = p;
            LockY();

            if (useFixedUpdateDuringPlayback)
            {
                int ticks = Mathf.Max(1, Mathf.RoundToInt(stepTime / Time.fixedDeltaTime));
                for (int k = 0; k < ticks; k++) yield return new WaitForFixedUpdate();
            }
            else
            {
                float t = 0f; while (t < stepTime) { t += Time.deltaTime; yield return null; }
            }
        }
    }

    void RollAlongPath(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; d.y = 0f;
        float dist = d.magnitude;
        if (dist < 1e-6f) return;
        Vector3 dir = d / dist;
        Vector3 axis = Vector3.Cross(Vector3.up, dir);
        float radians = dist / Mathf.Max(1e-6f, ballRadius);
        transform.Rotate(axis, radians * Mathf.Rad2Deg, Space.World);
    }

    public void PrepareForGuidedPlayback(bool disableOwnColliders)
    {
        StopAllCoroutines();
        guidedPlaybackActive = true;
        isMoving = false;
        velocity = Vector3.zero;
        omega = Vector3.zero;
        LockY();
        if (disableOwnColliders) ToggleColliders(false);
    }

    public void ToggleColliders(bool enable)
    {
        if (myCols == null) return;
        foreach (var c in myCols) if (c) c.enabled = enable;
    }

    List<Collider> DisableOtherBallColliders(CustomCueBall alsoMoveThisOne)
    {
        var disabled = new List<Collider>(64);
        var balls = GameObject.FindGameObjectsWithTag(ballsTag);
        for (int bi = 0; bi < balls.Length; bi++)
        {
            var go = balls[bi];
            if (!go) continue;

            var b = go.GetComponent<CustomCueBall>();
            if (!b) continue;
            if (b == this || b == alsoMoveThisOne) continue;

            var cols = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] && cols[i].enabled)
                {
                    cols[i].enabled = false;
                    disabled.Add(cols[i]);
                }
            }
        }
        return disabled;
    }

    // ===== Utilities =====
    static List<Vector3> ResampleUniform(List<Vector3> pts, float maxSegLen)
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
}
