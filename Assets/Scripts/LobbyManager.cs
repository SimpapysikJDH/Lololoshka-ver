using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_Text currentRoomCodeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "GameScene";

    private void Start()
    {
        // Всі клієнти будуть автоматично переходити
        // в ту сцену, яку завантажив Master Client.
        PhotonNetwork.AutomaticallySyncScene = true;

        statusText.text = "Connecting...";

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            statusText.text = "Connected";
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon.");

        statusText.text = "Connected";
    }

    // Викликається кнопкою CREATE ROOM
    public void CreateRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Photon is not ready.");
            return;
        }

        string roomCode = GenerateRoomCode();

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = false,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomCode, options);
    }

    // Викликається кнопкою JOIN ROOM
    public void JoinRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        string roomCode = roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(roomCode))
        {
            statusText.text = "Enter room code";
            return;
        }

        statusText.text = "Joining room...";

        PhotonNetwork.JoinRoom(roomCode);
    }

    // Викликається кнопкою LEAVE ROOM
    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
            return;

        PhotonNetwork.LeaveRoom();
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Room created: {PhotonNetwork.CurrentRoom.Name}");

        statusText.text = "Room created";
    }

    public override void OnJoinedRoom()
    {
        string roomCode = PhotonNetwork.CurrentRoom.Name;

        Debug.Log($"Joined room: {roomCode}");

        currentRoomCodeText.text = $"Code: {roomCode}";
        statusText.text =
            $"Players: {PhotonNetwork.CurrentRoom.PlayerCount}/2";

        CheckStartGame();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player joined: {newPlayer.ActorNumber}");

        statusText.text =
            $"Players: {PhotonNetwork.CurrentRoom.PlayerCount}/2";

        CheckStartGame();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        statusText.text =
            $"Players: {PhotonNetwork.CurrentRoom.PlayerCount}/2";
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left room.");

        currentRoomCodeText.text = "";
        statusText.text = "Connected";
    }

    public override void OnJoinRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogWarning($"Join failed: {message}");

        statusText.text = "Room not found";
    }

    public override void OnCreateRoomFailed(
        short returnCode,
        string message)
    {
        Debug.LogWarning($"Create failed: {message}");

        // Якщо випадково код вже існує —
        // просто пробуємо створити новий.
        CreateRoom();
    }

    private void CheckStartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (PhotonNetwork.CurrentRoom.PlayerCount != 2)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;

        Debug.Log("2 players connected. Starting game.");

        PhotonNetwork.LoadLevel(gameSceneName);
    }

    private string GenerateRoomCode()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        string result = "";

        for (int i = 0; i < 6; i++)
        {
            result += characters[
                Random.Range(0, characters.Length)
            ];
        }

        return result;
    }
}