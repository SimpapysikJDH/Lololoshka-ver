using Photon.Pun;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Spawn Points")]
    [SerializeField] private Transform playerOneSpawn;
    [SerializeField] private Transform playerTwoSpawn;

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError(
                "Game scene loaded but client is not in Photon room."
            );

            return;
        }

        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        int actorNumber =
            PhotonNetwork.LocalPlayer.ActorNumber;

        Transform spawnPoint;

        if (actorNumber == 1)
        {
            spawnPoint = playerOneSpawn;
        }
        else
        {
            spawnPoint = playerTwoSpawn;
        }

        GameObject player =
            PhotonNetwork.Instantiate(
                playerPrefabName,
                spawnPoint.position,
                Quaternion.identity
            );

        Debug.Log(
            $"Spawned Player {actorNumber}: {player.name}"
        );
    }
}