using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public enum RoomType
{
    Normal,
    Start,
    Boss,
    Treasure,
    Chest,
    ArtifactRoom, // кімната з вибором артефакту, заходиться лише знизу
    StatueRoom,   // кімната зі статуєю прокачки (+ХП/швидкість/урон/швидкість снаряду), теж заходиться лише знизу
    SecretRoom,   // кімната-бонус, з'являється НЕ гарантовано, а лише за шансом (0..N штук на рівень)
    DebugRoom     // 2x2 кімната-полігон для тестування всіх артефактів/прокачок (тільки editor, DebugArtifactTestRoom)
}

[Serializable]
public class CellDoors
{
    [Tooltip("Зміщення цієї клітинки відносно першої (0,0) в канонічній формі")]
    public Vector2Int cellOffset = Vector2Int.zero;

    [Header("Закриті двері (стандартний вигляд)")]
    public GameObject doorUp;
    public GameObject doorDown;
    public GameObject doorLeft;
    public GameObject doorRight;

    [Header("Відкриті двері (окремий об'єкт, показується замість закритих)")]
    public GameObject doorUpOpen;
    public GameObject doorDownOpen;
    public GameObject doorLeftOpen;
    public GameObject doorRightOpen;
}

[RequireComponent(typeof(Collider2D))]
public class RoomController : MonoBehaviour
{
    [Header("Тип кімнати (виставляється генератором автоматично)")]
    public RoomType roomType = RoomType.Normal;

    [Header("Позиція на сітці - якірна клітинка (виставляється генератором)")]
    public Vector2Int gridPos;

    [Header("Двері для кожної клітинки кімнати (1 елемент = звичайна кімната)")]
    public CellDoors[] cellDoors = new CellDoors[] { new CellDoors() };

    [Header("Візуальний вміст кімнати, який будемо вимикати")]
    public GameObject roomContent;

    [Header("Монстри (постав як дочірні GameObject кімнати, з MonsterController)")]
    public Transform enemiesParent; // якщо порожньо - шукає монстрів серед усіх дочірніх об'єктів кімнати

    [Header("Нестандартна форма кімнати (хрест, кільце тощо)")]
    [Tooltip("Постав галочку, якщо на цьому префабі вручну намальований PolygonCollider2D по формі підлоги. " +
             "Тоді генератор НЕ буде перезаписувати розмір колайдера прямокутником.")]
    public bool useCustomBoundsCollider = false;

    [Header("Відступ колайдера від стін кімнати (0 = точно по розміру кімнати)")]
    public float boundsPadding = 0f;

    [Header("Поштовх гравця вперед при вході в кімнату")]
    public float entryPushDistance = 0.9f; // на скільки одиниць проштовхнути гравця далі в кімнату

    [Header("Камера для великих/лабіринтних кімнат")]
    [Tooltip("Якщо увімкнено - камера НЕ показує всю кімнату одразу, а тримає зум ~1 клітинки " +
             "і плавно слідує за гравцем в межах цієї кімнати (для хрестів, кілець, лабіринтів тощо).")]
    public bool useCameraFollowMode = false;

    private Collider2D roomBounds;
    private Vector2 actualRoomSize; // канонічний (неповернутий) розмір

    public Vector2 RoomSize
    {
        get
        {
            float zRot = Mathf.Repeat(transform.eulerAngles.z, 360f);
            bool rotatedSideways = Mathf.Approximately(zRot, 90f) || Mathf.Approximately(zRot, 270f);
            return rotatedSideways ? new Vector2(actualRoomSize.y, actualRoomSize.x) : actualRoomSize;
        }
    }
    private bool hasBeenCleared = false;
    private bool isLocked = false;
    private List<MonsterHealth> aliveMonsters = new List<MonsterHealth>();

    // Запам'ятовуємо для кожних "закритих" дверей: чи мають вони бути відкриті
    // за задумом генератора, і який об'єкт "відкритих дверей" показувати замість них
    private class DoorState
    {
        public bool shouldBeOpen;
        public GameObject openVariant; // може бути null, якщо окремого вигляду відкритих дверей немає
    }
    private Dictionary<GameObject, DoorState> doorStates = new Dictionary<GameObject, DoorState>();

    [Tooltip("Викликається щоразу, коли гравець заходить у цю кімнату (наприклад для запуску хвиль боса)")]
    public event Action OnPlayerEntered;

    // Статична подія - спрацьовує для БУДЬ-ЯКОЇ кімнати в грі, щойно вона реально зачищена
    // (всі монстри вбиті, викликається з UnlockRoom(), а не просто при вході). Кімнати без монстрів
    // ніколи не блокуються, тому і цю подію не викликають - "прохідні" кімнати в кулдаун не рахуються.
    // Підписується, наприклад, ActiveArtifactSlot, щоб рахувати кулдаун активних артефактів у зачищених кімнатах.
    public static event Action OnAnyRoomCleared;

    // Статична подія - спрацьовує, щойно гравець заходить у БУДЬ-ЯКУ кімнату (перехід між кімнатами).
    // Підписується, наприклад, StationController, щоб прибрати станції/лінії/лазерне кільце
    // зі старої кімнати, а не лишати їх висіти там, поки гравець пішов далі.
    public static event Action OnAnyRoomEntered;

    // Публічні обгортки над LockRoom/UnlockRoom - для кімнат, де блокуванням керує
    // зовнішній скрипт (наприклад BossRoomController), а не власна логіка aliveMonsters
    public void LockRoomExternally() => LockRoom();
    public void UnlockRoomExternally() => UnlockRoom();

    void Awake()
    {
        roomBounds = GetComponent<Collider2D>();
        roomBounds.isTrigger = true;
    }

    void Start()
    {
        // Якщо це не стартова і не дебаг-кімната, ховаємо її графіку при запуску гри.
        // DebugRoom виключена тому, що DebugArtifactTestRoom сама вмикає roomContent
        // одразу після Instantiate, а Start() виконується вже ПІСЛЯ цього - без цього
        // винятку Start() одразу ж ховав би щойно увімкнений візуал назад.
        if (roomType != RoomType.Start && roomType != RoomType.DebugRoom && roomContent != null)
        {
            roomContent.SetActive(false);
        }

        FindMonsters();
    }

    void FindMonsters()
    {
        Transform searchRoot = enemiesParent != null ? enemiesParent : transform;
        MonsterHealth[] monsters = searchRoot.GetComponentsInChildren<MonsterHealth>();

        foreach (MonsterHealth monster in monsters)
        {
            // Кімната могла бути повернута генератором (90/180/270°) - без цього монстр
            // успадкував би цей поворот від батька і виглядав би "боком" замість прямо.
            // Позицію це НЕ чіпає, лише орієнтацію.
            monster.transform.rotation = Quaternion.identity;

            aliveMonsters.Add(monster);
            monster.OnDeath += () => OnMonsterDied(monster);
        }
    }

    void OnMonsterDied(MonsterHealth monster)
    {
        aliveMonsters.Remove(monster);

        if (aliveMonsters.Count == 0 && isLocked)
        {
            UnlockRoom();
        }
    }

    public void SetupBounds(Vector2 size)
    {
        actualRoomSize = size; // канонічний розмір, БЕЗ заміни x/y (потрібен для CameraController і при custom формі)

        // Якщо форма кімнати нестандартна (хрест, кільце тощо) - колайдер вже намальований
        // вручну в префабі (наприклад PolygonCollider2D), не чіпаємо його розмір/офсет
        if (useCustomBoundsCollider) return;

        if (roomBounds is BoxCollider2D box)
        {
            box.size = new Vector2(size.x - boundsPadding, size.y - boundsPadding);
            box.offset = Vector2.zero;
        }
        else
        {
            Debug.LogWarning($"[{name}] useCustomBoundsCollider вимкнено, але колайдер не BoxCollider2D - розмір не виставлено автоматично.");
        }
    }

    public void ConfigureDoors(int rotationSteps, Func<Vector2Int, Vector2Int, bool> isOpenWorldDir)
    {
        foreach (CellDoors cd in cellDoors)
        {
            Vector2Int rotatedOffset = GridUtils.Rotate(cd.cellOffset, rotationSteps);
            Vector2Int cellWorldPos = gridPos + rotatedOffset;

            SetOneDoor(cd.doorUp, cd.doorUpOpen, Vector2Int.up, rotationSteps, cellWorldPos, isOpenWorldDir);
            SetOneDoor(cd.doorDown, cd.doorDownOpen, Vector2Int.down, rotationSteps, cellWorldPos, isOpenWorldDir);
            SetOneDoor(cd.doorLeft, cd.doorLeftOpen, Vector2Int.left, rotationSteps, cellWorldPos, isOpenWorldDir);
            SetOneDoor(cd.doorRight, cd.doorRightOpen, Vector2Int.right, rotationSteps, cellWorldPos, isOpenWorldDir);
        }
    }

    private void SetOneDoor(GameObject door, GameObject doorOpenVariant, Vector2Int localDir, int rotationSteps, Vector2Int cellWorldPos, Func<Vector2Int, Vector2Int, bool> isOpenWorldDir)
    {
        if (door == null) return;

        Vector2Int worldDir = GridUtils.Rotate(localDir, rotationSteps);
        bool open = isOpenWorldDir(cellWorldPos, worldDir);

        door.SetActive(!open);
        if (doorOpenVariant != null)
            doorOpenVariant.SetActive(open);

        // запам'ятовуємо задум генератора для цих дверей + пов'язаний "відкритий" об'єкт
        doorStates[door] = new DoorState { shouldBeOpen = open, openVariant = doorOpenVariant };
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Передаємо САМ скрипт (this) в центральний менеджер камери
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetActiveRoom(this);
            if (MinimapUI.Instance != null)
                MinimapUI.Instance.SetCurrentRoom(gridPos);
        }

        // Замикаємо кімнату, тільки якщо в ній ще є живі монстри і її ще не зачищали раніше
        // (для кімнат боса з хвилями монстрів на старті ще немає - блокування там бере на себе
        // BossRoomController через LockRoomExternally(), підписавшись на OnPlayerEntered нижче)
        if (!hasBeenCleared && aliveMonsters.Count > 0 && !isLocked)
        {
            LockRoom();
        }

        OnPlayerEntered?.Invoke();
        OnAnyRoomEntered?.Invoke(); // будь-яка кімната - гасимо станції/лінії/кільце зі старої кімнати

        PushPlayerForward(other);
    }

    // Невеликий поштовх гравця далі в кімнату, щоб він не застряг рівно на межі дверей
    void PushPlayerForward(Collider2D playerCollider)
    {
        if (entryPushDistance <= 0f) return;

        Rigidbody2D playerRb = playerCollider.attachedRigidbody;
        if (playerRb == null) return;

        Vector2 pushDirection = playerRb.linearVelocity.sqrMagnitude > 0.01f
            ? playerRb.linearVelocity.normalized
            : ((Vector2)transform.position - playerRb.position).normalized; // запасний варіант - у бік центру кімнати

        playerRb.position += pushDirection * entryPushDistance;
    }

    void LockRoom()
    {
        isLocked = true;

        // Активуємо (закриваємо) АБСОЛЮТНО всі двері кімнати, незалежно від задуму генератора,
        // і ховаємо всі "відкриті" варіанти
        foreach (var kvp in doorStates)
        {
            if (kvp.Key != null)
                kvp.Key.SetActive(true);

            if (kvp.Value.openVariant != null)
                kvp.Value.openVariant.SetActive(false);
        }
    }

    void UnlockRoom()
    {
        isLocked = false;
        hasBeenCleared = true;

        OnAnyRoomCleared?.Invoke(); // кімнату реально зачищено - рахуємо це для кулдаунів активних артефактів і т.п.

        // Повертаємо двері до стану, який задумав генератор (відкриті лише туди, де є сусідні кімнати).
        // Замість дверей, які мають бути відкриті, показуємо їхній openVariant (якщо він заданий).
        foreach (var kvp in doorStates)
        {
            bool open = kvp.Value.shouldBeOpen;

            if (kvp.Key != null)
                kvp.Key.SetActive(!open);

            if (kvp.Value.openVariant != null)
                kvp.Value.openVariant.SetActive(open);
        }
    }
}