using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

public class CueStickController : MonoBehaviourPun
{
    public enum ShotExecutionMode { StrictRails, HybridRails, LegacyPhysics }

    [Header("References")]
    public Transform cueVisualRoot;
    public Transform cueStick;
    public Transform cueBall;
    public Slider powerSlider;
    public CustomCueBall cueBallScript;
    public CustomTrajectory trajectory;
    public CueSpinUI cueSpinUI;
    public Transform tableRef;

    [Header("Aim")]
    public float rotationSpeed = 300f;
    public float minDistance = 0.2f;
    public float maxDistance = 1.5f;

    [Header("Power")]
    public float minShotPower = 2f;
    public float maxShotPower = 20f;

    [Header("Spin")]
    public float maxSpinRadPerSec = 60f;

    [Header("Hit Animation")]
    public float shootDistance = 0.2f;
    public float shootDuration = 0.05f;
    public float retractDuration = 0.10f;

    [Header("Ball-In-Hand")]
    public bool allowBallInHandDrag = true;
    public BoxCollider placementBounds;
    public LayerMask tableMask = ~0;
    public float dragStartRadius = 0.6f;
    public float dragYOffset = 0f;

    [Header("Shot Mode")]
    public ShotExecutionMode shotMode = ShotExecutionMode.StrictRails;

    // ===== Internals =====
    float currentAngle = 0f;
    bool draggingAim = false;
    Renderer[] allRenderers;
    bool preShotLock = false;

    bool canAim = false;
    bool isPlayerTurn = false;
    bool hasLocalAuthority = false;

    bool ballInHandMode = false;
    bool draggingCueBall = false;
    Vector3 dragHitOffset = Vector3.zero;

    // Online manager (PUN)
    TurnManager8Ball_PUN tmPun;
    // Offline manager
    TurnManager8Ball tmOffline;

    PhotonView _pvController;   // this object's PhotonView (online use)
    PhotonView _pvBall;         // cue-ball PhotonView (online BIH ownership)

    bool IsOfflineLike => (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode);

    void Awake()
    {
        var root = cueVisualRoot ? cueVisualRoot : transform;
        allRenderers = root.GetComponentsInChildren<Renderer>(true);

#if UNITY_2023_1_OR_NEWER
        tmPun = FindFirstObjectByType<TurnManager8Ball_PUN>();
        tmOffline = FindFirstObjectByType<TurnManager8Ball>();
#else
        tmPun = FindObjectOfType<TurnManager8Ball_PUN>();
        tmOffline = FindObjectOfType<TurnManager8Ball>();
#endif

        _pvController = photonView ?? GetComponent<PhotonView>();
        if (cueBall) _pvBall = cueBall.GetComponent<PhotonView>();
    }

    void OnEnable()
    {
        if (IsOfflineLike) GrantOfflineAuthority();
    }

    void Start()
    {
        if (IsOfflineLike) GrantOfflineAuthority();
    }

    void Update()
    {
        if (!cueStick || !cueBall || cueBallScript == null || trajectory == null)
        {
            ForceHidden();
            return;
        }

        if (IsOfflineLike && !hasLocalAuthority)
            GrantOfflineAuthority();

        if (!hasLocalAuthority)
        {
            ForceHidden();
            return;
        }

        bool tableIdle = !cueBallScript.IsAnyBallMovingByTag("Ball");

        // === Ball-in-hand drag (allowed even while preShotLock is true) ===
        if (allowBallInHandDrag && ballInHandMode && isPlayerTurn && tableIdle)
        {
            HandleCueBallDragInput();
            if (draggingCueBall)
            {
                ForceHidden();
                return;
            }
        }

        // === Aim/shot gating ===
        if (!canAim || !isPlayerTurn || preShotLock || !tableIdle)
        {
            ForceHidden();
            return;
        }

        SetCueVisible(true);

        HandleMouseAim();
        UpdateCueTransform();

        // live preview
        Vector3 dir = AimDirFromAngle(currentAngle);
        float power = Mathf.Lerp(minShotPower, maxShotPower, powerSlider ? powerSlider.value : 0f);
        Vector3 omega = cueSpinUI ? cueSpinUI.SpinToWorld(dir, maxSpinRadPerSec) : Vector3.zero;
        trajectory.DrawTrajectoryWithSpin(cueBall.position, dir * power, omega);
    }

    // ===================== Aim & visuals =====================

    void HandleMouseAim()
    {
        bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0) && !overUI) draggingAim = true;
        if (Input.GetMouseButton(0) && draggingAim) currentAngle += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
        if (Input.GetMouseButtonUp(0)) draggingAim = false;
    }

    void UpdateCueTransform()
    {
        float s = powerSlider ? powerSlider.value : 0f;
        float distance = Mathf.Lerp(minDistance, maxDistance, s);

        Vector3 backDir = -AimDirFromAngle(currentAngle);
        Vector3 offset = backDir * distance;

        Vector3 pos = cueBall.position + offset;
        pos.y = cueBall.position.y;
        cueStick.position = pos;

        Vector3 lookAt = cueBall.position;
        lookAt.y = cueStick.position.y;
        cueStick.LookAt(lookAt);
    }

    Vector3 AimDirFromAngle(float angleDeg)
    {
        Vector3 zero = tableRef ? tableRef.forward : Vector3.forward;
        return Quaternion.AngleAxis(angleDeg, Vector3.up) * zero;
    }

    // ===================== SHOOT =====================

    public void Shoot()
    {
        if (!cueBall || cueBallScript == null) return;
        if (!hasLocalAuthority) return;
        if (!canAim || !isPlayerTurn) return;
        if (ballInHandMode) return;
        if (cueBallScript.IsAnyBallMovingByTag("Ball")) return;

        preShotLock = true;
        ForceHidden();

        float angleDeg = currentAngle;
        float power = Mathf.Lerp(minShotPower, maxShotPower, powerSlider ? powerSlider.value : 0f);
        Vector3 dirLocal = AimDirFromAngle(angleDeg);
        Vector3 omega = cueSpinUI ? cueSpinUI.SpinToWorld(dirLocal, maxSpinRadPerSec) : Vector3.zero;

        StartCoroutine(HitRoutine(dirLocal, power, omega));

        bool online = PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode;

        if (online)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplyShotLocally(angleDeg, power, omega, shotMode);
                tmPun?.OnLocalShotFired();
            }
            else
            {
                if (_pvController == null)
                {
                    Debug.LogError("[CueStickController] Missing PhotonView; cannot send shot RPC to Master.");
                    return;
                }
                _pvController.RPC(nameof(RPC_ApplyShotByAngleOnMaster), RpcTarget.MasterClient, angleDeg, power, omega, (int)shotMode);
            }
        }
        else
        {
            // OFFLINE: apply shot AND notify the offline turn manager
            ApplyShotLocally(angleDeg, power, omega, shotMode);
            tmOffline?.OnPlayerShotFired();   // <— this is the missing call that unblocks AI
        }

        if (powerSlider) powerSlider.value = 0f;
    }

    void ApplyShotLocally(float angleDeg, float power, Vector3 omega, ShotExecutionMode mode)
    {
        Vector3 dir = AimDirFromAngle(angleDeg);
        trajectory.DrawTrajectoryWithSpin(cueBall.position, dir * power, omega);

        switch (mode)
        {
            case ShotExecutionMode.StrictRails:
               // cueBallScript.ExecuteStrictShotFromRenderers(trajectory);
                break;
            case ShotExecutionMode.HybridRails:
               // cueBallScript.ExecuteHybridShotFromTrajectory(trajectory);
                break;
            case ShotExecutionMode.LegacyPhysics:
            default:
                cueBallScript.Shoot(dir, power, omega);
                break;
        }
    }

    IEnumerator HitRoutine(Vector3 dir, float power, Vector3 omega)
    {
        Vector3 initial = cueStick.position;
        Vector3 thrust = initial - dir.normalized * shootDistance;

        for (float t = 0f; t < shootDuration; t += Time.deltaTime)
        {
            cueStick.position = Vector3.Lerp(initial, thrust, t / shootDuration);
            yield return null;
        }
        cueStick.position = thrust;

        for (float t = 0f; t < retractDuration; t += Time.deltaTime)
        {
            cueStick.position = Vector3.Lerp(thrust, initial, t / retractDuration);
            yield return null;
        }
        cueStick.position = initial;
    }

    // Online: master applies client’s shot then begins resolution
    [PunRPC]
    void RPC_ApplyShotByAngleOnMaster(float angleDeg, float power, Vector3 omega, int modeOrdinal, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var mode = (ShotExecutionMode)Mathf.Clamp(modeOrdinal, 0, (int)ShotExecutionMode.LegacyPhysics);
        ApplyShotLocally(angleDeg, power, omega, mode);
        tmPun?.OnLocalShotFired();
    }

    // ===================== BIH Drag =====================

    public void SetBallInHand(bool enable)
    {
        ballInHandMode = enable;
        draggingCueBall = false;

        if (enable)
        {
            if (cueBallScript && !cueBallScript.gameObject.activeSelf)
                cueBallScript.gameObject.SetActive(true);

            cueBallScript?.ForceStopNow();
            ForceHidden();

            if (!IsOfflineLike && _pvBall && !_pvBall.AmOwner)
                _pvBall.RequestOwnership();

            preShotLock = false;
        }
        else
        {
            preShotLock = false;
        }
    }

    public void ExitBallInHandLocal()
    {
        draggingCueBall = false;
        ballInHandMode = false;
        preShotLock = false;
    }

    public void ForceExitBallInHandLocal()
    {
        draggingCueBall = false;
        ballInHandMode = false;
        preShotLock = false;
    }

    void HandleCueBallDragInput()
    {
        if (!allowBallInHandDrag) return;

        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonUp(0) && draggingCueBall)
                EndCueBallDrag(finalize: true);
            return;
        }

        if (!draggingCueBall && Input.GetMouseButtonDown(0))
        {
            Vector3 hit;
            if (RaycastTable(out hit))
            {
                float dist = Vector3.Distance(hit.WithY(cueBall.position.y), cueBall.position);
                if (dist <= dragStartRadius)
                    BeginCueBallDrag(hit);
            }
        }

        if (draggingCueBall && Input.GetMouseButton(0))
        {
            Vector3 hit;
            if (RaycastTable(out hit))
            {
                Vector3 target = hit + dragHitOffset;
                target.y = cueBall.position.y + dragYOffset;
                if (placementBounds) target = ClampToBoundsXZ(target, placementBounds);
                MoveCueBallWhileDragging(target);
            }
        }

        if (draggingCueBall && Input.GetMouseButtonUp(0))
            EndCueBallDrag(finalize: true);
    }

    void BeginCueBallDrag(Vector3 hitPoint)
    {
        draggingCueBall = true;
        preShotLock = true;

        Vector3 ballY = cueBall.position.WithY(hitPoint.y);
        dragHitOffset = (cueBall.position - ballY);

        if (!IsOfflineLike && _pvBall && !_pvBall.AmOwner)
            _pvBall.RequestOwnership();
    }

    void MoveCueBallWhileDragging(Vector3 targetPos)
    {
        cueBall.position = targetPos;
    }

    void EndCueBallDrag(bool finalize)
    {
        if (!draggingCueBall) return;
        draggingCueBall = false;

        if (finalize)
        {
            Vector3 finalPos = cueBall.position.WithY(cueBall.position.y - dragYOffset);

            if (IsOfflineLike)
            {
                cueBall.position = finalPos;
                cueBallScript?.OnCueBallPlaced(finalPos);
                // offline: no manager call needed here; placement UX only
            }
            else
            {
                if (_pvController == null)
                {
                    Debug.LogError("[CueStickController] Missing PhotonView; cannot finalize BIH.");
                }
                else
                {
                    _pvController.RPC(nameof(RPC_FinalizeBIHOnMaster), RpcTarget.MasterClient, finalPos);
                }
            }
        }

        ballInHandMode = false;
        preShotLock = false;
    }

    [PunRPC]
    void RPC_FinalizeBIHOnMaster(Vector3 finalPos, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        FinalizeBallPlacementOnMaster(finalPos);
    }

    void FinalizeBallPlacementOnMaster(Vector3 finalPos)
    {
        cueBall.position = finalPos;
        cueBallScript?.OnCueBallPlaced(finalPos);

        if (_pvBall && !_pvBall.AmOwner)
            _pvBall.TransferOwnership(PhotonNetwork.MasterClient);

        tmPun?.OnBallInHandPlacedNetwork();
    }

    // ===================== Turn Manager hooks =====================

    public void SetLocalAuthority(bool hasAuth)
    {
        hasLocalAuthority = hasAuth;
        if (!hasLocalAuthority) ForceHidden();
    }

    public void EnablePlayerTurn()
    {
        preShotLock = false;
        isPlayerTurn = true;
        canAim = true;
    }

    public void DisablePlayerTurn()
    {
        isPlayerTurn = false;
        canAim = false;

        ballInHandMode = false;
        preShotLock = false;

        ForceHidden();
        if (draggingCueBall) EndCueBallDrag(finalize: false);
    }

    // ===================== Utilities =====================

    void GrantOfflineAuthority()
    {
        hasLocalAuthority = true;
        isPlayerTurn = true;
        canAim = true;
        preShotLock = false;
        SetCueVisible(true);
        trajectory?.Hide();
    }

    void ForceHidden()
    {
        SetCueVisible(false);
        trajectory?.Hide();
    }

    void SetCueVisible(bool v)
    {
        if (allRenderers == null) return;
        for (int i = 0; i < allRenderers.Length; i++)
            if (allRenderers[i]) allRenderers[i].enabled = v;
    }

    bool RaycastTable(out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        var cam = Camera.main;
        if (!cam) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, tableMask))
        {
            hitPoint = hit.point;
            return true;
        }

        Plane p = new Plane(Vector3.up, new Vector3(0f, cueBall.position.y, 0f));
        if (p.Raycast(ray, out float dist))
        {
            hitPoint = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    Vector3 ClampToBoundsXZ(Vector3 pos, BoxCollider bounds)
    {
        if (!bounds) return pos;
        var t = bounds.transform;
        Vector3 local = t.InverseTransformPoint(pos);
        Vector3 half = bounds.size * 0.5f;
        local.x = Mathf.Clamp(local.x, bounds.center.x - half.x, bounds.center.x + half.x);
        local.z = Mathf.Clamp(local.z, bounds.center.z - half.z, bounds.center.z + half.z);
        return t.TransformPoint(local);
    }
}

static class Vec3Ext
{
    public static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }
}
