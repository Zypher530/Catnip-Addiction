using Photon.Pun;
using UnityEngine;

/// <summary>
/// Detects when players cross the finish line and triggers end-of-race events.
/// </summary>
/// <inheritdoc />
public class FinishLine : MonoBehaviour
{
    private FireworksManager _fireworks;
    private PlayerNotificationManager _notificationManager;

    private void Start()
    {
        _notificationManager = FindFirstObjectByType<PlayerNotificationManager>();
    }

    /// Detects when players reach the finish line
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameManager.Instance.gameStarted) return;
        if (!other.CompareTag("Player")) return;

        _fireworks = FindFirstObjectByType<FireworksManager>();
        if (_fireworks != null)
            _fireworks.TriggerFireworksSequence(other.transform.position);

        var photonView = PhotonView.Get(other);
        var playerName = photonView.Owner.NickName;

        if (_notificationManager != null)
            _notificationManager.ShowFinishNotification(playerName);

        if (!photonView.IsMine) return;

        var player = other.GetComponent<PlayerController>();

        if (player == null || !PhotonNetwork.IsConnected || !PhotonNetwork.LocalPlayer.IsLocal) return;

        var finishTime = Time.timeSinceLevelLoad - GameManager.Instance.startTime;
        var playerID = PhotonNetwork.LocalPlayer.ActorNumber;

        GameManager.Instance.PlayerFinished(playerID, finishTime);
        player.SetSpectatorMode(true);
    }
}
