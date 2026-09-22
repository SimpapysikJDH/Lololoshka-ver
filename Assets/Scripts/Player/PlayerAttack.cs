using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

// Причепи на GameObject гравця (той самий, де PlayerMovement)
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerAttack : MonoBehaviourPun
{
    [Header("Префаб кульки (Projectile.cs на ньому)")]
    public GameObject projectilePrefab;

    [Header("Звідки вилітає кулька (наскільки далі від гравця)")]
    public float spawnOffset = 0.6f;

    [Header("Мультишот - скільки кульок вилітає за ОДИН клік атаки")]
    [Tooltip("1 = звичайний одиночний постріл, як і раніше")]
    public int projectileCount = 1;
    [Tooltip("Затримка (в секундах) між кульками в черзі - стріляє не всіма кульками одночасно, а одна за одною з цією паузою. За замовчуванням 0 - без бонусного артефакту всі кульки й так по одній (projectileCount = 1), тому пауза ні на що не впливає, поки не з'явиться бонус")]
    public float multishotInterval = 0.08f;
    [Tooltip("Кут (у градусах) між сусідніми кульками в черзі - розкидає їх віялом навколо напрямку атаки, симетрично з обох боків. За замовчуванням 0 - всі кульки летять в одному напрямку. Не діє, якщо projectileCount = 1")]
    public float multishotSpreadAngle = 0f;
    [Tooltip("Бічний зсув (у світових одиницях) між кульками, перпендикулярно до їх напрямку польоту - розставляє кульки поруч, рядом одна з одною (додається ПОВЕРХ кута розкиду, якщо той теж задано). За замовчуванням 0 - всі кульки стартують з однієї точки. Не діє, якщо projectileCount = 1")]
    public float multishotSpacing = 0f;

    [Header("Рандомна точка вильоту")]
    [Tooltip("Якщо > 0 - кожна кулька спавниться у випадковій точці в цьому радіусі (в світових одиницях) навколо звичайної точки вильоту, замість завжди рівно spawnOffset від гравця. 0 = вимкнено, точка вильоту завжди фіксована")]
    public float randomSpawnRadius = 0f;

    [Header("Animator (необов'язково)")]
    public bool triggerAnimation = true;
    public string attackTriggerName = "Attack";
    public float attackAnimDuration = 0.2f; // на скільки блокується рух під час анімації

    [Header("Розблокування атаки (видається туторіальним NPC/артефактом)")]
    public bool canAttack = false; // на старті гравець НЕ може атакувати, поки не отримає пляшечку

    private PlayerMovement movement;
    private PlayerStats stats;
    private Animator animator;
    private StationController stationController; // з'являється сам, коли гравець бере артефакт станцій (ArtifactData.ApplyEffect)
    private float lastAttackTime = -999f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();
        stationController = GetComponent<StationController>(); // може бути null, якщо артефакт ще не взятий
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            return;
        }
        if (!canAttack) return; // атака ще не розблокована (гравець не отримав пляшечку)
        if (Keyboard.current == null) return;

        // Клавіша атаки - J (можна змінити на будь-яку іншу)
        if (Keyboard.current.jKey.wasPressedThisFrame && Time.time >= lastAttackTime + stats.attackCooldown)
        {
            Shoot();
        }
    }

    void Shoot()
    {
        lastAttackTime = Time.time;

        if (triggerAnimation && animator != null)
        {
            animator.SetTrigger(attackTriggerName);
            movement.SetBusy(true);
            Invoke(nameof(EndAttackAnim), attackAnimDuration);
        }

        Vector2 direction = GetAttackDirection();
        Vector2 spawnPos = (Vector2)transform.position + direction * spawnOffset;

        // StationController додається динамічно (ArtifactData.ApplyEffect -> AddComponent),
        // тобто на момент Awake() його могло ще не бути - тому підстраховуємось лінивим пошуком тут.
        if (stationController == null)
            stationController = GetComponent<StationController>();

        if (stationController != null && stationController.isEnabled)
        {
            // Комбо "Орбіта + Станції" - лазерне кільце навколо гравця ЗАМІСТЬ кульки.
            // Радіус кільця = attackRange, шкода за тік = damage - ті самі стати, що й для звичайної кульки.
            // Кулдаун між кільцями окремо не рахуємо - його вже дає перевірка attackCooldown в Update() вище.
            if (stationController.comboOrbitLaserEnabled)
            {
                stationController.SpawnComboOrbitLaser(transform, stats.damage, stats.attackRange);
                return;
            }

            // Звичайний режим станцій - точка ЗАМІСТЬ кульки.
            if (stationController.placeOnAttack)
            {
                stationController.PlaceStationAt(spawnPos);
                return;
            }
        }

        // Мультишот чергою: якщо projectileCount > 1 - стріляємо не всіма кульками миттєво,
        // а чергою, одна за одною, з паузою multishotInterval між пострілами. Кожна кулька ще й
        // може летіти під своїм кутом (multishotSpreadAngle - віяло) і/або спавнитись трохи вбік
        // (multishotSpacing - ряд поруч, перпендикулярно до напрямку саме цієї кульки), а також
        // отримати рандомний зсув точки вильоту (randomSpawnRadius). Всі три ефекти комбінуються.
        int shotsCount = Mathf.Max(1, projectileCount);
        StartCoroutine(FireBurstRoutine(direction, shotsCount));
    }

    // Стріляє shotsCount кульками чергою: перша - миттєво, кожна наступна - через multishotInterval
    // секунд після попередньої. Позицію спавну рахуємо від ПОТОЧНОЇ позиції гравця в момент
    // кожного окремого пострілу (а не один раз на весь залп), бо між пострілами черги встигає
    // пройти трохи часу і гравець міг трохи зсунутись.
    IEnumerator FireBurstRoutine(Vector2 direction, int shotsCount)
    {
        for (int i = 0; i < shotsCount; i++)
        {
            Vector2 shotDirection = GetSpreadDirection(direction, i, shotsCount);
            Vector2 shotSpawnPos = (Vector2)transform.position + shotDirection * spawnOffset;
            shotSpawnPos = GetRowSpawnPos(shotSpawnPos, shotDirection, i, shotsCount);
            shotSpawnPos = GetRandomizedSpawnPos(shotSpawnPos);

            FireSingleBullet(shotSpawnPos, shotDirection);

            if (i < shotsCount - 1 && multishotInterval > 0f)
                yield return new WaitForSeconds(multishotInterval);
        }
    }

    // Розставляє декілька кульок ПОРУЧ, перпендикулярно до напрямку ЦІЄЇ конкретної кульки
    // (shotDirection - вже після кута розкиду, якщо він є), симетрично з обох боків
    // (напр. 3 кульки і spacing = 0.3 дадуть зсуви -0.3, 0, +0.3). При spacing = 0
    // (за замовчуванням) або totalCount = 1 просто повертає basePos без змін.
    Vector2 GetRowSpawnPos(Vector2 basePos, Vector2 shotDirection, int index, int totalCount)
    {
        if (totalCount <= 1 || multishotSpacing == 0f) return basePos;

        Vector2 perpendicular = new Vector2(-shotDirection.y, shotDirection.x).normalized;

        float totalWidth = multishotSpacing * (totalCount - 1);
        float startOffset = -totalWidth / 2f;
        float offset = startOffset + multishotSpacing * index;

        return basePos + perpendicular * offset;
    }

    // Розкидає напрямки декількох кульок віялом навколо baseDirection, симетрично з обох боків
    // (напр. 3 кульки і spreadAngle = 15 дадуть кути -15, 0, +15 відносно напрямку атаки).
    // При spreadAngle = 0 (за замовчуванням) або totalCount = 1 просто повертає baseDirection без змін.
    Vector2 GetSpreadDirection(Vector2 baseDirection, int index, int totalCount)
    {
        if (totalCount <= 1 || multishotSpreadAngle == 0f) return baseDirection;

        float totalSpread = multishotSpreadAngle * (totalCount - 1);
        float startAngle = -totalSpread / 2f;
        float angleOffset = startAngle + multishotSpreadAngle * index;

        float rad = angleOffset * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            baseDirection.x * cos - baseDirection.y * sin,
            baseDirection.x * sin + baseDirection.y * cos
        ).normalized;
    }

    // Спавнить рівно ОДНУ кульку в заданій точці й напрямку з поточними статами гравця.
    // Винесено окремо з Shoot(), щоб однаково використовувалось і для одиночного пострілу,
    // і для кожного пострілу в черзі (FireBurstRoutine).
    void FireSingleBullet(Vector2 spawnPos, Vector2 direction)
    {
        GameObject bulletObj = PhotonNetwork.Instantiate(projectilePrefab.name, spawnPos, Quaternion.identity);
        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Setup(
                direction,
                stats.projectileSpeed,
                stats.damage,
                stats.attackRange,
                stats.projectileBounce,
                stats.projectileBounceCount,
                stats.projectileHoming,
                stats.projectileHomingStrength,
                stats.projectilePierceCount,
                stats.projectilePierceInfinite,
                gameObject,
                stats.projectileZigzag,
                stats.projectileZigzagFrequency,
                stats.projectileZigzagAmplitude,
                stats.projectileChaotic,
                stats.projectileChaosStrength,
                stats.projectileOrbit,
                stats.projectileOrbitRadius,
                stats.projectileOrbitSpeed,
                stats.projectileOrbitDuration,
                stats.projectileSizeMultiplier
            );
        }
    }

    // Якщо randomSpawnRadius > 0 - зсуває точку вильоту у випадкову точку в межах кола цього
    // радіусу навколо basePos. Якщо 0 (за замовчуванням) - точка вильоту лишається фіксованою, як і раніше.
    Vector2 GetRandomizedSpawnPos(Vector2 basePos)
    {
        if (randomSpawnRadius <= 0f) return basePos;

        Vector2 randomOffset = Random.insideUnitCircle * randomSpawnRadius;
        return basePos + randomOffset;
    }

    void EndAttackAnim()
    {
        movement.SetBusy(false);
    }

    // Викликай це, коли гравець отримує пляшечку (артефакт атаки) - наприклад, з NPCDialogueTrigger
    public void UnlockAttack()
    {
        canAttack = true;
    }

    // Напрямок пострілу - залежно від останнього напрямку руху гравця
    Vector2 GetAttackDirection()
    {
        return movement.LastDirection switch
        {
            0 => Vector2.down,
            1 => Vector2.up,
            _ => movement.FacingRight ? Vector2.right : Vector2.left
        };
    }
}