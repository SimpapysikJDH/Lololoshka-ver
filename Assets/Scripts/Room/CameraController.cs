using UnityEngine;

// Причепи на Main Camera
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("Швидкість переходу між кімнатами")]
    public float transitionSpeed = 8f;

    [Header("Наскільки далі відсунути камеру (1 = точно по кімнаті, 1.15 = на 15% далі)")]
    public float zoomOutPadding = 1.15f;

    [Header("Розмір кімнати за замовчуванням (для старту гри, до першого SetActiveRoom)")]
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    [Header("Гравець (потрібен для режиму слідування камери у великих/лабіринтних кімнатах)")]
    [Tooltip("Можна залишити порожнім - автоматично знайде об'єкт з тегом Player. " +
             "Або LevelGenerator сам підставить це поле при старті гри.")]
    public Transform player;

    private Camera cam;
    private Vector3 targetPosition;
    private float targetSize;

    // Зберігаємо посилання на поточну активну кімнату
    private RoomController currentRoom;

    // Режим слідування за гравцем (для кімнат з useCameraFollowMode = true)
    private bool isFollowMode = false;
    private RoomController followedRoom;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        targetPosition = transform.position;
        targetSize = CalculateFitSize(roomWidth, roomHeight);
        cam.orthographicSize = targetSize;
    }

    // Рахує Orthographic Size так, щоб уся кімната ЗАДАНОГО розміру влізла в екран (+ запас zoomOutPadding)
    float CalculateFitSize(float width, float height)
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float roomAspect = width / height;

        float size = screenAspect >= roomAspect
            ? height / 2f          // обмежує ВИСОТА
            : (width / 2f) / screenAspect; // обмежує ШИРИНА

        return size * zoomOutPadding;
    }

    void Update()
    {
        // В режимі слідування - щокадру рахуємо, куди камера повинна дивитись,
        // виходячи з позиції гравця, затиснутої в межах поточної кімнати
        if (isFollowMode && followedRoom != null && player != null)
        {
            UpdateFollowTarget();
        }

        Vector3 desired = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, transitionSpeed * Time.deltaTime);

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, transitionSpeed * Time.deltaTime);
    }

    // Затискає позицію гравця в межах кімнати так, щоб поле зору камери не виходило за стіни
    void UpdateFollowTarget()
    {
        float halfHeight = targetSize;
        float halfWidth = targetSize * cam.aspect;

        Vector2 roomSize = followedRoom.RoomSize;
        Vector3 roomCenter = followedRoom.transform.position;

        float minX = roomCenter.x - roomSize.x / 2f;
        float maxX = roomCenter.x + roomSize.x / 2f;
        float minY = roomCenter.y - roomSize.y / 2f;
        float maxY = roomCenter.y + roomSize.y / 2f;

        // Якщо кімната ширша/вища за поле зору камери по цій осі - затискаємо в межах,
        // інакше (кімната менша за екран) просто центруємо по цій осі
        float clampedX = (maxX - minX) > halfWidth * 2f
            ? Mathf.Clamp(player.position.x, minX + halfWidth, maxX - halfWidth)
            : roomCenter.x;

        float clampedY = (maxY - minY) > halfHeight * 2f
            ? Mathf.Clamp(player.position.y, minY + halfHeight, maxY - halfHeight)
            : roomCenter.y;

        targetPosition = new Vector3(clampedX, clampedY, targetPosition.z);
    }

    // Головний метод для перемикання кімнат (викликається з RoomController та LevelGenerator)
    public void SetActiveRoom(RoomController newRoom)
    {
        if (currentRoom == newRoom) return;

        // 1. Вимикаємо попередню кімнату (якщо вона була)
        if (currentRoom != null && currentRoom.roomContent != null)
        {
            currentRoom.roomContent.SetActive(false);
        }

        // 2. Запам'ятовуємо нову кімнату та вмикаємо її візуал
        currentRoom = newRoom;
        if (currentRoom.roomContent != null)
        {
            currentRoom.roomContent.SetActive(true);
        }

        // 3. Обираємо режим камери залежно від того, чи це "лабіринтна" кімната
        if (newRoom.useCameraFollowMode)
        {
            isFollowMode = true;
            followedRoom = newRoom;

            // Фіксований зум "як в звичайній кімнаті" (1 клітинка), а не вся кімната цілком
            targetSize = CalculateFitSize(roomWidth, roomHeight);

            // targetPosition порахує сам UpdateFollowTarget() в наступному Update(), виходячи з гравця
        }
        else
        {
            isFollowMode = false;
            followedRoom = null;

            Vector2 size = newRoom.RoomSize;
            targetSize = CalculateFitSize(size.x, size.y);
            MoveToRoom(newRoom.transform.position);
        }
    }

    public void MoveToRoom(Vector3 roomCenter)
    {
        targetPosition = roomCenter;
    }

    public void SnapToRoom(Vector3 roomCenter)
    {
        targetPosition = roomCenter;
        transform.position = new Vector3(roomCenter.x, roomCenter.y, transform.position.z);
        cam.orthographicSize = targetSize; // миттєво фіксуємо розмір, без Lerp-переходу на старті
    }

    // Миттєво (без плавного Lerp) підігнати камеру під конкретну кімнату - корисно на старті гри
    public void SnapToRoom(RoomController room)
    {
        currentRoom = room;

        if (room.useCameraFollowMode)
        {
            isFollowMode = true;
            followedRoom = room;
            targetSize = CalculateFitSize(roomWidth, roomHeight);

            if (player != null)
                UpdateFollowTarget();
            else
                targetPosition = room.transform.position;
        }
        else
        {
            isFollowMode = false;
            followedRoom = null;
            targetPosition = room.transform.position;
            targetSize = CalculateFitSize(room.RoomSize.x, room.RoomSize.y);
        }

        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        cam.orthographicSize = targetSize;

        if (room.roomContent != null)
            room.roomContent.SetActive(true);
    }
}