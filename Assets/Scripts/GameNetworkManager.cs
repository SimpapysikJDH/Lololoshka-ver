using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameNetworkManager : MonoBehaviourPunCallbacks
{
    [Header("Network player")]
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Spawn points")]
    [SerializeField] private Transform[] spawnPoints;

    private GameObject localPlayer;

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Сцену Game відкрито без підключення до Photon Room"
            );

            SceneManager.LoadScene("Lobby");
            return;
        }

        SpawnLocalPlayer();
    }

    private void SpawnLocalPlayer()
    {
        if (localPlayer != null)
            return;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "У GameNetworkManager не призначені точки спавну"
            );
            return;
        }

        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int spawnIndex = (actorNumber - 1) % spawnPoints.Length;

        Transform spawnPoint = spawnPoints[spawnIndex];

        localPlayer = PhotonNetwork.Instantiate(
            playerPrefabName,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Debug.Log(
            $"Заспавнено мережевого гравця. " +
            $"ActorNumber: {actorNumber}, " +
            $"SpawnPoint: {spawnIndex}"
        );
    }

    public void LeaveGame()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene("Lobby");
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(
        Photon.Realtime.DisconnectCause cause)
    {
        Debug.LogError($"Photon disconnected: {cause}");

        SceneManager.LoadScene("Lobby");
    }
}