using UnityEngine;

// Причепи на GameObject монстра-стрільця (той самий, де MonsterHealth і Animator)
// Потрібні компоненти: Rigidbody2D, SpriteRenderer, MonsterHealth
// Логіка: блукає, поки гравець не в detectionRange -> підбігає/відбігає, щоб триматись
// на preferredDistance -> зупиняється і стріляє з кулдауном data.attackCooldown
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(MonsterHealth))]
public class RangedMonsterController : MonoBehaviour, ISlowable, IKnockbackable, IFreezable
{
    [Header("Дані монстра (ScriptableObject з MonsterData.cs)")]
    public MonsterData data;

    [Header("Снаряд (префаб з Projectile.cs)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 4f;
    public float spawnOffset = 0.5f;

    [Header("Дистанція стрільби")]
    public float preferredDistance = 3f;  // монстр намагається зупинитись і стріляти на цій відстані
    public float retreatDistance = 1.5f;  // якщо гравець підійшов ближче - монстр відступає назад

    [Header("Анімація (Animator: параметри Speed(float), Direction(int 0=Down,1=Up,2=Side), тригер атаки)")]
    public Animator animator;
    public string attackTriggerName = "Attack";
    public float attackAnimDuration = 0.3f; // на цей час монстр "застигає" під час пострілу

    [Header("Вилітання кульки")]
    // Якщо true - кулька вилітає одразу коли починається анімація атаки.
    // Якщо false - треба додати Animation Event на потрібному кадрі, що викликає SpawnProjectileFromAnimEvent()
    public bool spawnProjectileImmediately = true;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private MonsterHealth health;
    private Transform player;
    private float lastAttackTime = -999f;
    private bool isBusy = false;

    // Блукання (та сама логіка, що описана в MonsterData: рух -> пауза -> рух)
    private Vector2 wanderDirection;
    private float wanderTimer;
    private bool isWandering;

    // Куди зараз "дивиться" монстр (для Animator), НЕ залежить від швидкості -
    // інакше під час пострілу (velocity = 0) напрямок анімації застрягав на старому
    private int facingDirection = 0; // 0 = Down, 1 = Up, 2 = Side

    private enum State { Wander, Chase, Attack }
    private State state = State.Wander;

    // Множник швидкості від ISlowable.Slow() - застосовується до moveSpeed/wanderSpeed
    private float speedMultiplier = 1f;
    private Coroutine slowRoutine;

    // Поки true - монстра рухає лише поштовх (IKnockbackable), звичайна логіка на паузі
    private bool isStaggered = false;

    // Поки true - монстр повністю заморожений (IFreezable): рух і атака вимкнені
    private bool isFrozen = false;
    private Coroutine freezeRoutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        health = GetComponent<MonsterHealth>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (data != null)
            health.Initialize(data);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        StartWanderPause();
    }

    void Update()
    {
        if (data == null || health.IsDead || player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateAnimator();
            return;
        }

        if (isStaggered)
        {
            UpdateAnimator();
            return;
        }

        if (isBusy)
        {
            UpdateAnimator();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= data.detectionRange)
        {
            state = (distance > preferredDistance || distance < retreatDistance)
                ? State.Chase
                : State.Attack;
        }
        else
        {
            state = State.Wander;
        }

        switch (state)
        {
            case State.Wander:
                UpdateWander();
                break;
            case State.Chase:
                UpdateChase(distance);
                break;
            case State.Attack:
                UpdateAttack();
                break;
        }

        UpdateAnimator();
    }

    void UpdateChase(float distance)
    {
        Vector2 dirToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // Якщо гравець надто близько - відступаємо у протилежний бік, інакше йдемо до нього
        Vector2 moveDir = (distance < retreatDistance) ? -dirToPlayer : dirToPlayer;
        rb.linearVelocity = moveDir * data.moveSpeed * speedMultiplier;

        FaceDirection(moveDir);
    }

    void UpdateAttack()
    {
        rb.linearVelocity = Vector2.zero;

        Vector2 dirToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        FaceDirection(dirToPlayer);

        if (Time.time >= lastAttackTime + data.attackCooldown)
        {
            Shoot();
        }
    }

    void UpdateWander()
    {
        wanderTimer -= Time.deltaTime;

        if (isWandering)
        {
            rb.linearVelocity = wanderDirection * data.wanderSpeed * speedMultiplier;
            FaceDirection(wanderDirection);

            if (wanderTimer <= 0f)
            {
                isWandering = false;
                wanderTimer = data.wanderPauseDuration;
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;

            if (wanderTimer <= 0f)
                StartWanderMove();
        }
    }

    void StartWanderMove()
    {
        isWandering = true;
        wanderTimer = data.wanderMoveDuration;
        wanderDirection = Random.insideUnitCircle.normalized;
    }

    void StartWanderPause()
    {
        isWandering = false;
        wanderTimer = data.wanderPauseDuration;
    }

    void Shoot()
    {
        lastAttackTime = Time.time;

        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.SetTrigger(attackTriggerName);
            isBusy = true;
            rb.linearVelocity = Vector2.zero;
            Invoke(nameof(EndAttackAnim), attackAnimDuration);

            if (spawnProjectileImmediately)
                SpawnProjectileTowardsPlayer();
            // якщо spawnProjectileImmediately == false - виклич SpawnProjectileFromAnimEvent()
            // через Animation Event на потрібному кадрі анімації атаки
        }
        else
        {
            SpawnProjectileTowardsPlayer();
        }
    }

    // Виклич це з Animation Event, якщо хочеш щоб кулька вилітала в конкретний кадр анімації
    // (постав спершу spawnProjectileImmediately = false)
    public void SpawnProjectileFromAnimEvent()
    {
        SpawnProjectileTowardsPlayer();
    }

    void SpawnProjectileTowardsPlayer()
    {
        if (projectilePrefab == null || player == null) return;

        Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
        Vector2 spawnPos = (Vector2)transform.position + direction * spawnOffset;

        GameObject bulletObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        Projectile projectile = bulletObj.GetComponent<Projectile>();

        if (projectile != null)
        {
            projectile.Setup(
                direction: direction,
                speed: projectileSpeed,
                dmg: data.damage,
                range: data.attackRange,
                shooter: gameObject   // тепер справді потрапляє в owner
            );
        }
    }

    void EndAttackAnim()
    {
        isBusy = false;
    }

    void FaceDirection(Vector2 dir)
    {
        if (dir.magnitude < 0.05f) return;

        if (Mathf.Abs(dir.x) > 0.01f)
            spriteRenderer.flipX = dir.x > 0f;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", rb.linearVelocity.magnitude);

        Vector2 vel = rb.linearVelocity;
        if (vel.magnitude > 0.05f)
        {
            // Direction - Float, а не Int, бо Blend Tree приймає для осі блендингу лише Float
            float direction = Mathf.Abs(vel.y) > Mathf.Abs(vel.x)
                ? (vel.y > 0f ? 1f : 0f)
                : 2f;
            animator.SetFloat("Direction", direction);
        }
    }

    // ---------- ISlowable ----------
    public void Slow(float slowMultiplier, float duration)
    {
        if (slowRoutine != null)
            StopCoroutine(slowRoutine);

        slowRoutine = StartCoroutine(SlowRoutine(slowMultiplier, duration));
    }

    private System.Collections.IEnumerator SlowRoutine(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Clamp01(multiplier);
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        slowRoutine = null;
    }

    // ---------- IKnockbackable ----------
    public void Knockback(Vector2 direction, float force, float staggerDuration)
    {
        StartCoroutine(KnockbackRoutine(direction, force, staggerDuration));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector2 direction, float force, float staggerDuration)
    {
        isStaggered = true;
        rb.linearVelocity = direction.normalized * force;
        yield return new WaitForSeconds(staggerDuration);
        isStaggered = false;
    }

    // ---------- IFreezable ----------
    public void Freeze(float duration)
    {
        if (freezeRoutine != null)
            StopCoroutine(freezeRoutine);

        freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private System.Collections.IEnumerator FreezeRoutine(float duration)
    {
        isFrozen = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(duration);
        isFrozen = false;
        freezeRoutine = null;
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}