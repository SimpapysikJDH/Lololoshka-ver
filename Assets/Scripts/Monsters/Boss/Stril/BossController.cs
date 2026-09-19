using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Причепи на GameObject боса (той самий, де MonsterHealth і Collider2D).
// Бос СТОЇТЬ НА МІСЦІ, рухатись не вміє - лише крутиться між 3 атаками:
// 1) Лазер - 3 фіксовані промені по черзі (кожен: виростає -> активний з тіками шкоди -> вимикається)
// 2) Віяло кульок - N кульок віялом на 180° в бік гравця
// 3) Прямі кульки - декілька кульок в повністю рандомних напрямках з рандомною затримкою між ними
// Кожні attacksBeforeRest атак - перепочинок (restDuration сек), під час якого бос не атакує.
[RequireComponent(typeof(MonsterHealth))]
public class BossController : MonoBehaviour
{
    [Header("Дані боса (MonsterData - використовуються лише maxHP і дроп монет)")]
    public MonsterData data;

    [Header("Гравець (шукається автоматично за тегом Player, якщо не признач вручну)")]
    public Transform player;

    [Header("Префаб снаряду (Projectile.cs) - спільний для віяла і прямих кульок")]
    public GameObject projectilePrefab;
    public float bulletSpawnOffset = 0.6f;

    [Header("=== Лазерна атака (3 фіксовані промені, по черзі, кожен трохи лівіше) ===")]
    [Tooltip("Кути променів у градусах (0 = вправо, 90 = вгору, 180 = вліво, 270/-90 = вниз). Порядок = порядок стрільби")]
    public float[] laserAngles = { -100f, -120f, -140f };
    public float laserMaxLength = 15f;
    [Tooltip("За скільки секунд промінь візуально виростає до повної довжини (шкоди в цей час ще нема)")]
    public float laserGrowDuration = 0.4f;
    [Tooltip("Скільки секунд промінь стоїть активний на повній довжині і б'є тіками")]
    public float laserActiveDuration = 2f;
    [Tooltip("Як часто (сек) промінь завдає шкоди, поки активний")]
    public float laserTickInterval = 0.2f;
    public float laserTickDamage = 1f;
    public float laserWidth = 0.15f;
    public Color laserColor = Color.red;
    [Tooltip("Sorting Layer для лазера (як у SpriteRenderer) - постав такий самий, як в основних об'єктах сцени, або залиш пустим (Default)")]
    public string laserSortingLayerName = "";
    [Tooltip("Order in Layer для лазера - велике число, щоб промінь ЗАВЖДИ рендерився поверх боса/підлоги/гравця")]
    public int laserSortingOrder = 500;
    [Tooltip("З ким лазер взаємодіє (постав шар гравця)")]
    public LayerMask laserHitMask = ~0;

    [Header("=== Віяло кульок (180° в бік гравця) ===")]
    public int fanBulletCount = 8;
    public float fanSpreadAngle = 180f;
    public float fanBulletSpeed = 4f;
    public float fanBulletDamage = 1f;
    public float fanBulletRange = 12f;
    [Tooltip("Розмір (масштаб) кульки - 1 = оригінальний розмір префабу")]
    public float fanBulletSize = 1f;

    [Header("=== Прямі кульки (рандомні напрямки, рандомна затримка) ===")]
    public int straightMinShots = 4;
    public int straightMaxShots = 8;
    public float straightMinDelay = 0.1f;
    public float straightMaxDelay = 0.5f;
    public float straightBulletSpeed = 4f;
    public float straightBulletDamage = 1f;
    public float straightBulletRange = 12f;
    [Tooltip("Розмір (масштаб) кульки - 1 = оригінальний розмір префабу")]
    public float straightBulletSize = 1f;
    [Tooltip("Мін/макс кут напрямку кульок. 180 -> 360 = лише нижня половина кола (завжди вниз від боса)")]
    public float straightMinAngle = 180f;
    public float straightMaxAngle = 360f;

    [Header("=== Цикл атак / перепочинок ===")]
    public float startDelay = 1f;              // пауза перед першою атакою після старту сцени
    public float pauseBetweenAttacks = 1f;      // пауза між атаками, поки триває "робоча" серія
    public int attacksBeforeRest = 3;           // після скількох атак підряд бос відпочиває
    public float restDuration = 3f;             // тривалість перепочинку

    [Header("=== Анімації (Animator) ===")]
    [Tooltip("Якщо не признач - спробує знайти Animator на цьому ж GameObject")]
    public Animator animator;
    [Tooltip("Тригер для віяла кульок і прямих кульок - анімація в них однакова")]
    public string bulletAttackTriggerName = "Attack";
    [Tooltip("Тригер для лазерної атаки (спрацьовує раз на всю серію з 3 променів)")]
    public string laserAttackTriggerName = "LaserAttack";
    [Tooltip("Bool-параметр - true поки бос відпочиває (стан), false під час атак")]
    public string restingBoolName = "IsResting";
    [Tooltip("Тригер анімації смерті")]
    public string deathTriggerName = "Death";
    [Tooltip("Скільки чекати після тригера смерті, перш ніж знищити об'єкт (щоб анімація встигла програтись)")]
    public float deathAnimDuration = 1f;

    // Підписуйся в UI, щоб показувати назву поточної атаки
    public event Action<string> OnAttackStarted;

    // Підписуйся в UI таймера стану: (isResting, тривалість перепочинку)
    public event Action<bool, float> OnRestStateChanged;

    public bool IsResting { get; private set; } = false;
    public float RestTimeRemaining { get; private set; } = 0f;

    private MonsterHealth health;
    private LineRenderer[] laserRenderers;
    private Coroutine loopRoutine;

    void Awake()
    {
        health = GetComponent<MonsterHealth>();
        if (animator == null)
            animator = GetComponent<Animator>();

        SetupLaserRenderer();
    }

    void Start()
    {
        if (data != null)
            health.Initialize(data);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Смерть контролюємо самі - спершу програємо анімацію, потім знищуємо об'єкт
        health.destroyOnDeath = false;
        health.OnDeath += HandleDeath;

        loopRoutine = StartCoroutine(BossLoopRoutine());
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    // ---------- Основний цикл ----------

    private int attacksDone = 0;

    IEnumerator BossLoopRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (!health.IsDead)
        {
            yield return StartCoroutine(PerformRandomAttack());

            if (health.IsDead) yield break;

            attacksDone++;

            if (attacksDone >= attacksBeforeRest)
            {
                attacksDone = 0;
                yield return StartCoroutine(RestRoutine());
            }
            else
            {
                yield return new WaitForSeconds(pauseBetweenAttacks);
            }
        }
    }

    IEnumerator RestRoutine()
    {
        IsResting = true;
        RestTimeRemaining = restDuration;
        OnRestStateChanged?.Invoke(true, restDuration);
        SetAnimBool(restingBoolName, true);

        while (RestTimeRemaining > 0f)
        {
            RestTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        RestTimeRemaining = 0f;
        IsResting = false;
        OnRestStateChanged?.Invoke(false, 0f);
        SetAnimBool(restingBoolName, false);
    }

    IEnumerator PerformRandomAttack()
    {
        int choice = UnityEngine.Random.Range(0, 3); // 0 = лазер, 1 = віяло, 2 = прямі рандом кульки

        switch (choice)
        {
            case 0:
                OnAttackStarted?.Invoke("Лазер");
                SetAnimTrigger(laserAttackTriggerName);
                yield return StartCoroutine(LaserAttackRoutine());
                break;
            case 1:
                OnAttackStarted?.Invoke("Віяло кульок");
                SetAnimTrigger(bulletAttackTriggerName);
                yield return StartCoroutine(FanBulletAttackRoutine());
                break;
            case 2:
                OnAttackStarted?.Invoke("Прямі кульки");
                SetAnimTrigger(bulletAttackTriggerName);
                yield return StartCoroutine(StraightRandomAttackRoutine());
                break;
        }
    }

    void HandleDeath()
    {
        // Зупиняємо все (цикл атак, лазер, кульки), програємо анімацію смерті,
        // і вже після неї знищуємо об'єкт (health.destroyOnDeath = false в Start())
        StopAllCoroutines();

        if (laserRenderers != null)
        {
            foreach (LineRenderer lr in laserRenderers)
                if (lr != null)
                    lr.enabled = false;
        }

        SetAnimTrigger(deathTriggerName);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimDuration);
        Destroy(gameObject);
    }

    // ---------- Хелпери для Animator (щоб не дублювати null/empty перевірки скрізь) ----------

    void SetAnimTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
            animator.SetTrigger(triggerName);
    }

    void SetAnimBool(string boolName, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(boolName))
            animator.SetBool(boolName, value);
    }

    // ---------- Лазерна атака ----------

    void SetupLaserRenderer()
    {
        laserRenderers = new LineRenderer[laserAngles.Length];

        for (int i = 0; i < laserAngles.Length; i++)
        {
            GameObject laserObj = new GameObject("BossLaserBeam_" + i);
            laserObj.transform.SetParent(transform);
            laserObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = laserObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.startWidth = laserWidth;
            lr.endWidth = laserWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = laserColor;
            lr.endColor = laserColor;
            lr.sortingLayerName = laserSortingLayerName;
            lr.sortingOrder = laserSortingOrder;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);
            lr.enabled = false;

            laserRenderers[i] = lr;
        }
    }

    // Спочатку грає лише анімація атаки (тригер ставиться в PerformRandomAttack ДО виклику цієї корутини),
    // а самі промені (усі одразу, паралельно) виростають і б'ють тіками одночасно.
    IEnumerator LaserAttackRoutine()
    {
        Vector2[] directions = new Vector2[laserAngles.Length];
        for (int i = 0; i < laserAngles.Length; i++)
            directions[i] = AngleToDirection(laserAngles[i]);

        for (int i = 0; i < laserRenderers.Length; i++)
        {
            laserRenderers[i].enabled = true;
            laserRenderers[i].SetPosition(0, Vector3.zero);
            laserRenderers[i].SetPosition(1, Vector3.zero);
        }

        // Фаза росту - усі промені одночасно видовжуються, шкоди ще НЕМА (це попередження)
        float t = 0f;
        while (t < laserGrowDuration)
        {
            t += Time.deltaTime;
            float currentLength = Mathf.Lerp(0f, laserMaxLength, t / laserGrowDuration);
            for (int i = 0; i < laserRenderers.Length; i++)
                laserRenderers[i].SetPosition(1, directions[i] * currentLength);
            yield return null;
        }
        for (int i = 0; i < laserRenderers.Length; i++)
            laserRenderers[i].SetPosition(1, directions[i] * laserMaxLength);

        // Фаза активної шкоди - усі промені стоять на повній довжині і б'ють тіками одночасно
        float activeTimer = 0f;
        float tickTimer = laserTickInterval; // перший тік одразу, як тільки промені виросли
        while (activeTimer < laserActiveDuration)
        {
            activeTimer += Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= laserTickInterval)
            {
                tickTimer = 0f;
                for (int i = 0; i < directions.Length; i++)
                    DealLaserDamage(directions[i]);
            }

            yield return null;
        }

        for (int i = 0; i < laserRenderers.Length; i++)
            laserRenderers[i].enabled = false;
    }

    void DealLaserDamage(Vector2 direction)
    {
        // ContactFilter2D з useTriggers = true - інакше промінь ІГНОРУЄ тригер-колайдери
        // (а хітбокс/колайдер гравця дуже часто саме тригер), і це залежить від глобальної
        // настройки Queries Hit Triggers в Project Settings, яку легко забути ввімкнути.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(laserHitMask);
        filter.useTriggers = true;

        List<RaycastHit2D> hits = new List<RaycastHit2D>();
        int hitCount = Physics2D.Raycast(transform.position, direction, filter, hits, laserMaxLength);

        // ТИМЧАСОВА ДІАГНОСТИКА - постав курсор на боса, ввімкни Gizmos в Game view,
        // запусти лазерну атаку і подивись Console + жовтий промінь в Scene view.
        // Коли проблему знайдемо - весь цей Debug-блок можна прибрати.
        Debug.DrawRay(transform.position, direction * laserMaxLength, Color.yellow, laserTickInterval);
        if (hitCount == 0)
            Debug.Log($"[BossLaser] Raycast у напрямку {direction} нічого не знайшов (0 хітів). Перевір Laser Hit Mask і чи є колайдер на шляху променя.");

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;

            // Ігноруємо колайдери самого боса (і будь-які дочірні, напр. якщо колайдер висить на дитячому об'єкті)
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                // Не damageable (кімната/фон/тригер меж рівня тощо) - не блокує промінь, шукаємо далі
                Debug.Log($"[BossLaser] {hit.collider.name} без IDamageable - пропускаю, шукаю далі.");
                continue;
            }

            damageable.TakeDamage(laserTickDamage);
            Debug.Log($"[BossLaser] Влучив у {hit.collider.name}, завдав {laserTickDamage} шкоди.");
            break; // знайшли реальну ціль - зупиняємось, далі промінь не пробиває
        }

    }

    // ---------- Віяло кульок (180°) ----------

    IEnumerator FanBulletAttackRoutine()
    {
        if (projectilePrefab == null || player == null) yield break;

        Vector2 centerDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

        for (int i = 0; i < fanBulletCount; i++)
        {
            Vector2 dir = GetFanDirection(centerDir, i, fanBulletCount);
            SpawnBullet(dir, fanBulletSpeed, fanBulletDamage, fanBulletRange, fanBulletSize);
        }

        yield return null;
    }

    // Розкидає fanBulletCount напрямків симетрично навколо centerDir на fanSpreadAngle градусів сумарно
    Vector2 GetFanDirection(Vector2 centerDir, int index, int total)
    {
        if (total <= 1) return centerDir;

        // Захист: якщо в інспекторі випадково виставили >360 (або від'ємне) - обрізаємо,
        // інакше "віяло" буквально обійде все коло замість очікуваного конуса
        float clampedSpread = Mathf.Clamp(fanSpreadAngle, 0f, 360f);

        float step = clampedSpread / (total - 1);
        float startAngle = -clampedSpread / 2f;
        float angleOffset = startAngle + step * index;

        return RotateVector(centerDir, angleOffset);
    }

    // ---------- Прямі кульки (рандомний напрямок, рандомна затримка) ----------

    IEnumerator StraightRandomAttackRoutine()
    {
        if (projectilePrefab == null) yield break;

        int shots = UnityEngine.Random.Range(straightMinShots, straightMaxShots + 1);

        for (int i = 0; i < shots; i++)
        {
            float randomAngle = UnityEngine.Random.Range(straightMinAngle, straightMaxAngle);
            Vector2 dir = AngleToDirection(randomAngle);
            SpawnBullet(dir, straightBulletSpeed, straightBulletDamage, straightBulletRange, straightBulletSize);

            float delay = UnityEngine.Random.Range(straightMinDelay, straightMaxDelay);
            yield return new WaitForSeconds(delay);
        }
    }

    // ---------- Спільне ----------

    void SpawnBullet(Vector2 direction, float speed, float damage, float range, float size)
    {
        Vector2 spawnPos = (Vector2)transform.position + direction * bulletSpawnOffset;
        GameObject bulletObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        bulletObj.transform.localScale = Vector3.one * size;
        Projectile projectile = bulletObj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Setup(
                direction: direction,
                speed: speed,
                dmg: damage,
                range: range,
                shooter: gameObject
            );
        }
    }

    Vector2 AngleToDirection(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void OnDrawGizmosSelected()
    {
        // Лазер - фіксовані промені, кожен своїм кутом
        Gizmos.color = Color.red;
        foreach (float angle in laserAngles)
            DrawGizmoRay(angle, laserMaxLength);

        // Віяло кульок - конус на fanSpreadAngle навколо напрямку на гравця
        // (якщо гравець не признач./не в Play Mode - прев'ю показує вниз за замовчуванням)
        Gizmos.color = Color.yellow;
        Vector2 fanCenterDir = GetFanPreviewCenterDirection();
        float fanCenterAngle = Mathf.Atan2(fanCenterDir.y, fanCenterDir.x) * Mathf.Rad2Deg;
        float clampedFanSpread = Mathf.Clamp(fanSpreadAngle, 0f, 360f); // той самий захист, що й в GetFanDirection
        int fanPreviewCount = Mathf.Max(fanBulletCount, 2);
        for (int i = 0; i < fanPreviewCount; i++)
        {
            float step = clampedFanSpread / (fanPreviewCount - 1);
            float startAngle = fanCenterAngle - clampedFanSpread / 2f;
            DrawGizmoRay(startAngle + step * i, fanBulletRange);
        }

        // Прямі кульки - межі конуса рандомного розкиду (straightMinAngle -> straightMaxAngle)
        Gizmos.color = Color.cyan;
        DrawGizmoRay(straightMinAngle, straightBulletRange);
        DrawGizmoRay(straightMaxAngle, straightBulletRange);

        // Кілька коротших проміжних променів всередині конуса - лише для наочності розкиду
        int straightPreviewSteps = 5;
        for (int i = 1; i < straightPreviewSteps; i++)
        {
            float angle = Mathf.Lerp(straightMinAngle, straightMaxAngle, i / (float)straightPreviewSteps);
            DrawGizmoRay(angle, straightBulletRange * 0.6f);
        }
    }

    void DrawGizmoRay(float angleDegrees, float length)
    {
        Vector2 dir = AngleToDirection(angleDegrees);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(dir * length));
    }

    // Для прев'ю віяла в редакторі: якщо гравець признач. (або йде Play Mode і скрипт його знайшов) -
    // напрямок на нього, інакше просто вниз, щоб було що показати навіть поза Play Mode
    Vector2 GetFanPreviewCenterDirection()
    {
        if (player != null)
            return ((Vector2)player.position - (Vector2)transform.position).normalized;

        return Vector2.down;
    }
}