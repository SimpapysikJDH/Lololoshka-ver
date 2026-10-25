using UnityEngine;

// Монстр, який САМ по собі їздить вгору-вниз, не залежно від гравця.
// Гравця не шукає і не переслідує. Шкоду завдає лише при дотику.
// Причепи на GameObject монстра. Потрібні компоненти: Rigidbody2D, MonsterHealth, SpriteRenderer, Animator
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class VerticalMonsterController : MonoBehaviour, IDamageable, ISlowable, IKnockbackable, IFreezable
{
    [Header("Налаштування цього монстра")]
    public MonsterData data;

    [Header("Вертикальний рух")]
    [Tooltip("Починати рух вгору (true) чи вниз (false)")]
    public bool startMovingUp = false;

    [Tooltip("На скільки одиниць монстр відходить від стартової точки вгору і вниз. 0 = без обмеження, розвертається лише об стіни")]
    public float patrolDistance = 3f;

    [Tooltip("Скільки секунд стоїть після розвороту. 0 = не зупиняється")]
    public float endPauseDuration = 0f;

    [Header("Шкода при дотику")]
    public bool damageOnContact = true;

    private Rigidbody2D rb;
    private MonsterHealth health;
    private Animator animator;

    private float lastAttackTime = -999f;
    private bool isInvincible = false;

    private float speedMultiplier = 1f;
    private Coroutine slowRoutine;

    private bool isStaggered = false;

    private bool isFrozen = false;
    private Coroutine freezeRoutine;

    private float patrolDirection = -1f; // +1 = вгору, -1 = вниз
    private float startY;
    private float pauseTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<MonsterHealth>();
        animator = GetComponent<Animator>();

        // Рух по X заборонений - монстр їздить лише вертикально
        rb.constraints |= RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }

    void Start()
    {
        health.Initialize(data);

        startY = transform.position.y;
        patrolDirection = startMovingUp ? 1f : -1f;
        SetFacing(patrolDirection);
    }

    void Update()
    {
        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0f);
            return;
        }

        if (isStaggered)
        {
            animator.SetFloat("Speed", 1f);
            return;
        }

        // Пауза на краю маршруту
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0f);
            return;
        }

        // Дійшли до краю маршруту - розвертаємось
        if (patrolDistance > 0f)
        {
            float y = transform.position.y;
            if (patrolDirection > 0f && y >= startY + patrolDistance)
                Turn();
            else if (patrolDirection < 0f && y <= startY - patrolDistance)
                Turn();

            if (pauseTimer > 0f) return;
        }

        rb.linearVelocity = new Vector2(0f, patrolDirection * data.moveSpeed * speedMultiplier);
        animator.SetFloat("Speed", 1f);
    }

    void Turn()
    {
        patrolDirection = -patrolDirection;
        SetFacing(patrolDirection);
        pauseTimer = endPauseDuration;
        rb.linearVelocity = Vector2.zero;
    }

    // Direction: 0 = Down, 1 = Up
    void SetFacing(float verticalSign)
    {
        animator.SetInteger("Direction", verticalSign > 0f ? 1 : 0);
    }

    // ---------- Зіткнення ----------
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Вперлись у стіну/перешкоду по вертикалі - розвертаємось
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y * patrolDirection < -0.5f)
            {
                Turn();
                break;
            }
        }

        TryDamage(collision.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    void TryDamage(GameObject other)
    {
        if (!damageOnContact || isFrozen) return;
        if (!other.CompareTag("Player")) return;
        if (Time.time < lastAttackTime + data.attackCooldown) return;

        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph == null) ph = other.GetComponentInParent<PlayerHealth>();
        if (ph == null) return;

        lastAttackTime = Time.time;
        ph.TakeDamage(data.damage);
    }

    // ---------- IDamageable ----------
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
    // Поштовх лише по вертикалі (X заморожений)
    public void Knockback(Vector2 direction, float force, float staggerDuration)
    {
        float dirY = Mathf.Abs(direction.y) < 0.01f ? 0f : Mathf.Sign(direction.y);
        StartCoroutine(KnockbackRoutine(dirY, force, staggerDuration));
    }

    private System.Collections.IEnumerator KnockbackRoutine(float dirY, float force, float staggerDuration)
    {
        isStaggered = true;
        rb.linearVelocity = new Vector2(0f, dirY * force);
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