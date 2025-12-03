// EnableSimulationIfOwner.cs
using UnityEngine;
using Photon.Pun;
using Photon.Realtime; // Player

/// <summary>
/// Only the owner (Master for scene views) runs the custom ball simulation script.
/// Everyone else disables it and follows via PhotonTransformViewClassic (or your custom sync).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class EnableSimulationIfOwner : MonoBehaviourPunCallbacks, IPunOwnershipCallbacks
{
    [Tooltip("Script that actually moves this ball (e.g., CustomCueBall). Enabled only on the owner.")]
    public Behaviour simulationBehaviour;

    void Awake()
    {
        if (!simulationBehaviour)
            simulationBehaviour = GetComponent<Behaviour>(); // convenience fallback
    }

    public override void OnEnable()
    {
        Apply();
        PhotonNetwork.AddCallbackTarget(this);   // register IPunOwnershipCallbacks
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this); // unregister
    }

    void Start() => Apply();

    // Master change (scene views switch owner to new Master)
    public override void OnMasterClientSwitched(Player newMasterClient) => Apply();

    // --- IPunOwnershipCallbacks (note: NO 'override' here) ---
    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        // not responding here — we don't hand out ownership for balls in this setup
    }

    // PUN uses the (misspelled) method name 'OnOwnershipTransfered'
    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView == photonView) Apply();
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        if (targetView == photonView) Apply();
    }

    // Enable simulation only for the owning client (Master for scene views)
    void Apply()
    {
        if (!simulationBehaviour) return;
        simulationBehaviour.enabled = photonView && photonView.IsMine;
    }
}
