using UnityEngine;

// Причепи на GameObject монстра
// Потрібні компоненти: Rigidbody2D, MonsterHealth, SpriteRenderer, Animator
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class MonsterController : MonoBehaviour, IDamageable, ISlowable, IKnockbackable, IFreezable
{
    [Header("Налаштування цього монстра")]
    public MonsterData data;

    private Rigidbody2D rb;
    private MonsterHealth health;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform player;
    private PlayerHealth playerHealth;

    private float lastAttackTime = -999f;
    private bool isInvincible = false;

    // Множник швидкості від ISlowable.Slow() - 1 = звичайна швидкість, 0.5 = вдвічі повільніше і т.д.
    private float speedMultiplier = 1f;
    private Coroutine slowRoutine;

    // Поки true - монстра штовхає лише поштовх (IKnockbackable), звичайна логіка руху вимкнена
    private bool isStaggered = false;

    // Поки true - монстр повністю заморожений (IFreezable): рух і атака вимкнені
    private bool isFrozen = false;
    private Coroutine freezeRoutine;

    // Блукання, коли гравець не виявлений
    private static readonly Vector2[] wanderDirections =
    {
        Vector2.left,
        Vector2.right,
        Vector2.down
    };

    private Vector2 wanderDirection = Vector2.zero;
    private float wanderTimer = 0f;
    private bool isWanderingMoving = false;

    // Як тільки гравець потрапив у detectionRange хоч раз - монстр більше не "забуває" про нього
    private bool hasSeenPlayer = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<MonsterHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        health.Initialize(data);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        // Щоб різні монстри не рухались синхронно, стартовий стан і таймер трохи випадкові
        isWanderingMoving = false;
        wanderTimer = Random.Range(0f, data.wanderPauseDuration);
    }

    void Update()
    {
        if (player == null) return;

        // Поки триває заморозка (Freeze) - монстр повністю стоїть, звичайна логіка не втручається
        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0f);
            return;
        }

        // Поки триває поштовх (Knockback) - монстра рухає лише він, звичайна логіка не втручається
        if (isStaggered)
        {
            animator.SetFloat("Speed", 1f);
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        Vector2 toPlayer = player.position - transform.position;

        bool isMoving = false;

        // Щойно гравець хоч раз потрапив у радіус виявлення - ставимо прапорець назавжди (поки монстр живий)
        if (!hasSeenPlayer && distance <= data.detectionRange)
            hasSeenPlayer = true;

        if (distance <= data.attackRange)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateFacing(toPlayer);

            if (Time.time >= lastAttackTime + data.attackCooldown)
                Attack();
        }
        else if (hasSeenPlayer || distance <= data.detectionRange)
        {
            Vector2 direction = toPlayer.normalized;
            rb.linearVelocity = direction * data.moveSpeed * speedMultiplier;
            UpdateFacing(toPlayer);
            isMoving = true;
        }
        else
        {
            // Гравця ще жодного разу не було видно - монстр трохи блукає замість того, щоб просто стояти
            isMoving = UpdateWander();
        }

        // Той самий принцип, що і в PlayerMovement: Speed керує Idle/Walk
        animator.SetFloat("Speed", isMoving ? 1f : 0f);
    }

    // Проста логіка блукання: рух у випадковому напрямку (ліво/право/вниз), потім пауза, і по колу
    bool UpdateWander()
    {
        wanderTimer -= Time.deltaTime;

        if (wanderTimer <= 0f)
        {
            isWanderingMoving = !isWanderingMoving;

            if (isWanderingMoving)
            {
                wanderDirection = wanderDirections[Random.Range(0, wanderDirections.Length)];
                wanderTimer = data.wanderMoveDuration;
            }
            else
            {
                wanderTimer = data.wanderPauseDuration;
            }
        }

        if (isWanderingMoving)
        {
            rb.linearVelocity = wanderDirection * data.wanderSpeed * speedMultiplier;
            UpdateFacing(wanderDirection);
            return true;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            return false;
        }
    }

    // Визначає Direction (0=Down, 1=Up, 2=Side) і flipX так само, як PlayerMovement
    void UpdateFacing(Vector2 toPlayer)
    {
        if (Mathf.Abs(toPlayer.y) > Mathf.Abs(toPlayer.x))
        {
            animator.SetInteger("Direction", toPlayer.y > 0f ? 1 : 0);
        }
        else
        {
            animator.SetInteger("Direction", 2);
            spriteRenderer.flipX = toPlayer.x > 0f;
        }
    }

    void Attack()
    {
        lastAttackTime = Time.time;

        if (playerHealth != null)
            playerHealth.TakeDamage(data.damage);
    }

    public void TakeDamage(float amount)
    {
        if (isInvincible) return;

        health.TakeDamage(amount);
        StartCoroutine(HitInvincibilityRoutine());
    }

    private System.Collections.IEnumerator HitInvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(data.hitInvincibilityDuration);
        isInvincible = false;
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
}