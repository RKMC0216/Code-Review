using System.Collections.Generic;
using UnityEngine;

public class SimpleAIOpponent : MonoBehaviour
{
    public enum TargetSet { Any, Solids, Stripes, EightOnly }

    [Header("Refs")]
    public CustomCueBall cueBall;
    public Transform[] pockets;
    public LayerMask collisionMask;
    public TurnManager8Ball turnManager; // optional, to notify after firing

    [Header("Ball Set")]
    public string ballTag = "Ball";
    public TargetSet forceTargetSet = TargetSet.Stripes;   // ← stripes only per request

    [Header("Shot Tuning")]
    public float contactClearRadius = 0.057f;
    public float objPathRadius = 0.057f;
    public float minPower = 4f;
    public float maxPower = 16f;
    public float extraPowerPerMeter = 2.5f;
    public float aimJitterDeg = 0f;

    [Header("Spin (optional)")]
    public bool useSpin = false;
    public float maxSpinRadPerSec = 40f;

    [Header("Debug")]
    public bool verboseLogs = false;

    public void TakeShot()
    {
        if (!cueBall) { Debug.LogWarning("[AI] No cue ball"); return; }
        var balls = GameObject.FindGameObjectsWithTag(ballTag);

        // Count stripes left to know when to shoot 8‑ball
        int stripesLeft = 0;
        Transform eightBall = null;

        List<Transform> targets = new List<Transform>();
        foreach (var go in balls)
        {
            var b = go.GetComponent<CustomCueBall>();
            if (!b || b == cueBall || !go.activeInHierarchy) continue;

            int n = b.ballNumber; // requires the number on each ball
            if (n == 8) { eightBall = go.transform; continue; }

            bool isStripe = (n >= 9 && n <= 15);
            bool isSolid = (n >= 1 && n <= 7);

            if (isStripe) stripesLeft++;

            if (forceTargetSet == TargetSet.Stripes && isStripe) targets.Add(go.transform);
            else if (forceTargetSet == TargetSet.Solids && isSolid) targets.Add(go.transform);
            else if (forceTargetSet == TargetSet.Any && (isSolid || isStripe)) targets.Add(go.transform);
        }

        // If stripes are all down, target the 8‑ball
        if (forceTargetSet == TargetSet.Stripes && stripesLeft == 0 && eightBall != null)
        {
            TryShootTarget(eightBall, pockets, true);
            return;
        }

        if (targets.Count == 0)
        {
            // fallback random
            Fire(RandomAim(), Mathf.Lerp(minPower, maxPower, 0.35f), Vector3.zero);
            return;
        }

        // Choose best (same logic as before)
        Transform bestBall = null;
        Transform bestPocket = null;
        Vector3 bestCueDir = Vector3.zero;
        float bestCost = float.MaxValue;
        float bestTotalDist = 0f;

        foreach (var tBall in targets)
        {
            foreach (var p in pockets)
            {
                Vector3 obVec = p.position - tBall.position; obVec.y = 0f;
                float obDist = obVec.magnitude; if (obDist < 0.2f) continue;
                Vector3 obDir = obVec / obDist;

                Vector3 contactPt = tBall.position - obDir * (2f * contactClearRadius);
                if (!PathClearSphere(tBall.position, obDir, obDist, objPathRadius)) continue;

                Vector3 cueVec = contactPt - cueBall.transform.position; cueVec.y = 0f;
                float cueDist = cueVec.magnitude; if (cueDist < 0.05f) continue;
                Vector3 cueDir = cueVec / cueDist;

                if (!CueToContactClear(cueBall.transform.position, cueDir, cueDist, contactClearRadius, tBall))
                    continue;

                float cutPenalty = 1f - Mathf.Abs(Vector3.Dot(obDir, cueDir));
                float cost = cueDist + obDist * 0.6f + cutPenalty * 0.5f;

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestBall = tBall;
                    bestPocket = p;
                    bestCueDir = cueDir;
                    bestTotalDist = cueDist + obDist;
                }
            }
        }

        if (bestBall == null)
        {
            Fire(RandomAim(), Mathf.Lerp(minPower, maxPower, 0.35f), Vector3.zero);
            return;
        }

        float pwr = Mathf.Clamp(minPower + bestTotalDist * extraPowerPerMeter, minPower, maxPower);
        Vector3 aim = bestCueDir;
        if (aimJitterDeg > 0f) aim = Quaternion.Euler(0f, Random.Range(-aimJitterDeg, aimJitterDeg), 0f) * aim;

        Vector3 spin = Vector3.zero;
        if (useSpin)
        {
            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            spin = right * (maxSpinRadPerSec * 0.25f);
        }

        Fire(aim, pwr, spin);
    }

    bool TryShootTarget(Transform tBall, Transform[] pockets, bool softer = false)
    {
        Transform bestP = null;
        Vector3 bestCueDir = Vector3.zero;
        float bestCost = float.MaxValue, bestTotalDist = 0f;

        foreach (var p in pockets)
        {
            Vector3 obVec = p.position - tBall.position; obVec.y = 0f;
            float obDist = obVec.magnitude; if (obDist < 0.2f) continue;
            Vector3 obDir = obVec / obDist;

            Vector3 contactPt = tBall.position - obDir * (2f * contactClearRadius);
            if (!PathClearSphere(tBall.position, obDir, obDist, objPathRadius)) continue;

            Vector3 cueVec = contactPt - cueBall.transform.position; cueVec.y = 0f;
            float cueDist = cueVec.magnitude; if (cueDist < 0.05f) continue;
            Vector3 cueDir = cueVec / cueDist;

            if (!CueToContactClear(cueBall.transform.position, cueDir, cueDist, contactClearRadius, tBall))
                continue;

            float cutPenalty = 1f - Mathf.Abs(Vector3.Dot(obDir, cueDir));
            float cost = cueDist + obDist * 0.6f + cutPenalty * 0.5f;

            if (cost < bestCost)
            {
                bestCost = cost;
                bestP = p;
                bestCueDir = cueDir;
                bestTotalDist = cueDist + obDist;
            }
        }

        if (bestP == null) return false;

        float pwr = Mathf.Clamp(minPower + bestTotalDist * extraPowerPerMeter, minPower, softer ? (maxPower * 0.8f) : maxPower);
        Fire(bestCueDir, pwr, Vector3.zero);
        return true;
    }

    bool PathClearSphere(Vector3 origin, Vector3 dir, float distance, float radius)
        => !Physics.SphereCast(origin, radius, dir, out _, distance, collisionMask);

    bool CueToContactClear(Vector3 origin, Vector3 dir, float distance, float radius, Transform targetBall)
    {
        if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, distance, collisionMask))
        {
            if (hit.transform == targetBall && hit.distance >= distance - (radius * 1.25f)) return true;
            return false;
        }
        return true;
    }

    void Fire(Vector3 aim, float power, Vector3 omegaWorld)
    {
        cueBall.Shoot(aim, power, omegaWorld);
        turnManager?.OnAIShotFired();
    }

    Vector3 RandomAim()
    {
        Vector3 v = Random.insideUnitSphere; v.y = 0f;
        if (v.sqrMagnitude < 1e-6f) v = Vector3.forward;
        return v.normalized;
    }
}
