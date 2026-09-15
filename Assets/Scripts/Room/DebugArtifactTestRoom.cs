#if UNITY_EDITOR
using UnityEngine;

// Дебаг-кімната для тестування комбінацій артефактів/прокачок ("на всі приколи разом").
// ВАЖЛИВО: весь файл обгорнутий у #if UNITY_EDITOR - в реальному білді гри
// цього класу не існує взагалі (він не скомпілюється, тому й не потрапить у білд).
// Працює тільки в Play Mode редактора.
//
// По хоткею (за замовчуванням F1):
//   - якщо кімната ще не створена - спавнить префаб 2x2 кімнати з усіма
//     артефактами/бустами всередині і телепортує гравця туди;
//   - якщо кімната вже відкрита - видаляє її і телепортує гравця туди,
//     де він був до цього (в тому числі назад у стартову кімнату).
//
// "Біля спавна" реалізовано не через сусідню клітинку сітки генератора
// (це було б крихко - LevelGenerator міг би випадково зайняти цю клітинку
// іншою кімнатою), а через миттєвий телепорт: гравець натискає клавішу
// і одразу опиняється в тестовій кімнаті, звідки й куди завгодно - це
// зручніше за фізичне сусідство і повністю ізольовано від генератора рівня.
public class DebugArtifactTestRoom : MonoBehaviour
{
    [Header("Префаб дебаг-кімнати (2x2, RoomController + всі артефакти/приколи як діти)")]
    public GameObject debugRoomPrefab;

    [Header("Гравець")]
    public GameObject player;

    [Header("Клавіша перемикання")]
    public KeyCode toggleKey = KeyCode.F1;

    [Header("Розмір однієї клітинки (має збігатись з LevelGenerator.roomWidth/roomHeight)")]
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    [Tooltip("Наскільки далеко в світових координатах розмістити дебаг-кімнату, щоб вона " +
             "гарантовано не перетнулась з процедурно згенерованим рівнем. Оскільки гравець " +
             "потрапляє туди миттєвим телепортом по хоткею, реальна 'віддаленість' на геймплей " +
             "не впливає. Якщо збільшуєте кількість кімнат/розмір рівня - збільште і це значення.")]
    public float safeOffset = 500f;

    private GameObject spawnedRoom;
    private Vector3 playerPositionBeforeToggle;
    private bool isRoomActive = false;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    void Toggle()
    {
        if (isRoomActive)
            CloseDebugRoom();
        else
            OpenDebugRoom();
    }

    void OpenDebugRoom()
    {
        if (debugRoomPrefab == null || player == null)
        {
            Debug.LogWarning("[DebugArtifactTestRoom] Не задано debugRoomPrefab або player - неможливо відкрити тестову кімнату.");
            return;
        }

        Vector3 spawnPos = new Vector3(safeOffset, safeOffset, 0f);
        spawnedRoom = Instantiate(debugRoomPrefab, spawnPos, Quaternion.identity);

        RoomController controller = spawnedRoom.GetComponent<RoomController>();
        if (controller != null)
        {
            controller.roomType = RoomType.DebugRoom;

            // 2x2 кімната - розмір як у боса (roomWidth*2 x roomHeight*2)
            controller.SetupBounds(new Vector2(roomWidth * 2f, roomHeight * 2f));

            if (controller.roomContent != null)
                controller.roomContent.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[DebugArtifactTestRoom] На префабі debugRoomPrefab немає RoomController - " +
                              "кімната спаунеться, але без правильних боундів/камери.");
        }

        // Запам'ятовуємо, де був гравець ДО телепорту, щоб повернути його точно туди при закритті
        playerPositionBeforeToggle = player.transform.position;
        TeleportPlayer(spawnPos);

        if (CameraController.Instance != null && controller != null)
            CameraController.Instance.SetActiveRoom(controller);

        isRoomActive = true;
    }

    void CloseDebugRoom()
    {
        if (spawnedRoom != null)
            Destroy(spawnedRoom);
        spawnedRoom = null;

        if (player != null)
            TeleportPlayer(playerPositionBeforeToggle);

        isRoomActive = false;
    }

    // Через Rigidbody2D.position, якщо він є - щоб фізика не "засмикала" гравця назад
    // після прямого зсуву transform.position на великій відстані.
    void TeleportPlayer(Vector3 worldPos)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = worldPos;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.transform.position = worldPos;
        }
    }

    void OnDestroy()
    {
        // Про всяк випадок прибираємо кімнату, якщо об'єкт-менеджер знищується, поки вона відкрита
        if (spawnedRoom != null)
            Destroy(spawnedRoom);
    }
}
#endif
