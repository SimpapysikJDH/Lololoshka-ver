using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Причепи на порожній GameObject в Canvas (наприклад "MinimapUI")
public class MinimapUI : MonoBehaviour
{
    public static MinimapUI Instance;

    [Header("Viewport - рамка на екрані з компонентом Mask (обрізає зайве)")]
    public RectTransform viewport;

    [Header("World - контейнер іконок, який РУХАЄТЬСЯ і МАСШТАБУЄТЬСЯ всередині viewport")]
    public RectTransform minimapWorld;

    [Header("Префаб іконки кімнати (Image, розтягується під розмір кімнати)")]
    public GameObject roomIconPrefab;

    [Header("Відстань між клітинками сітки (у пікселях UI)")]
    public float iconSpacing = 30f;
    [Header("Невеликий проміжок між сусідніми іконками (у пікселях)")]
    public float iconGap = 3f;

    [Header("Розмір рамки: маленька мінімапа / повна карта (Tab)")]
    public Vector2 miniMapSize = new Vector2(120f, 120f);
    public Vector2 fullMapSize = new Vector2(360f, 360f);
    public float fullMapWorldScale = 0.55f; // наскільки "зменшується" карта в режимі повного огляду

    [Header("Швидкість плавного панорамування/масштабування")]
    public float panSpeed = 8f;

    [Header("Кольори типів кімнат (для ВІДВІДАНИХ кімнат)")]
    public Color normalColor = Color.white;
    public Color startColor = Color.green;
    public Color bossColor = Color.red;
    public Color treasureColor = Color.yellow;
    public Color chestColor = new Color(0.6f, 0.4f, 0.9f);

    [Header("Колір сусідньої кімнати, де ще не був (лише натяк)")]
    public Color neighborHintColor = new Color(0.35f, 0.35f, 0.35f);

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // Дані по кімнатах (не по окремих клітинках)
    private List<RoomData> allRooms;
    private Dictionary<Vector2Int, int> cellToRoomIndex = new Dictionary<Vector2Int, int>();
    private Dictionary<int, Image> roomIcons = new Dictionary<int, Image>();
    private HashSet<int> visitedRooms = new HashSet<int>();
    private HashSet<int> revealedRooms = new HashSet<int>();

    private int currentRoomIndex = -1;
    private Vector2 currentRoomCenterCell; // центр поточної кімнати в координатах сітки (може бути дробовим)
    private bool isFullMap = false;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isFullMap = !isFullMap;
        }

        // Плавно змінюємо розмір рамки (Viewport)
        Vector2 targetSize = isFullMap ? fullMapSize : miniMapSize;
        viewport.sizeDelta = Vector2.Lerp(viewport.sizeDelta, targetSize, panSpeed * Time.deltaTime);

        // Плавно змінюємо масштаб карти
        float targetScale = isFullMap ? fullMapWorldScale : 1f;
        float newScale = Mathf.Lerp(minimapWorld.localScale.x, targetScale, panSpeed * Time.deltaTime);
        minimapWorld.localScale = new Vector3(newScale, newScale, 1f);

        // Перераховуємо позицію контейнера щокадру, з урахуванням поточного масштабу,
        // щоб поточна кімната завжди лишалась точно по центру рамки
        Vector2 targetWorldPos = new Vector2(-currentRoomCenterCell.x * iconSpacing, -currentRoomCenterCell.y * iconSpacing) * newScale;
        minimapWorld.anchoredPosition = Vector2.Lerp(minimapWorld.anchoredPosition, targetWorldPos, panSpeed * Time.deltaTime);
    }

    // Викликає LevelGenerator одразу після генерації рівня, передаючи повний список кімнат
    public void GenerateMinimap(List<RoomData> rooms)
    {
        allRooms = rooms;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomData room = rooms[i];

            foreach (Vector2Int cell in room.cells)
                cellToRoomIndex[cell] = i;

            // Розраховуємо межі кімнати в клітинках сітки (працює для 1, 2 чи 3-клітинкових форм)
            int minX = room.cells.Min(c => c.x);
            int maxX = room.cells.Max(c => c.x);
            int minY = room.cells.Min(c => c.y);
            int maxY = room.cells.Max(c => c.y);

            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;
            int widthCells = maxX - minX + 1;
            int heightCells = maxY - minY + 1;

            GameObject iconObj = Instantiate(roomIconPrefab, minimapWorld);
            iconObj.transform.localScale = Vector3.one; // страховка від зміщеного масштабу префабу

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(centerX * iconSpacing, centerY * iconSpacing);
            rt.sizeDelta = new Vector2(widthCells * iconSpacing - iconGap, heightCells * iconSpacing - iconGap);

            Image img = iconObj.GetComponent<Image>();
            roomIcons[i] = img;

            iconObj.SetActive(false);
        }

        // Знаходимо стартову кімнату (Vector2Int.zero завжди належить до неї) і показуємо одразу
        if (cellToRoomIndex.TryGetValue(Vector2Int.zero, out int startIndex))
        {
            SetCurrentRoom(Vector2Int.zero);
        }

        // На старті гри - миттєво (без Lerp) ставимо правильні розмір/позицію
        viewport.sizeDelta = miniMapSize;
        minimapWorld.localScale = Vector3.one;
        minimapWorld.anchoredPosition = new Vector2(-currentRoomCenterCell.x * iconSpacing, -currentRoomCenterCell.y * iconSpacing);
    }

    // Викликає RoomController, передаючи БУДЬ-ЯКУ клітинку, що належить кімнаті (наприклад gridPos)
    public void SetCurrentRoom(Vector2Int anyCellOfRoom)
    {
        if (!cellToRoomIndex.TryGetValue(anyCellOfRoom, out int roomIndex)) return;
        if (allRooms == null) return;

        currentRoomIndex = roomIndex;
        RoomData room = allRooms[roomIndex];

        int minX = room.cells.Min(c => c.x);
        int maxX = room.cells.Max(c => c.x);
        int minY = room.cells.Min(c => c.y);
        int maxY = room.cells.Max(c => c.y);
        currentRoomCenterCell = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        visitedRooms.Add(roomIndex);
        Reveal(roomIndex);

        // Розкриваємо сусідні кімнати (по всіх клітинках цієї кімнати одразу, бо велика кімната
        // може межувати з різними сусідами з різних боків)
        foreach (Vector2Int cell in room.cells)
        {
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighborCell = cell + dir;
                if (cellToRoomIndex.TryGetValue(neighborCell, out int neighborRoomIndex) && neighborRoomIndex != roomIndex)
                {
                    Reveal(neighborRoomIndex);
                }
            }
        }

        RefreshAllIcons();
    }

    // Викликається активним артефактом типу RevealMap - розкриває ВСІ кімнати рівня одразу
    // (як нейтральна "мапа", ще НЕ відвідані кімнати лишаються сірими - neighborHintColor,
    // бо visitedRooms не змінюється, змінюється лише revealedRooms)
    public void RevealAllRooms()
    {
        if (allRooms == null) return;

        for (int i = 0; i < allRooms.Count; i++)
            Reveal(i);

        RefreshAllIcons();
    }

    void Reveal(int roomIndex)
    {
        if (!revealedRooms.Contains(roomIndex))
        {
            revealedRooms.Add(roomIndex);
            if (roomIcons.TryGetValue(roomIndex, out Image img))
                img.gameObject.SetActive(true);
        }
    }

    void RefreshAllIcons()
    {
        foreach (var kvp in roomIcons)
        {
            int roomIndex = kvp.Key;
            Image img = kvp.Value;

            if (!revealedRooms.Contains(roomIndex)) continue;

            RoomType type = allRooms[roomIndex].type;
            img.color = visitedRooms.Contains(roomIndex) ? GetColorForType(type) : neighborHintColor;

            bool isCurrent = roomIndex == currentRoomIndex;
            img.transform.localScale = isCurrent ? Vector3.one * 1.15f : Vector3.one;
        }
    }

    Color GetColorForType(RoomType type)
    {
        return type switch
        {
            RoomType.Start => startColor,
            RoomType.Boss => bossColor,
            RoomType.Treasure => treasureColor,
            RoomType.Chest => chestColor,
            _ => normalColor
        };
    }
}