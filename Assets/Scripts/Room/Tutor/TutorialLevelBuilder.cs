using System.Collections.Generic;
using UnityEngine;

// Порожній GameObject у сцені Tutorial. Будує лінійну стрічку кімнат ТІЛЬКИ вправо.
public class TutorialLevelBuilder : MonoBehaviour
{
    [Header("Префаби кімнат по порядку: 1) рух, 2) стрільба, 3) монстр")]
    public List<GameObject> roomPrefabs = new List<GameObject>();

    [Header("Розмір клітинки (як у LevelGenerator)")]
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    [Tooltip("Крок між кімнатами в світі. Має дорівнювати DoorTrigger.teleportDistance")]
    public float gridStep = 100f;

    [Header("Гравець (якщо задано - ставиться в центр першої кімнати)")]
    public Transform player;

    void Start()
    {
        if (roomPrefabs.Count == 0)
        {
            Debug.LogWarning("TutorialLevelBuilder: список roomPrefabs порожній.");
            return;
        }

        List<RoomController> rooms = new List<RoomController>();
        int last = roomPrefabs.Count - 1;

        for (int i = 0; i < roomPrefabs.Count; i++)
        {
            Vector3 pos = new Vector3(i * gridStep, 0f, 0f);
            GameObject obj = Instantiate(roomPrefabs[i], pos, Quaternion.identity, transform);

            RoomController rc = obj.GetComponent<RoomController>();
            if (rc == null)
            {
                Debug.LogWarning($"TutorialLevelBuilder: на префабі '{roomPrefabs[i].name}' немає RoomController.");
                continue;
            }

            rc.gridPos = new Vector2Int(i, 0);
            rc.roomType = i == 0 ? RoomType.Start : RoomType.Normal;
            rc.SetupBounds(new Vector2(roomWidth, roomHeight));

            // Двері відкриті лише вправо, і лише якщо праворуч є ще кімната
            rc.ConfigureDoors(0, (cell, dir) => dir == Vector2Int.right && cell.x < last);

            rooms.Add(rc);
        }

        if (rooms.Count == 0) return;

        if (player != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = rooms[0].transform.position; rb.linearVelocity = Vector2.zero; }
            else player.position = rooms[0].transform.position;
        }

        if (CameraController.Instance != null)
            CameraController.Instance.SnapToRoom(rooms[0]);
    }
}
