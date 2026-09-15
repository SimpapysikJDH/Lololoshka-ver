using System.Collections.Generic;
using UnityEngine;

// Цей компонент автоматично додається гравцю через ArtifactData (enableStations = true).
// Не потрібно вручну вішати його на гравця в редакторі.
//
// Логіка:
// 1. Раз на "spawnInterval" секунд ставить нову станцію в поточній позиції гравця.
// 2. Кожна станція живе "stationLifetime" секунд, потім зникає сама (StationInstance).
// 3. Якщо активних станцій більше 2 (тобто 3+), вони з'єднуються лініями по колу
//    (1-2, 2-3, ..., N-1), і кожна лінія періодично б'є ворогів, що в неї потрапляють.
[DisallowMultipleComponent]
public class StationController : MonoBehaviour
{
    [Header("Базові налаштування (можна міняти в інспекторі гравця)")]
    public bool isEnabled = false;
    public GameObject stationPrefab;

    [Header("Базові стати (доповнюються бонусами з артефактів)")]
    public int maxStations = 3;
    public float stationLifetime = 6f;
    public float spawnInterval = 4f;
    public float minSpawnInterval = 0.5f; // щоб інтервал не пішов у нуль/мінус

    [Header("Розміщення станцій")]
    // Якщо true - станція ставиться замість кульки при атаці гравця (PlayerAttack.Shoot),
    // а автоматичне розставляння по таймеру (нижче) вимикається.
    // Якщо false - працює старий режим: станції ставляться самі по таймеру spawnInterval.
    public bool placeOnAttack = true;

    [Header("З'єднувальна лінія (від 3 станцій і більше)")]
    public int minStationsToLink = 3; // ">2" станції
    public float linkWidth = 0.15f;
    public float damageTickInterval = 0.25f;
    public LayerMask enemyLayer; // виставити в інспекторі шар ворогів; якщо порожньо - спробує знайти шар "Enemy"
    public Color linkColor = new Color(1f, 0.3f, 0.3f, 0.85f);
    public Material linkMaterial; // необов'язково, якщо null - буде створено дефолтний sprite-матеріал

    [Header("Кастомний вигляд лінії (необов'язково)")]
    public Texture2D linkTexture;              // текстура для лінії (ваш PNG - "стрічка" з градієнтом впоперек товщини і т.д.)
    public float linkTextureTilingPerUnit = 0.5f; // скільки повторів текстури на 1 юніт довжини лінії
    public float linkScrollSpeed = 1.5f;       // швидкість "перетікання" текстури вздовж лінії
    public Gradient linkColorGradient;         // якщо задано (має ключі) - переважає над linkColor, дає плавний перехід кольорів ВЗДОВЖ ДОВЖИНИ лінії

    [Header("Homing - вигин лінії станцій в бік ворогів (реагує на PlayerStats.projectileHoming)")]
    // Суто візуальний ефект - шкода лінії (ApplyLinkDamage) як і раніше рахується по прямій
    // капсулі між станціями, вигин на неї не впливає.
    public int linkCurvePoints = 14;          // на скільки точок розбивати вигнуту лінію (тільки поки Homing активний)
    public float linkHomingSearchRadius = 3f; // в межах якого радіусу від середини лінії шукати найближчого ворога

    [Header("Homing - вигин лазерного кільця (комбо) в бік ворогів")]
    public float ringHomingSearchRadius = 2.5f; // на скільки далі за сам радіус кільця шукати ворогів для вигину

    [Header("Комбо: Орбіта + Станції -> лазерне кільце навколо гравця")]
    // Вмикається автоматично в ArtifactData.ApplyEffect, коли гравець одночасно має
    // артефакт орбіти (PlayerStats.projectileOrbit) і артефакт станцій.
    public bool comboOrbitLaserEnabled = false;
    public float comboRingMinRadius = 1f;       // страховка, якщо attackRange гравця чомусь 0
    public float comboRingScrollSpeed = 2f;     // швидкість "перетікання" лазера по колу (чисто візуал, з статами не пов'язано)
    public float comboRingDuration = 4f;        // ГОЛОВНЕ - кільце живе обмежений час, потім зникає
    public float comboRingWidth = 0.25f;
    public int comboRingSegments = 48;
    private OrbitLaserRing activeComboRing;     // поки жива поточна - нову не ставимо (кулдаун між кастами дає stats.attackCooldown в PlayerAttack)

    // Підписуйся на цю подію (наприклад, в PlayerStatsUI), щоб оновлювати панель статів
    // на екрані щоразу, коли артефакт міняє стати станцій.
    public event System.Action OnStatsChanged;

    // Підписуйся на цю подію, щоб показати спливаючу підказку "+2" / "-1" біля конкретного стату.
    // statKey - ключ стату ("stationMaxStations", "stationLifetime", "stationSpawnInterval",
    // "stationLinkWidth"), delta - реальна зміна значення (може відрізнятись від переданого
    // amount через Clamp/Max). Шкода лінії БІЛЬШЕ НЕ окремий стат - вона завжди дорівнює
    // PlayerStats.damage (див. ApplyLinkDamage), тому її зміна показується як звичайний
    // popup стату "damage" в PlayerStatsUI, без жодного окремого ключа тут.
    public event System.Action<string, float> OnStatChanged;

    private void NotifyStatsChanged(string statKey = null, float delta = 0f)
    {
        OnStatsChanged?.Invoke();
        if (statKey != null && delta != 0f)
            OnStatChanged?.Invoke(statKey, delta);
    }

    private readonly List<StationInstance> activeStations = new List<StationInstance>();
    private readonly List<LineRenderer> linkRenderers = new List<LineRenderer>();

    // Для UI - скільки станцій зараз активно (наприклад, показати "2/3").
    public int ActiveStationCount => activeStations.Count;

    private Transform container; // тримає всі станції та лінії, щоб не смітити в ієрархії
    private float spawnTimer;
    private float damageTickTimer;

    // Шкода лінії між станціями = звичайна шкода атаки гравця (PlayerStats.damage) -
    // це той самий стат, не окремий. Беремо посилання один раз в Awake().
    private PlayerStats stats;

    // Дозволяє UI (PlayerStatsUI) підписатись на стати ЩОЙНО створеного StationController
    // СИНХРОННО, в момент AddComponent - без цього перші ж зміни статів одразу після
    // підбору артефакта (EnableFeature/AddLinkWidth/тощо) проходили б повз UI, бо панель
    // встигала підписатись лише пізніше, за таймером.
    public static event System.Action<StationController> OnAnyStationControllerCreated;

    void Awake()
    {
        EnsureContainer();
        spawnTimer = spawnInterval;
        stats = GetComponent<PlayerStats>();

        if (enemyLayer.value == 0)
        {
            int guessed = LayerMask.NameToLayer("Enemy");
            if (guessed != -1)
            {
                enemyLayer = 1 << guessed;
            }
            else
            {
                // StationController додається В РАНТАЙМІ (AddComponent), тому це поле НЕ можна
                // заздалегідь виставити в інспекторі гравця - воно завжди стартує як 0.
                // Якщо шар "Enemy" не знайдено за назвою - краще перевіряти УСІ шари і фільтрувати
                // по IDamageable/тегу гравця (див. ApplyLinkDamage), ніж мовчки не влучати НІКУДИ (маска 0).
                enemyLayer = ~0;
                Debug.LogWarning("[StationController] Шар 'Enemy' не знайдено за назвою - лінії/кільце перевірятимуть УСІ шари. Для точності призначте enemyLayer вручну через ArtifactData.stationEnemyLayerOverride (див. ArtifactData.cs).");
            }
        }

        OnAnyStationControllerCreated?.Invoke(this);
    }

    void OnEnable()
    {
        // Коли гравець переходить в ІНШУ кімнату - всі станції, лінії між ними і лазерне кільце
        // (якщо активне) мають одразу зникнути, а не лишатись висіти в старій кімнаті.
        RoomController.OnAnyRoomEntered += ClearAllStations;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomEntered -= ClearAllStations;
    }

    void OnDestroy()
    {
        // Станції та лінії живуть окремо від гравця (щоб не тягнулись за ним),
        // тому при знищенні гравця їх треба прибрати вручну.
        ClearAllStations();

        if (container != null)
            Destroy(container.gameObject);
    }

    // Прибирає ВСІ активні станції, лінії між ними і лазерне кільце (якщо активне) -
    // викликається як при переході в іншу кімнату (OnAnyRoomEntered), так і при знищенні гравця (OnDestroy).
    public void ClearAllStations()
    {
        for (int i = activeStations.Count - 1; i >= 0; i--)
        {
            if (activeStations[i] != null)
                Destroy(activeStations[i].gameObject);
        }
        activeStations.Clear();

        for (int i = linkRenderers.Count - 1; i >= 0; i--)
        {
            if (linkRenderers[i] != null)
                Destroy(linkRenderers[i].gameObject);
        }
        linkRenderers.Clear();

        if (activeComboRing != null)
        {
            Destroy(activeComboRing.gameObject);
            activeComboRing = null;
        }

        spawnTimer = spawnInterval; // скидаємо на випадок старого автоматичного режиму (placeOnAttack = false)
    }

    void Update()
    {
        if (!isEnabled) return;

        // В режимі placeOnAttack станції ставить PlayerAttack.Shoot() через PlaceStationAt(),
        // тому автоматичний таймер тут не потрібен - інакше станції ставились би і від атаки, і самі по собі.
        if (!placeOnAttack)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnStation();
                spawnTimer = Mathf.Max(minSpawnInterval, spawnInterval);
            }
        }

        UpdateLinks();

        damageTickTimer -= Time.deltaTime;
        if (damageTickTimer <= 0f)
        {
            damageTickTimer = damageTickInterval;
            ApplyLinkDamage();
        }
    }

    // ---------- Виклики з ArtifactData.ApplyEffect ----------

    public void EnableFeature(GameObject prefabOverride)
    {
        isEnabled = true;

        if (prefabOverride != null)
            stationPrefab = prefabOverride;

        // Панель статів має з'явитись/оновитись одразу, коли фіча станцій щойно увімкнулась артефактом.
        NotifyStatsChanged();
    }

    public void SetPlaceOnAttack(bool value)
    {
        placeOnAttack = value;
        NotifyStatsChanged();
    }

    // Викликається з ArtifactData.ApplyEffect, коли гравець має ОБИДВА артефакти:
    // орбіту (PlayerStats.projectileOrbit) і станції.
    public void EnableComboOrbitLaser()
    {
        comboOrbitLaserEnabled = true;
    }

    // Викликається з PlayerAttack.Shoot(), коли комбо активне - ставить лазерне кільце
    // навколо гравця ЗАМІСТЬ кульки і замість звичайної станції-крапки.
    // statDamage / statRange - ЗВИЧАЙНІ стати гравця (ті самі, що йдуть в Projectile.Setup) -
    // радіус кільця завжди = attackRange (1:1, без окремого "розміру"), шкода за тік = damage.
    public void SpawnComboOrbitLaser(Transform playerAnchor, float statDamage, float statRange)
    {
        if (!isEnabled || !comboOrbitLaserEnabled) return;

        // Кулдаун між кастами вже дає stats.attackCooldown (перевірка в PlayerAttack.Update()) -
        // тут лише не даємо заспавнити ДРУГЕ кільце, поки перше ще не зникло.
        if (activeComboRing != null) return;

        EnsureContainer();

        GameObject ringObj = new GameObject("ComboOrbitLaserRing");
        ringObj.transform.SetParent(container);
        ringObj.transform.position = playerAnchor.position;

        float ringRadius = Mathf.Max(comboRingMinRadius, statRange);
        float ringDamagePerTick = Mathf.Max(0f, statDamage);

        // Homing - той самий стат, що вмикає вигин снарядів, тепер додатково змушує
        // й лазерне кільце "тягнутись" в бік найближчих ворогів замість ідеального кола.
        bool homingActive = stats != null && stats.projectileHoming;
        float homingStrengthValue = stats != null ? stats.projectileHomingStrength : 0f;

        OrbitLaserRing ring = ringObj.AddComponent<OrbitLaserRing>();
        ring.Setup(
            this,
            playerAnchor,
            ringRadius,
            comboRingScrollSpeed,
            comboRingDuration,
            comboRingWidth,
            ringDamagePerTick,
            damageTickInterval,
            enemyLayer,
            linkColor,
            linkTexture,
            linkColorGradient,
            comboRingSegments,
            homingActive,
            homingStrengthValue,
            ringHomingSearchRadius);

        activeComboRing = ring;
    }

    // Викликається самим кільцем (OrbitLaserRing), коли час вийшов і воно себе знищило.
    public void OnComboRingExpired()
    {
        activeComboRing = null;
    }

    public void AddMaxStations(int amount)
    {
        if (amount == 0) return;
        int oldValue = maxStations;
        maxStations = Mathf.Max(1, maxStations + amount);
        NotifyStatsChanged("stationMaxStations", maxStations - oldValue);
    }

    public void AddStationLifetime(float amount)
    {
        if (amount == 0f) return;
        float oldValue = stationLifetime;
        stationLifetime = Mathf.Max(0.5f, stationLifetime + amount);
        NotifyStatsChanged("stationLifetime", stationLifetime - oldValue);
    }

    public void ReduceSpawnInterval(float amount)
    {
        if (amount == 0f) return;
        float oldValue = spawnInterval;
        spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - amount);
        NotifyStatsChanged("stationSpawnInterval", spawnInterval - oldValue);
    }

    public void AddLinkWidth(float amount)
    {
        if (amount == 0f) return;
        float oldValue = linkWidth;
        linkWidth = Mathf.Max(0.02f, linkWidth + amount);
        NotifyStatsChanged("stationLinkWidth", linkWidth - oldValue);
    }

    // На відміну від AddLinkWidth (додає бонус до поточного значення) -
    // виставляє стат РІВНО на передане значення, незалежно від того, що було до цього
    // (як comboFixedDamage і т.д. в ArtifactData). Шкоди лінії тут більше немає -
    // вона завжди = PlayerStats.damage, міняти яку треба напряму через stats.AddDamage().
    public void SetLinkWidth(float value)
    {
        float oldValue = linkWidth;
        linkWidth = Mathf.Max(0.02f, value);
        NotifyStatsChanged("stationLinkWidth", linkWidth - oldValue);
    }

    // ---------- Станції ----------

    private void EnsureContainer()
    {
        if (container != null) return;

        GameObject containerObj = new GameObject($"StationsContainer_{name}");
        container = containerObj.transform;
        container.position = Vector3.zero;
        // Свідомо НЕ робимо container дочірнім до гравця - станції мають лишатись
        // на місці, поки гравець йде далі.
    }

    // Викликається з PlayerAttack.Shoot(), коли активний режим placeOnAttack -
    // ставить станцію в заданій позиції ЗАМІСТЬ пострілу кулькою.
    public void PlaceStationAt(Vector3 worldPos)
    {
        if (!isEnabled) return;
        SpawnStation(worldPos);
    }

    // Старий автоматичний режим (placeOnAttack = false) - ставить станцію на позиції гравця.
    private void SpawnStation()
    {
        SpawnStation(transform.position);
    }

    private void SpawnStation(Vector3 spawnPos)
    {
        // Якщо ліміт досягнуто - прибираємо найстарішу станцію, щоб звільнити місце.
        if (activeStations.Count >= maxStations && activeStations.Count > 0)
        {
            activeStations[0].ForceExpire();
        }

        GameObject stationObj;
        if (stationPrefab != null)
        {
            stationObj = Instantiate(stationPrefab, spawnPos, Quaternion.identity, container);
        }
        else
        {
            // Фолбек-візуал, якщо префаб не призначили в ArtifactData.
            stationObj = new GameObject("Station (default)");
            stationObj.transform.SetParent(container);
            stationObj.transform.position = spawnPos;
            SpriteRenderer sr = stationObj.AddComponent<SpriteRenderer>();
            sr.color = Color.yellow;
            Debug.LogWarning("[StationController] stationPrefab не призначено в ArtifactData - використовується заглушка без спрайта. Признач префаб станції для нормального вигляду.");
        }

        StationInstance instance = stationObj.GetComponent<StationInstance>();
        if (instance == null)
            instance = stationObj.AddComponent<StationInstance>();

        instance.Setup(this, stationLifetime);
        activeStations.Add(instance);

        // Кількість активних станцій теж корисно бачити в UI (наприклад "2/3") - без цього
        // панель не оновлювалась би, поки не зміниться сам стат maxStations.
        NotifyStatsChanged();
    }

    // Викликається самою StationInstance, коли її час вийшов
    public void OnStationExpired(StationInstance instance)
    {
        activeStations.Remove(instance);
        NotifyStatsChanged();
    }

    // ---------- Лінії між станціями ----------

    private void UpdateLinks()
    {
        activeStations.RemoveAll(s => s == null);

        int stationCount = activeStations.Count;
        int neededLinks = stationCount >= minStationsToLink ? stationCount : 0; // замкнене коло 1-2-3-...-N-1

        // Прибираємо зайві лінії
        while (linkRenderers.Count > neededLinks)
        {
            LineRenderer extra = linkRenderers[linkRenderers.Count - 1];
            linkRenderers.RemoveAt(linkRenderers.Count - 1);
            if (extra != null)
                Destroy(extra.gameObject);
        }

        // Додаємо, якщо не вистачає
        while (linkRenderers.Count < neededLinks)
        {
            linkRenderers.Add(CreateLinkRenderer());
        }

        if (neededLinks == 0) return;

        // Homing - якщо в гравця активний Homing (той самий стат, що й для снарядів),
        // прямі лінії між станціями замінюються на криву, вигнуту в бік найближчого ворога.
        bool homingActive = stats != null && stats.projectileHoming;
        float homingStrengthValue = stats != null ? stats.projectileHomingStrength : 0f;

        for (int i = 0; i < stationCount; i++)
        {
            Vector3 a = activeStations[i].Position;
            Vector3 b = activeStations[(i + 1) % stationCount].Position;

            LineRenderer lr = linkRenderers[i];

            if (homingActive)
                DrawCurvedLink(lr, a, b, homingStrengthValue);
            else
                DrawStraightLink(lr, a, b);

            // Кастомний вигляд - текстура, що "перетікає" вздовж лінії (енергія/електрика тощо)
            if (linkTexture != null && lr.material != null)
            {
                float length = Vector3.Distance(a, b);
                float tiling = Mathf.Max(1f, length * linkTextureTilingPerUnit);
                lr.material.mainTextureScale = new Vector2(tiling, 1f);
                lr.material.mainTextureOffset = new Vector2(-Time.time * linkScrollSpeed, 0f);
            }
        }
    }

    // Звичайна пряма лінія (Homing вимкнено) - 2 точки, як і раніше.
    private void DrawStraightLink(LineRenderer lr, Vector3 a, Vector3 b)
    {
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    // Вигнута лінія (Homing увімкнено) - квадратична крива Безьє (a -> control -> b), де
    // контрольна точка притягнута до найближчого ворога біля середини лінії. Якщо поруч
    // ворогів немає - крива фактично лишається прямою, просто розбитою на N точок.
    private void DrawCurvedLink(LineRenderer lr, Vector3 a, Vector3 b, float homingStrengthValue)
    {
        lr.positionCount = Mathf.Max(2, linkCurvePoints);

        Vector3 mid = (a + b) * 0.5f;
        Vector3? nearestEnemy = FindNearestEnemyPos(mid, linkHomingSearchRadius);

        Vector3 control = mid;
        if (nearestEnemy.HasValue)
        {
            float maxBend = Mathf.Min(homingStrengthValue * 0.15f, Vector3.Distance(a, b) * 0.5f);
            Vector3 toEnemy = nearestEnemy.Value - mid;
            control = mid + Vector3.ClampMagnitude(toEnemy, maxBend);
        }

        int pointCount = lr.positionCount;
        for (int p = 0; p < pointCount; p++)
        {
            float t = p / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(Vector3.Lerp(a, control, t), Vector3.Lerp(control, b, t), t);
            lr.SetPosition(p, point);
        }
    }

    // Шукає найближчого ворога біля заданої точки (для вигину лінії) - не самого гравця.
    private Vector3? FindNearestEnemyPos(Vector3 fromPos, float searchRadius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(fromPos, searchRadius, enemyLayer);

        Vector3? nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.root.gameObject == gameObject)
                continue;

            float dist = Vector3.Distance(fromPos, hits[i].transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hits[i].transform.position;
            }
        }

        return nearest;
    }

    private LineRenderer CreateLinkRenderer()
    {
        GameObject lineObj = new GameObject("StationLink");
        lineObj.transform.SetParent(container);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = linkWidth;
        lr.endWidth = linkWidth;
        lr.textureMode = LineTextureMode.Tile;
        lr.numCapVertices = 8;    // заокруглені кінці лінії замість гострих зрізів
        lr.numCornerVertices = 8; // заокруглені стики між лініями (у трикутнику/багатокутнику станцій)
        lr.alignment = LineAlignment.View;

        if (linkColorGradient != null && linkColorGradient.colorKeys.Length > 0)
        {
            lr.colorGradient = linkColorGradient;
        }
        else
        {
            lr.startColor = linkColor;
            lr.endColor = linkColor;
        }

        // Робимо копію матеріалу (new Material(...)), а не спільний інстанс -
        // кожна лінія скролить текстуру незалежно, без впливу на інші об'єкти, що юзають той самий базовий матеріал.
        Material mat = linkMaterial != null ? new Material(linkMaterial) : new Material(Shader.Find("Sprites/Default"));
        if (linkTexture != null)
        {
            mat.mainTexture = linkTexture;
            // Якщо забули виставити в імпорт-налаштуваннях самого PNG - підстраховуємось тут,
            // щоб піксель-арт текстура не розмивалась і нормально тайлилась по довжині.
            linkTexture.wrapMode = TextureWrapMode.Repeat;
        }
        lr.material = mat;
        lr.sortingOrder = 5;
        return lr;
    }

    // ---------- Шкода по лінії ----------

    private void ApplyLinkDamage()
    {
        if (linkRenderers.Count == 0) return;
        if (stats == null) return;

        // Шкода лінії = звичайна шкода атаки гравця, той самий стат, без дублювання
        float damagePerSecond = stats.damage;
        if (damagePerSecond <= 0f) return;

        float damagePerTick = damagePerSecond * damageTickInterval;
        int stationCount = activeStations.Count;

        for (int i = 0; i < stationCount && i < linkRenderers.Count; i++)
        {
            Vector2 a = activeStations[i].Position;
            Vector2 b = activeStations[(i + 1) % stationCount].Position;

            Vector2 dir = b - a;
            float length = dir.magnitude;
            if (length < 0.001f) continue;

            Vector2 center = (a + b) * 0.5f;
            float angle = Vector2.SignedAngle(Vector2.up, dir);
            Vector2 capsuleSize = new Vector2(linkWidth, length + linkWidth);

            Collider2D[] hits = Physics2D.OverlapCapsuleAll(center, capsuleSize, CapsuleDirection2D.Vertical, angle, enemyLayer);
            for (int h = 0; h < hits.Length; h++)
            {
                // Гравець теж може потрапити в маску, якщо шар "Enemy" не знайдено і ми перевіряємо УСІ шари -
                // тому явно ігноруємо його власні колайдери.
                if (hits[h].transform.root.gameObject == gameObject)
                    continue;

                IDamageable damageable = hits[h].GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(damagePerTick);
            }
        }
    }
}