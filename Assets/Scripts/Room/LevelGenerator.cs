using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public enum RoomShape { Normal, Big, Square }

public class RoomData
{
    public int id;
    public List<Vector2Int> cells = new List<Vector2Int>();
    public RoomShape shape = RoomShape.Normal;
    public RoomType type = RoomType.Normal;
    public int rotationSteps;
}

public class LevelGenerator : MonoBehaviour
{
    [Header("Стартова кімната (одна)")]
    public GameObject startRoomPrefab;

    [Header("Звичайні кімнати (1 клітинка) - варіанти")]
    public GameObject[] normalRoomPrefabs;

    [Header("Великі кімнати (2 клітинки, прямокутні)")]
    public GameObject[] bigRoomPrefabs;
    [Range(0f, 1f)] public float bigRoomChance = 0.2f;

    [Header("Велика кімната боса (2x2 клітинки = 4, квадрат)")]
    [Tooltip("Якщо задано - РІВНО ОДНА з кімнат боса буде цієї великої 2x2 форми " +
             "(решта, якщо bossRoomCount > 1, будуть звичайного 1-клітинкового розміру з normalRoomPrefabs). " +
             "Кімната боса ЗАВЖДИ ставиться в найвіддаленішій від старту точці рівня.")]
    public GameObject[] bigBossRoomPrefabs;
    [Tooltip("Не використовується: кімната боса тепер завжди ставиться в найвіддаленішій точці, а не за шансом.")]
    [Range(0f, 1f)] public float bigBossRoomChance = 0.5f;

    [Header("Кімната з вибором артефакту (заходиться ТІЛЬКИ знизу)")]
    public GameObject[] artifactRoomPrefabs;

    [Header("Кімната зі статуєю прокачки (заходиться ТІЛЬКИ знизу)")]
    public GameObject[] statueRoomPrefabs;

    [Header("Кількість спеціальних кімнат")]
    public int bossRoomCount = 1;
    public int treasureRoomCount = 1;
    public int chestRoomCount = 2;
    public int artifactRoomCount = 1;
    public int statueRoomCount = 1;

    [Header("Таємна кімната (НЕ гарантована - з'являється за шансом)")]
    [Tooltip("На відміну від Treasure/Chest/тощо (завжди рівно N штук), ця кімната НЕ гарантована: " +
             "кожен придатний глухий кут проходить незалежний рол з шансом secretRoomChance, " +
             "поки не назбирали secretRoomMaxCount кімнат. Тому на конкретному рівні їх може " +
             "бути 0, 1, ..., аж до secretRoomMaxCount - це навмисно, не баг.")]
    public GameObject[] secretRoomPrefabs;
    [Range(0f, 1f)] public float secretRoomChance = 0.15f;
    public int secretRoomMaxCount = 1;

    [Header("Гарантія кімнат артефакту/статуї")]
    [Tooltip("Кімната артефакту і кімната статуї ЗАВЖДИ повинні бути на рівні і НІКОЛИ не " +
             "можуть стояти поруч. Якщо одну спробу генерації не вдалося зробити правильно " +
             "(наприклад, примусове добудовування не знайшло вільного місця), весь рівень " +
             "перегенеровується заново - до цієї кількості спроб.")]
    [Min(1)] public int maxGenerationRetries = 30;

    [Header("Скільки ЗВИЧАЙНИХ кімнат додатково")]
    public int normalRoomCount = 5;

    [Header("Розмір ОДНІЄЇ клітинки сітки у світових одиницях")]
    public float roomWidth = 20f;
    public float roomHeight = 12f;

    [Header("Гравець")]
    public GameObject player;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private static readonly Vector2Int[] BigShapeOffsets = { Vector2Int.zero, Vector2Int.right };
    private static readonly Vector2Int[] SquareShapeOffsets = { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.right + Vector2Int.up };

    private bool bigBossPlaced = false;

    private Dictionary<Vector2Int, int> cellOwner = new Dictionary<Vector2Int, int>();
    private List<RoomData> rooms = new List<RoomData>();
    private List<Vector2Int> frontier = new List<Vector2Int>();
    private Vector3 startRoomWorldPos;

    void Start()
    {
        int totalRooms = 1 + normalRoomCount + bossRoomCount + treasureRoomCount + chestRoomCount + artifactRoomCount + statueRoomCount;

        // Кімната артефакту і кімната статуї - ОБОВ'ЯЗКОВА вимога: обидві мають існувати,
        // і вони НІКОЛИ не можуть стояти поруч одна з одною. AssignArtifactRoom/AssignStatueRoom
        // намагаються це забезпечити в межах однієї спроби генерації, але якщо конкретному
        // випадковому лабіринту фізично не вистачило місця (рідкісний, але можливий кейс),
        // одної спроби недостатньо - тому тут перегенеровуємо ВЕСЬ рівень заново, поки
        // умова не виконається (або поки не вичерпаємо ліміт спроб).
        int attempt = 0;
        bool guaranteed = false;

        do
        {
            attempt++;
            ResetGenerationState();
            GenerateLayout(totalRooms);
            AssignRoomTypes();
            guaranteed = ArtifactAndStatueRoomsAreGuaranteed();
        }
        while (!guaranteed && attempt < maxGenerationRetries);

        if (!guaranteed)
        {
            Debug.LogError($"LevelGenerator: за {maxGenerationRetries} спроб(и) не вдалося гарантовано " +
                            "розмістити кімнату(и) артефакту і статуї так, щоб обидві були присутні " +
                            "і НЕ стояли поруч. Рівень буде використано з останньої спроби генерації " +
                            "(перевірте normalRoomCount / розмір рівня - можливо, йому просто затісно).");
        }

        SpawnRooms();
        PlacePlayerAtStart();
    }

    // Повне скидання стану генератора перед новою спробою побудови рівня.
    void ResetGenerationState()
    {
        cellOwner.Clear();
        rooms.Clear();
        frontier.Clear();
        bigBossPlaced = false;
    }

    // Жорстка перевірка обов'язкової вимоги: кімната(и) артефакту і статуї (якщо їхня
    // кількість > 0) повинні бути ФАКТИЧНО розміщені, і жодна клітинка кімнати артефакту
    // не повинна межувати з кімнатою статуї (і навпаки).
    bool ArtifactAndStatueRoomsAreGuaranteed()
    {
        if (artifactRoomCount > 0 && rooms.Count(r => r.type == RoomType.ArtifactRoom) < artifactRoomCount)
            return false;

        if (statueRoomCount > 0 && rooms.Count(r => r.type == RoomType.StatueRoom) < statueRoomCount)
            return false;

        foreach (RoomData room in rooms)
        {
            if (room.type != RoomType.ArtifactRoom && room.type != RoomType.StatueRoom) continue;

            RoomType oppositeType = room.type == RoomType.ArtifactRoom ? RoomType.StatueRoom : RoomType.ArtifactRoom;
            foreach (Vector2Int cell in room.cells)
            {
                if (CellAdjacentToRoomOfType(cell, oppositeType))
                    return false;
            }
        }

        return true;
    }

    void GenerateLayout(int totalRooms)
    {
        CreateRoom(new List<Vector2Int> { Vector2Int.zero }, RoomShape.Normal, 0);

        int safetyCounter = 0;
        int maxAttempts = totalRooms * 60;

        while (rooms.Count < totalRooms && safetyCounter < maxAttempts && frontier.Count > 0)
        {
            safetyCounter++;

            Vector2Int branchFrom = frontier[Random.Range(0, frontier.Count)];
            Vector2Int direction = Directions[Random.Range(0, Directions.Length)];
            Vector2Int firstCell = branchFrom + direction;

            if (cellOwner.ContainsKey(firstCell)) continue;
            if (CountDistinctNeighborRooms(firstCell, new HashSet<Vector2Int> { firstCell }, -1) > 1) continue;

            if (bigRoomPrefabs.Length > 0 && Random.value < bigRoomChance &&
                TryPlaceShape(firstCell, BigShapeOffsets, out List<Vector2Int> bigCells, out int bigRot))
            {
                CreateRoom(bigCells, RoomShape.Big, bigRot);
                continue;
            }

            CreateRoom(new List<Vector2Int> { firstCell }, RoomShape.Normal, 0);
        }

        // Кімнату боса розміщуємо ПІСЛЯ того, як увесь інший рівень уже згенеровано,
        // і завжди в найвіддаленішій (за кількістю переходів-кімнат від старту) точці,
        // куди вона взагалі здатна влізти - а не де завгодно випадково.
        if (bigBossRoomPrefabs != null && bigBossRoomPrefabs.Length > 0)
        {
            PlaceBigBossRoomFarthest();
        }
    }

    // Прохід по всіх поточних клітинках фронтиру, відсортованих від НАЙВІДДАЛЕНІШОЇ
    // до найближчої (за реальною відстанню в кімнатах-переходах від старту, а не по
    // прямій), у пошуку першого місця, куди влізе 2x2 кімната боса.
    void PlaceBigBossRoomFarthest()
    {
        Dictionary<int, int> roomDistances = ComputeRoomDistancesFromStart();

        List<Vector2Int> frontierCopy = new List<Vector2Int>(frontier);
        ShuffleDirections(frontierCopy); // випадковий порядок серед кандидатів з однаковою відстанню

        frontierCopy = frontierCopy
            .OrderByDescending(cell =>
                cellOwner.TryGetValue(cell, out int roomId) && roomDistances.TryGetValue(roomId, out int d) ? d : -1)
            .ToList();

        foreach (Vector2Int branchFrom in frontierCopy)
        {
            List<Vector2Int> directionsCopy = new List<Vector2Int>(Directions);
            ShuffleDirections(directionsCopy);

            foreach (Vector2Int direction in directionsCopy)
            {
                Vector2Int firstCell = branchFrom + direction;

                if (cellOwner.ContainsKey(firstCell)) continue;
                if (CountDistinctNeighborRooms(firstCell, new HashSet<Vector2Int> { firstCell }, -1) > 1) continue;

                if (TryPlaceShape(firstCell, SquareShapeOffsets, out List<Vector2Int> bossCells, out int bossRot))
                {
                    CreateRoom(bossCells, RoomShape.Square, bossRot);
                    rooms[rooms.Count - 1].type = RoomType.Boss;
                    bigBossPlaced = true;
                    return;
                }
            }
        }

        Debug.LogWarning("LevelGenerator: не вдалося втиснути велику 2x2 кімнату боса на рівні - " +
                          "буде використано звичайну 1-клітинкову кімнату з normalRoomPrefabs.");
    }

    // Рахує реальну відстань (кількість переходів між кімнатами) від стартової кімнати (id 0)
    // до кожної вже створеної кімнати. На відміну від Mathf.Abs(x)+Mathf.Abs(y), це враховує
    // фактичну звивистість лабіринту, а не відстань "по прямій" у сітці.
    Dictionary<int, int> ComputeRoomDistancesFromStart()
    {
        Dictionary<int, int> distances = new Dictionary<int, int>();
        if (rooms.Count == 0) return distances;

        Dictionary<int, HashSet<int>> adjacency = new Dictionary<int, HashSet<int>>();
        foreach (RoomData room in rooms)
            adjacency[room.id] = new HashSet<int>();

        foreach (RoomData room in rooms)
        {
            foreach (Vector2Int cell in room.cells)
            {
                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int n = cell + dir;
                    if (cellOwner.TryGetValue(n, out int otherId) && otherId != room.id)
                    {
                        adjacency[room.id].Add(otherId);
                        adjacency[otherId].Add(room.id);
                    }
                }
            }
        }

        Queue<int> bfsQueue = new Queue<int>();
        distances[0] = 0;
        bfsQueue.Enqueue(0);

        while (bfsQueue.Count > 0)
        {
            int current = bfsQueue.Dequeue();
            foreach (int neighbor in adjacency[current])
            {
                if (!distances.ContainsKey(neighbor))
                {
                    distances[neighbor] = distances[current] + 1;
                    bfsQueue.Enqueue(neighbor);
                }
            }
        }

        return distances;
    }

    void ShuffleDirections(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    bool TryPlaceShape(Vector2Int anchorCell, Vector2Int[] canonicalOffsets, out List<Vector2Int> resultCells, out int resultRotation)
    {
        List<int> rotationsToTry = new List<int> { 0, 1, 2, 3 };
        Shuffle(rotationsToTry);

        foreach (int rotation in rotationsToTry)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            bool valid = true;

            foreach (Vector2Int offset in canonicalOffsets)
            {
                Vector2Int cell = anchorCell + GridUtils.Rotate(offset, rotation);
                if (cellOwner.ContainsKey(cell) && cell != anchorCell)
                {
                    valid = false;
                    break;
                }
                cells.Add(cell);
            }

            if (!valid) continue;

            HashSet<Vector2Int> ownCells = new HashSet<Vector2Int>(cells);
            HashSet<int> neighborRoomIds = new HashSet<int>();
            foreach (Vector2Int cell in cells)
            {
                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int n = cell + dir;
                    if (ownCells.Contains(n)) continue;
                    if (cellOwner.TryGetValue(n, out int roomId))
                        neighborRoomIds.Add(roomId);
                }
            }

            if (neighborRoomIds.Count > 1) continue;

            resultCells = cells;
            resultRotation = rotation;
            return true;
        }

        resultCells = null;
        resultRotation = 0;
        return false;
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void CreateRoom(List<Vector2Int> cells, RoomShape shape, int rotationSteps)
    {
        RoomData room = new RoomData { id = rooms.Count, cells = cells, shape = shape, rotationSteps = rotationSteps };
        rooms.Add(room);

        foreach (Vector2Int cell in cells)
        {
            cellOwner[cell] = room.id;
            frontier.Add(cell);
        }
    }

    int CountDistinctNeighborRooms(Vector2Int cell, HashSet<Vector2Int> excludeCells, int excludeRoomId)
    {
        HashSet<int> found = new HashSet<int>();
        foreach (Vector2Int dir in Directions)
        {
            Vector2Int n = cell + dir;
            if (excludeCells != null && excludeCells.Contains(n)) continue;
            if (cellOwner.TryGetValue(n, out int roomId) && roomId != excludeRoomId)
                found.Add(roomId);
        }
        return found.Count;
    }

    void AssignRoomTypes()
    {
        rooms[0].type = RoomType.Start;

        // Спершу підбираємо кімнату(и) артефакту і статуї - у них жорстка вимога (єдиний сусід рівно знизу),
        // тому робимо це до того, як бос/скарб/скриня розберуть решту глухих кутів.
        // Виклик AssignStatueRoom() йде ПІСЛЯ AssignArtifactRoom(), тому кімнату, яку вже
        // забрав артефакт, статуя повторно не займе (обидва фільтрують r.type == RoomType.Normal)
        AssignArtifactRoom();
        AssignStatueRoom();

        Dictionary<int, int> roomDistances = ComputeRoomDistancesFromStart();

        List<RoomData> deadEndCandidates = rooms
            .Where(r => r.id != 0 && r.shape == RoomShape.Normal && GetRoomNeighborCount(r) == 1)
            .OrderByDescending(r => roomDistances.TryGetValue(r.id, out int d) ? d : 0)
            .ToList();

        Queue<RoomData> queue = new Queue<RoomData>(deadEndCandidates);

        // Велика 2x2 кімната боса (якщо вдалося розмістити в GenerateLayout) вже має type == Boss,
        // тому тут дораховуємо лише РЕШТУ звичайних 1-клітинкових кімнат боса, якщо bossRoomCount > 1
        int bossAlreadyAssigned = rooms.Count(r => r.type == RoomType.Boss);
        AssignSpecificType(queue, RoomType.Boss, Mathf.Max(0, bossRoomCount - bossAlreadyAssigned));
        AssignSpecificType(queue, RoomType.Treasure, treasureRoomCount);
        AssignSpecificType(queue, RoomType.Chest, chestRoomCount);

        // НЕ гарантований тип - на відміну від рядків вище, тут може призначитись
        // менше кімнат, ніж secretRoomMaxCount (аж до нуля), якщо рол не пощастить
        // або підходящих глухих кутів забракло. Це навмисна поведінка.
        AssignChanceBasedType(queue, RoomType.SecretRoom, secretRoomChance, secretRoomMaxCount);

        List<RoomData> fallback = rooms
            .Where(r => r.id != 0 && r.shape == RoomShape.Normal && r.type == RoomType.Normal)
            .OrderByDescending(r => roomDistances.TryGetValue(r.id, out int d) ? d : 0)
            .ToList();
        Queue<RoomData> fallbackQueue = new Queue<RoomData>(fallback);

        AssignSpecificType(fallbackQueue, RoomType.Boss, bossRoomCount - rooms.Count(r => r.type == RoomType.Boss));
        AssignSpecificType(fallbackQueue, RoomType.Treasure, treasureRoomCount - rooms.Count(r => r.type == RoomType.Treasure));
        AssignSpecificType(fallbackQueue, RoomType.Chest, chestRoomCount - rooms.Count(r => r.type == RoomType.Chest));
    }

    // Кімната артефакту має суворе обмеження: рівно один сусід, і він має бути ЗНИЗУ
    // (тобто гравець фізично може зайти в цю кімнату лише через її нижні двері)
    void AssignArtifactRoom()
    {
        if (artifactRoomPrefabs == null || artifactRoomPrefabs.Length == 0) return;
        if (artifactRoomCount <= 0) return;

        List<RoomData> candidates = rooms
            .Where(r => r.id != 0 && r.shape == RoomShape.Normal && r.type == RoomType.Normal)
            .Where(HasSingleSouthNeighbor)
            .OrderByDescending(r => Mathf.Abs(r.cells[0].x) + Mathf.Abs(r.cells[0].y))
            .ToList();

        int assigned = 0;
        foreach (RoomData room in candidates)
        {
            if (assigned >= artifactRoomCount) break;
            room.type = RoomType.ArtifactRoom;
            assigned++;
        }

        // Якщо серед уже згенерованих клітинок не знайшлося природного глухого кута знизу -
        // ДОБУДОВУЄМО нову клітинку спеціально під артефакт, щоб кімната з'являлась гарантовано (100%)
        if (assigned < artifactRoomCount)
            assigned += ForcePlaceSouthEntryRoom(RoomType.ArtifactRoom, artifactRoomCount - assigned, RoomType.StatueRoom);

        if (assigned < artifactRoomCount)
            Debug.LogWarning($"LevelGenerator: не вдалося розмістити {artifactRoomCount - assigned} кімнату(и) артефакту - " +
                              "навіть примусово не знайшлося вільного місця зверху від жодної кімнати.");
    }

    // Кімната статуї має те саме сувору вимогу, що й кімната артефакту: рівно один сусід,
    // і він має бути ЗНИЗУ. Окремий тип (RoomType.StatueRoom) і окремий пул префабів/лічильник,
    // тому статуя і артефакт можуть існувати в одному рівні одночасно, не заважаючи одне одному.
    void AssignStatueRoom()
    {
        if (statueRoomPrefabs == null || statueRoomPrefabs.Length == 0) return;
        if (statueRoomCount <= 0) return;

        // ВАЖЛИВО: кімната статуї НІКОЛИ не повинна опинитись поруч (по стороні клітинки)
        // з кімнатою артефакту - тому додатково відфільтровуємо кандидатів,
        // чия клітинка межує з уже призначеною кімнатою артефакту.
        List<RoomData> candidates = rooms
            .Where(r => r.id != 0 && r.shape == RoomShape.Normal && r.type == RoomType.Normal)
            .Where(HasSingleSouthNeighbor)
            .Where(r => !CellAdjacentToRoomOfType(r.cells[0], RoomType.ArtifactRoom))
            .OrderByDescending(r => Mathf.Abs(r.cells[0].x) + Mathf.Abs(r.cells[0].y))
            .ToList();

        int assigned = 0;
        foreach (RoomData room in candidates)
        {
            if (assigned >= statueRoomCount) break;
            room.type = RoomType.StatueRoom;
            assigned++;
        }

        // Так само - гарантоване добудовування, якщо природного місця не знайшлось.
        // avoidAdjacentTo: RoomType.ArtifactRoom гарантує ту саму умову й тут -
        // навіть примусово добудована статуя не стане сусідом артефакту.
        if (assigned < statueRoomCount)
            assigned += ForcePlaceSouthEntryRoom(RoomType.StatueRoom, statueRoomCount - assigned, RoomType.ArtifactRoom);

        if (assigned < statueRoomCount)
            Debug.LogWarning($"LevelGenerator: не вдалося розмістити {statueRoomCount - assigned} кімнату(и) статуї - " +
                              "навіть примусово не знайшлося вільного місця зверху від жодної кімнати.");
    }

    // Примусово добудовує НОВУ 1-клітинкову кімнату вказаного типу точно зверху від якоїсь
    // уже існуючої кімнати (щоб зайти в неї можна було тільки знизу). Використовується як
    // гарантований запасний варіант, коли випадкова генерація не залишила придатного глухого кута.
    // Шукає серед НАЙВІДДАЛЕНІШИХ від старту кімнат спершу, щоб не псувати ранню частину рівня.
    int ForcePlaceSouthEntryRoom(RoomType type, int neededCount, RoomType? avoidAdjacentTo = null)
    {
        if (neededCount <= 0) return 0;

        Dictionary<int, int> roomDistances = ComputeRoomDistancesFromStart();

        List<RoomData> baseCandidates = rooms
            .Where(r => r.shape == RoomShape.Normal || r.shape == RoomShape.Big)
            .OrderByDescending(r => roomDistances.TryGetValue(r.id, out int d) ? d : 0)
            .ToList();

        int placed = 0;
        foreach (RoomData baseRoom in baseCandidates)
        {
            if (placed >= neededCount) break;

            foreach (Vector2Int baseCell in baseRoom.cells)
            {
                Vector2Int newCell = baseCell + Vector2Int.up;
                if (cellOwner.ContainsKey(newCell)) continue;

                // Нова клітинка має мати РІВНО одного сусіда - і саме знизу (нашу базову кімнату)
                if (CountDistinctNeighborRooms(newCell, new HashSet<Vector2Int> { newCell }, -1) != 1) continue;

                Vector2Int southNeighbor = newCell + Vector2Int.down;
                if (!cellOwner.TryGetValue(southNeighbor, out int southOwnerId) || southOwnerId != baseRoom.id) continue;

                // Ніколи не добудовуємо кімнату впритул до кімнати "протилежного" спецтипу
                // (наприклад, статую - поруч з артефактом, і навпаки).
                if (avoidAdjacentTo.HasValue && CellAdjacentToRoomOfType(newCell, avoidAdjacentTo.Value)) continue;

                CreateRoom(new List<Vector2Int> { newCell }, RoomShape.Normal, 0);
                rooms[rooms.Count - 1].type = type;
                placed++;
                break;
            }
        }

        return placed;
    }

    // Перевіряє, чи клітинка cell межує (по будь-якій з 4 сторін) з клітинкою,
    // що належить кімнаті вказаного типу. Використовується, щоб гарантувати,
    // що кімната артефакту і кімната статуї НІКОЛИ не опиняються поруч одна з одною.
    bool CellAdjacentToRoomOfType(Vector2Int cell, RoomType type)
    {
        foreach (Vector2Int dir in Directions)
        {
            Vector2Int n = cell + dir;
            if (cellOwner.TryGetValue(n, out int otherId) && rooms[otherId].type == type)
                return true;
        }
        return false;
    }

    bool HasSingleSouthNeighbor(RoomData room)
    {
        Vector2Int cell = room.cells[0];
        int neighborCount = 0;
        bool neighborIsSouth = false;

        foreach (Vector2Int dir in Directions)
        {
            Vector2Int n = cell + dir;
            if (cellOwner.TryGetValue(n, out int otherId) && otherId != room.id)
            {
                neighborCount++;
                if (dir == Vector2Int.down) neighborIsSouth = true;
            }
        }

        return neighborCount == 1 && neighborIsSouth;
    }

    void AssignSpecificType(Queue<RoomData> queue, RoomType type, int count)
    {
        for (int i = 0; i < count && queue.Count > 0; i++)
        {
            RoomData room = queue.Dequeue();
            if (room.type == RoomType.Normal)
                room.type = type;
        }
    }

    // На відміну від AssignSpecificType - НЕ гарантує появу заданої кількості кімнат.
    // Кожен кандидат із черги проходить незалежний рол Random.value < chance; якщо не пощастило -
    // кімната повертається в чергу (щоб її ще могли забрати наступні виклики AssignSpecificType/
    // AssignChanceBasedType), а не "згорає" назавжди. Зупиняємось, щойно назбирали maxCount
    // або перебрали всю чергу один раз.
    void AssignChanceBasedType(Queue<RoomData> queue, RoomType type, float chance, int maxCount)
    {
        if (maxCount <= 0) return;

        int assigned = 0;
        int roomsToCheck = queue.Count; // фіксуємо початковий розмір - інакше requeue зациклить перебір

        for (int i = 0; i < roomsToCheck && assigned < maxCount; i++)
        {
            RoomData room = queue.Dequeue();

            if (room.type == RoomType.Normal && Random.value < chance)
            {
                room.type = type;
                assigned++;
            }
            else
            {
                queue.Enqueue(room);
            }
        }
    }

    int GetRoomNeighborCount(RoomData room)
    {
        HashSet<Vector2Int> ownCells = new HashSet<Vector2Int>(room.cells);
        HashSet<int> neighborIds = new HashSet<int>();
        foreach (Vector2Int cell in room.cells)
        {
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int n = cell + dir;
                if (ownCells.Contains(n)) continue;
                if (cellOwner.TryGetValue(n, out int otherId))
                    neighborIds.Add(otherId);
            }
        }
        return neighborIds.Count;
    }

    GameObject GetPrefabForRoom(RoomData room)
    {
        if (room.shape == RoomShape.Square)
            return bigBossRoomPrefabs[Random.Range(0, bigBossRoomPrefabs.Length)];
        if (room.shape == RoomShape.Big)
            return bigRoomPrefabs[Random.Range(0, bigRoomPrefabs.Length)];

        GameObject[] pool = room.type switch
        {
            RoomType.Start => new GameObject[] { startRoomPrefab },
            RoomType.ArtifactRoom => artifactRoomPrefabs,
            RoomType.StatueRoom => statueRoomPrefabs,
            RoomType.SecretRoom => secretRoomPrefabs,
            _ => normalRoomPrefabs
        };

        if (pool == null || pool.Length == 0)
            return normalRoomPrefabs[Random.Range(0, normalRoomPrefabs.Length)];

        return pool[Random.Range(0, pool.Length)];
    }

    void SpawnRooms()
    {
        foreach (RoomData room in rooms)
        {
            GameObject prefab = GetPrefabForRoom(room);

            Vector3 center = Vector3.zero;
            foreach (Vector2Int cell in room.cells)
                center += new Vector3(cell.x * roomWidth, cell.y * roomHeight, 0f);
            center /= room.cells.Count;

            Quaternion rotation = Quaternion.Euler(0f, 0f, -90f * room.rotationSteps);
            GameObject roomObj = Instantiate(prefab, center, rotation, transform);

            RoomController controller = roomObj.GetComponent<RoomController>();
            if (controller != null)
            {
                controller.gridPos = room.cells[0];
                controller.roomType = room.type;

                Vector2 size = room.shape switch
                {
                    RoomShape.Big => new Vector2(roomWidth * 2, roomHeight),
                    RoomShape.Square => new Vector2(roomWidth * 2, roomHeight * 2),
                    _ => new Vector2(roomWidth, roomHeight)
                };

                controller.SetupBounds(size);

                controller.ConfigureDoors(room.rotationSteps, (cellWorldPos, worldDir) =>
                {
                    Vector2Int neighborCell = cellWorldPos + worldDir;
                    return cellOwner.TryGetValue(neighborCell, out int otherId) && otherId != room.id;
                });
            }

            if (room.type == RoomType.Start)
            {
                startRoomWorldPos = center;

                // Одразу активуємо стартову кімнату в камері
                if (CameraController.Instance != null && controller != null)
                {
                    CameraController.Instance.SetActiveRoom(controller);
                }
            }
        }
        if (MinimapUI.Instance != null)
        {
            MinimapUI.Instance.GenerateMinimap(rooms);
        }
    }

    void PlacePlayerAtStart()
    {
        if (player != null)
            player.transform.position = startRoomWorldPos;

        if (CameraController.Instance != null)
        {
            if (player != null)
                CameraController.Instance.player = player.transform;

            // ПРАВИЛЬНО: Передаємо тип Vector3 (координати центру стартової кімнати)
            CameraController.Instance.SnapToRoom(startRoomWorldPos);
        }
    }
}