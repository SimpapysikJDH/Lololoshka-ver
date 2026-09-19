using System;
using System.Collections;
using UnityEngine;

// Причепи на GameObject нового боса (той самий, де Rigidbody2D, MonsterHealth, SpriteRenderer, Animator, Collider2D).
// На відміну від BossController.cs (той стоїть на місці) - цей бос ЗАВЖДИ йде за гравцем,
// і зупиняється лише на час підготовки атак 1 і 2. Має 3 атаки:
// 1) Стрибок і падіння зверху - зникає з екрану, тінь-мітка "живою ціллю" слідкує за гравцем,
//    потім бос видимо падає в останню відстежену точку і б'є по площі.
// 2) Віяло кульок - анімація, потім кульки віялом в бік гравця (як в основного боса).
// 3) Заряд швидкості - сидить/заряджається на місці, потім довго йде за гравцем ДУЖЕ швидко,
//    і поступово сповільнюється (кожне влучання зменшує швидкість), а не одразу зупиняється -
//    атака завершується, коли швидкість опуститься до звичайної.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class ChasingBossController : MonoBehaviour, IDamageable
{
    [Header("Дані боса (MonsterData - maxHP, недоторканність від удару, дроп монет)")]
    public MonsterData data;

    [Header("Гравець (шукається автоматично за тегом Player, якщо не признач вручну)")]
    public Transform player;

    [Header("=== Звичайний рух (переслідування гравця) ===")]
    [Tooltip("Швидкість, з якою бос завжди йде за гравцем, поки не готує атаку")]
    public float moveSpeed = 2.5f;

    [Header("=== Атака 1: Стрибок і падіння зверху ===")]
    [Tooltip("Префаб тіні/мітки (спрайт), що показує де впаде бос - признач сюди свій спрайт")]
    public GameObject shadowIndicatorPrefab;
    [Tooltip("Скільки триває анімація підготовки, перш ніж бос зникне з екрану")]
    public float jumpWindupDuration = 0.5f;
    [Tooltip("Скільки бос невидимий у повітрі - весь цей час мітка ЖИВЦЕМ слідкує за гравцем")]
    public float aerialTrackDuration = 1.2f;
    [Tooltip("Скільки триває видима фаза падіння (з анімацією польоту) в останню відстежену точку")]
    public float fallDuration = 0.35f;

    [Header("=== Атака 1: звуження мітки-тіні ===")]
    [Tooltip("Якщо увімкнено - мітка поступово звужується весь час (і поки бос у повітрі, і поки видимо падає). Якщо вимкнено - розмір мітки не змінюється (лишається таким, як у префабі).")]
    public bool shadowScalingEnabled = true;
    [Tooltip("Масштаб мітки одразу після появи (початок фази 'в повітрі')")]
    public float shadowStartScale = 1.5f;
    [Tooltip("Масштаб мітки в момент, коли бос закінчує бути 'в повітрі' і починає видиме падіння")]
    public float shadowMidScale = 1f;
    [Tooltip("Масштаб мітки в момент удару об землю (кінець видимого падіння)")]
    public float shadowEndScale = 0.6f;

    [Tooltip("Радіус шкоди в точці приземлення")]
    public float landingRadius = 1.5f;
    public float landingDamage = 2f;
    [Tooltip("З ким взаємодіє удар при приземленні (постав шар гравця)")]
    public LayerMask landingHitMask = ~0;
    [Tooltip("Затримка (сек) між моментом, коли бос вже видимо стоїть на землі (тригер jumpLandTrigger), і моментом, коли фактично рахується шкода/відштовхування. Дає гравцю останній момент, щоб встигнути втекти з зони удару.")]
    public float landingImpactDelay = 0.12f;
    [Tooltip("Радіус, у якому гравця ВІДШТОВХУЄ від точки удару (зроби трохи більшим за landingRadius, щоб тих, хто вибіг із зони шкоди, але лишився близько, все одно відкидало)")]
    public float landingPushRadius = 2.2f;
    [Tooltip("Сила відштовхування (імпульс) від центру удару")]
    public float landingPushForce = 10f;
    public string jumpStartTrigger = "JumpStart";   // анімація підготовки (ще на землі)
    public string jumpFallTrigger = "JumpFall";     // анімація видимого польоту вниз
    public string jumpLandTrigger = "JumpLand";     // анімація/поштовх в момент удару об землю (опційно)


    [Header("=== Атака 2: Віяло кульок ===")]
    [Tooltip("Префаб снаряду (Projectile.cs)")]
    public GameObject projectilePrefab;
    public float fanWindupDuration = 0.4f;
    public int fanBulletCount = 8;
    public float fanSpreadAngle = 100f;
    public float fanBulletSpeed = 4f;
    public float fanBulletDamage = 1f;
    public float fanBulletRange = 12f;
    [Tooltip("Розмір (масштаб) кульки - 1 = оригінальний розмір префабу")]
    public float fanBulletSize = 1f;
    public float bulletSpawnOffset = 0.6f;
    public string fanAttackTrigger = "FanAttack";

    [Header("=== Атака 3: Заряд швидкості ===")]
    [Tooltip("Скільки секунд бос сидить і заряджається на місці, перш ніж прискоритись")]
    public float chargeUpDuration = 1f;
    [Tooltip("У скільки разів швидший бос стає під час заряду (від moveSpeed)")]
    public float fastMoveSpeedMultiplier = 3f;
    [Tooltip("Максимальна тривалість швидкої фази, якщо гравець жодного разу не влучить")]
    public float fastChaseMaxDuration = 5f;
    [Tooltip("У скільки разів сповільнюється бос за КОЖНЕ влучання під час швидкої фази (напр. 0.75 = -25% швидкості за влучання). Швидкість падає поступово, а не миттєво до звичайної - атака завершується (canMove-логіка звичайна), коли після кількох влучань швидкість опуститься до базової (currentSpeedMultiplier = 1)")]
    public float fastChaseSlowdownPerHit = 0.75f;
    public string chargeStartTrigger = "ChargeStart"; // анімація заряду (сидить/заряджається)
    public string chargeBoolFastMoving = "IsFastMoving"; // true поки біжить швидко

    [Header("=== Контактна шкода (дотик до тіла боса) ===")]
    [Tooltip("Шкода, яку отримує гравець просто від дотику до тіла боса (не від атаки, а від самого контакту)")]
    public float contactDamage = 1f;
    [Tooltip("Мінімальний інтервал (сек) між тіками контактної шкоди, поки гравець торкається боса")]
    public float contactDamageInterval = 0.5f;

    [Header("=== Цикл атак ===")]
    public float startDelay = 1f;              // пауза перед першою атакою після старту сцени
    public float minPauseBetweenAttacks = 1f;  // мін. пауза між атаками (бос в цей час просто йде за гравцем)
    public float maxPauseBetweenAttacks = 2.5f;

    [Header("=== Анімації / компоненти ===")]
    [Tooltip("Якщо не признач - спробує знайти на цьому ж GameObject")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    [Tooltip("Колайдер боса (той, що ловить влучання кульок) - ховається на час стрибка, щоб кульки летіли крізь невидимого боса")]
    public Collider2D bossCollider;
    public string speedFloatName = "Speed";       // 0/1 - як в MonsterController, керує Idle/Walk
    public string directionIntName = "Direction"; // 0=Down,1=Up,2=Side - той самий принцип, що і в MonsterController
    public string deathTriggerName = "Death";
    public float deathAnimDuration = 1f;

    // Підписуйся в UI, щоб показувати назву поточної атаки
    public event Action<string> OnAttackStarted;

    private MonsterHealth health;
    private Rigidbody2D rb;
    private Coroutine loopRoutine;

    private bool isInvincible = false;      // недоторканність від удару (як в MonsterController12)
    private bool canMove = true;            // false = бос зупинений (готує атаку 1 або 2)
    private float currentSpeedMultiplier = 1f;

    private bool isFastChasing = false;         // true під час швидкої фази атаки 3
    private bool tookDamageDuringFastChase = false;

    private GameObject activeShadowInstance; // поточна тінь-мітка атаки 1 (щоб прибрати при смерті боса)

    private float lastContactDamageTime = -999f; // коли востаннє нанесли контактну шкоду (дотиком)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<MonsterHealth>();

        if (animator == null)
            animator = GetComponent<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (bossCollider == null)
            bossCollider = GetComponent<Collider2D>();
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

        // Смерть контролюємо самі - спершу анімація, потім знищення об'єкта
        health.destroyOnDeath = false;
        health.OnDeath += HandleDeath;

        loopRoutine = StartCoroutine(BossLoopRoutine());
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    void Update()
    {
        if (health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimFloat(speedFloatName, 0f);
            return;
        }

        // Під час підготовки атак 1 і 2 (canMove = false) бос стоїть на місці.
        // Під час звичайного переслідування І під час швидкої фази атаки 3 (isFastChasing) -
        // рух однаковий, лише currentSpeedMultiplier більший.
        if (!canMove)
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimFloat(speedFloatName, 0f);
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        Vector2 direction = toPlayer.normalized;

        rb.linearVelocity = direction * moveSpeed * currentSpeedMultiplier;
        UpdateFacing(toPlayer);
        SetAnimFloat(speedFloatName, 1f);
    }

    // Той самий принцип, що і в MonsterController: Direction (0=Down,1=Up,2=Side) + flipX
    void UpdateFacing(Vector2 toPlayer)
    {
        if (Mathf.Abs(toPlayer.y) > Mathf.Abs(toPlayer.x))
        {
            SetAnimFloat(directionIntName, toPlayer.y > 0f ? 1 : 0);
        }
        else
        {
            SetAnimFloat(directionIntName, 2);
            if (spriteRenderer != null)
                spriteRenderer.flipX = toPlayer.x > 0f;
        }
    }

    // ---------- Контактна шкода (дотик до тіла боса) ----------
    // Працює і для звичайного (не-триггер) колайдера, і для триггера -
    // залежно від того, як в проєкті налаштовані колайдери боса/гравця.

    void OnCollisionEnter2D(Collision2D collision) => TryDealContactDamage(collision.collider);
    void OnCollisionStay2D(Collision2D collision) => TryDealContactDamage(collision.collider);
    void OnTriggerEnter2D(Collider2D other) => TryDealContactDamage(other);
    void OnTriggerStay2D(Collider2D other) => TryDealContactDamage(other);

    void TryDealContactDamage(Collider2D other)
    {
        if (health.IsDead) return;
        if (other == null) return;

        // Не б'ємо самого себе (свої ж колайдери/дочірні об'єкти)
        if (other.transform == transform || other.transform.IsChildOf(transform))
            return;

        if (Time.time - lastContactDamageTime < contactDamageInterval)
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            return;

        lastContactDamageTime = Time.time;
        damageable.TakeDamage(contactDamage);
    }

    // ---------- IDamageable ----------

    public void TakeDamage(float amount)
    {
        if (isInvincible) return;

        health.TakeDamage(amount);

        // Атака 3: кожне влучання поступово сповільнює боса (а не одразу скидає швидкість)
        if (isFastChasing)
            HandleFastChaseHit();

        StartCoroutine(HitInvincibilityRoutine());
    }

    // Поступове сповільнення під час швидкої фази атаки 3: кожен виступ зменшує
    // currentSpeedMultiplier за формулою fastChaseSlowdownPerHit. Коли швидкість
    // опускається до звичайної (1x) або нижче - фаза завершується як зазвичай.
    void HandleFastChaseHit()
    {
        currentSpeedMultiplier *= fastChaseSlowdownPerHit;

        if (currentSpeedMultiplier <= 1f)
        {
            currentSpeedMultiplier = 1f;
            tookDamageDuringFastChase = true; // швидкість вже впала до звичайної - завершуємо атаку
        }
    }

    private IEnumerator HitInvincibilityRoutine()
    {
        isInvincible = true;
        float duration = data != null ? data.hitInvincibilityDuration : 0.2f;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    // ---------- Основний цикл атак ----------

    IEnumerator BossLoopRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (!health.IsDead)
        {
            float pause = UnityEngine.Random.Range(minPauseBetweenAttacks, maxPauseBetweenAttacks);
            yield return new WaitForSeconds(pause);

            if (health.IsDead) yield break;

            yield return StartCoroutine(PerformRandomAttack());
        }
    }

    IEnumerator PerformRandomAttack()
    {
        int choice = UnityEngine.Random.Range(0, 3); // 0 = стрибок, 1 = віяло, 2 = заряд швидкості

        switch (choice)
        {
            case 0:
                OnAttackStarted?.Invoke("Стрибок");
                yield return StartCoroutine(JumpAttackRoutine());
                break;
            case 1:
                OnAttackStarted?.Invoke("Віяло кульок");
                yield return StartCoroutine(FanAttackRoutine());
                break;
            case 2:
                OnAttackStarted?.Invoke("Заряд швидкості");
                yield return StartCoroutine(ChargeSpeedAttackRoutine());
                break;
        }
    }

    void HandleDeath()
    {
        StopAllCoroutines();

        // Якщо бос помер саме під час стрибка (невидимий/без колайдера) - повертаємо все як є,
        // інакше анімація смерті програється на невидимому об'єкті
        if (activeShadowInstance != null)
        {
            Destroy(activeShadowInstance);
            activeShadowInstance = null;
        }
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
        if (bossCollider != null)
            bossCollider.enabled = true;

        rb.linearVelocity = Vector2.zero;
        canMove = false;

        SetAnimTrigger(deathTriggerName);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimDuration);
        Destroy(gameObject);
    }

    // ---------- Атака 1: Стрибок і падіння зверху ----------

    IEnumerator JumpAttackRoutine()
    {
        canMove = false;
        rb.linearVelocity = Vector2.zero;

        // Фаза підготовки - бос ще видимий і стоїть на місці, грає анімацію стрибка
        SetAnimTrigger(jumpStartTrigger);
        yield return new WaitForSeconds(jumpWindupDuration);

        // Бос "підстрибує дуже високо і пропадає з екрану" - ховаємо спрайт і колайдер,
        // щоб кульки й гравець фізично його не бачили й не чіпали, поки він у повітрі
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (bossCollider != null) bossCollider.enabled = false;

        Vector3 trackedPosition = player != null ? player.position : transform.position;

        if (shadowIndicatorPrefab != null)
        {
            activeShadowInstance = Instantiate(shadowIndicatorPrefab, trackedPosition, Quaternion.identity);
            if (shadowScalingEnabled)
                activeShadowInstance.transform.localScale = Vector3.one * shadowStartScale;
        }

        // Поки бос невидимий у повітрі - мітка ЖИВЦЕМ слідкує за гравцем (не фіксується заздалегідь)
        float trackTimer = 0f;
        while (trackTimer < aerialTrackDuration)
        {
            trackTimer += Time.deltaTime;

            if (player != null)
                trackedPosition = player.position;

            if (activeShadowInstance != null)
            {
                activeShadowInstance.transform.position = trackedPosition;

                if (shadowScalingEnabled)
                {
                    float t = aerialTrackDuration > 0f ? Mathf.Clamp01(trackTimer / aerialTrackDuration) : 1f;
                    activeShadowInstance.transform.localScale = Vector3.one * Mathf.Lerp(shadowStartScale, shadowMidScale, t);
                }
            }

            yield return null;
        }

        // Тепер ціль зафіксована в останній відстеженій точці - бос видимо летить туди й падає
        transform.position = trackedPosition;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        SetAnimTrigger(jumpFallTrigger);

        float fallTimer = 0f;
        while (fallTimer < fallDuration)
        {
            fallTimer += Time.deltaTime;

            if (shadowScalingEnabled && activeShadowInstance != null)
            {
                float t = fallDuration > 0f ? Mathf.Clamp01(fallTimer / fallDuration) : 1f;
                activeShadowInstance.transform.localScale = Vector3.one * Mathf.Lerp(shadowMidScale, shadowEndScale, t);
            }

            yield return null;
        }

        if (activeShadowInstance != null)
        {
            Destroy(activeShadowInstance);
            activeShadowInstance = null;
        }

        if (bossCollider != null) bossCollider.enabled = true;

        SetAnimTrigger(jumpLandTrigger);

        // Невелика затримка між появою боса на землі і фактичним ударом -
        // це "вікно втечі": якщо гравець встигне вибігти з радіуса за цей час, удар його не зачепить.
        if (landingImpactDelay > 0f)
            yield return new WaitForSeconds(landingImpactDelay);

        DealLandingDamage(trackedPosition);

        canMove = true;
    }

    void DealLandingDamage(Vector3 center)
    {
        // Шкода - лише в межах landingRadius (перевіряється ПІСЛЯ landingImpactDelay,
        // тобто гравець, що встиг вибігти з цього кола, шкоди не отримає)
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, landingRadius, landingHitMask);

        foreach (Collider2D hit in hits)
        {
            // Не б'ємо самого себе
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(landingDamage);
        }

        // Відштовхування - у трохи більшому радіусі landingPushRadius.
        // Це стосується й тих, хто вибіг із зони шкоди, але лишився близько до точки удару -
        // їх не поранить, але хвилею вибуху відкине геть.
        Collider2D[] pushHits = Physics2D.OverlapCircleAll(center, landingPushRadius, landingHitMask);

        foreach (Collider2D hit in pushHits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            Rigidbody2D targetRb = hit.GetComponent<Rigidbody2D>();
            if (targetRb == null)
                continue;

            Vector2 pushDir = ((Vector2)hit.transform.position - (Vector2)center);
            pushDir = pushDir.sqrMagnitude > 0.0001f ? pushDir.normalized : UnityEngine.Random.insideUnitCircle.normalized;

            targetRb.AddForce(pushDir * landingPushForce, ForceMode2D.Impulse);
        }
    }

    // ---------- Атака 2: Віяло кульок ----------

    IEnumerator FanAttackRoutine()
    {
        canMove = false;
        rb.linearVelocity = Vector2.zero;

        SetAnimTrigger(fanAttackTrigger);
        yield return new WaitForSeconds(fanWindupDuration);

        if (projectilePrefab != null && player != null)
        {
            Vector2 centerDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

            for (int i = 0; i < fanBulletCount; i++)
            {
                Vector2 dir = GetFanDirection(centerDir, i, fanBulletCount);
                SpawnBullet(dir, fanBulletSpeed, fanBulletDamage, fanBulletRange, fanBulletSize);
            }
        }

        canMove = true;
    }

    // Розкидає fanBulletCount напрямків симетрично навколо centerDir на fanSpreadAngle градусів сумарно
    Vector2 GetFanDirection(Vector2 centerDir, int index, int total)
    {
        if (total <= 1) return centerDir;

        float clampedSpread = Mathf.Clamp(fanSpreadAngle, 0f, 360f);
        float step = clampedSpread / (total - 1);
        float startAngle = -clampedSpread / 2f;
        float angleOffset = startAngle + step * index;

        return RotateVector(centerDir, angleOffset);
    }

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

    Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // ---------- Атака 3: Заряд швидкості ----------

    IEnumerator ChargeSpeedAttackRoutine()
    {
        // Фаза заряду - бос сидить на місці і "заряджається"
        canMove = false;
        rb.linearVelocity = Vector2.zero;
        SetAnimTrigger(chargeStartTrigger);
        yield return new WaitForSeconds(chargeUpDuration);

        // Швидка фаза - бос дуже швидко йде за гравцем (звичайна логіка руху з Update(),
        // просто з більшим множником швидкості), поки не скінчиться час або поки не отримає урон
        isFastChasing = true;
        tookDamageDuringFastChase = false;
        currentSpeedMultiplier = fastMoveSpeedMultiplier;
        canMove = true;
        SetAnimBool(chargeBoolFastMoving, true);

        float timer = 0f;
        while (timer < fastChaseMaxDuration && !tookDamageDuringFastChase)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // Урон отримано (або вийшов час) - одразу повертаємось до звичайної швидкості, атака завершена
        currentSpeedMultiplier = 1f;
        isFastChasing = false;
        SetAnimBool(chargeBoolFastMoving, false);
    }

    // ---------- Хелпери для Animator ----------

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

    void SetAnimFloat(string floatName, float value)
    {
        if (animator != null && !string.IsNullOrEmpty(floatName))
            animator.SetFloat(floatName, value);
    }

    void SetAnimInt(string intName, int value)
    {
        if (animator != null && !string.IsNullOrEmpty(intName))
            animator.SetInteger(intName, value);
    }

    void OnDrawGizmosSelected()
    {
        // Радіус шкоди приземлення (навколо самого боса, для орієнтиру в редакторі)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, landingRadius);

        // Радіус відштовхування - трохи більший за радіус шкоди
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, landingPushRadius);

        // Конус віяла кульок в бік гравця (або вниз, якщо гравця не признач.)
        Gizmos.color = Color.yellow;
        Vector2 centerDir = player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : Vector2.down;

        int previewCount = Mathf.Max(fanBulletCount, 2);
        for (int i = 0; i < previewCount; i++)
        {
            Vector2 dir = GetFanDirection(centerDir, i, previewCount);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)(dir * fanBulletRange));
        }
    }
}