using UnityEngine;

// Причепи на GameObject слайма
// Потрібні компоненти: Rigidbody2D, MonsterHealth, SpriteRenderer, Animator
//
// Аніматор має розуміти параметри:
//   Speed (float)     - 0 = idle, 1 = рух (так само як у PlayerMovement/MonsterController)
//   Direction (int)   - 0=Down, 1=Up, 2=Side (так само як у MonsterController)
//   Charging (bool)   - true поки слайм "трясеться" перед ривком (телеграф атаки)
//   Dashing (bool)    - true під час самого ривка
//   Die (trigger)     - запускається один раз в момент смерті
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class SlimeController : MonoBehaviour, IDamageable, ISlowable, IKnockbackable, IFreezable
{
    [Header("Налаштування цього слайма")]
    public SlimeData data;

    private Rigidbody2D rb;
    private MonsterHealth health;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform player;
    private PlayerHealth playerHealth;

    private bool isInvincible = false;
    private float lastContactDamageTime = -999f;

    private Collider2D col;

    // Множник швидкості від ISlowable.Slow() - застосовується до dashSpeed слайма
    private float speedMultiplier = 1f;
    private Coroutine slowRoutine;

    // Поки true - слайма рухає лише поштовх (IKnockbackable), його власний стейт-машин на паузі
    private bool isStaggered = false;

    // Поки true - слайм повністю заморожений (IFreezable): стейт-машина і рух на паузі
    private bool isFrozen = false;
    private Coroutine freezeRoutine;

    // Стани слайма
    private enum SlimeState { Idle, Charging, Dashing, Cooldown }
    private SlimeState state = SlimeState.Idle;

    private float stateTimer = 0f;
    private Vector2 dashDirection = Vector2.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<MonsterHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
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

    }

    void Update()
    {
        if (player == null) return;

        // Поки триває заморозка - слайм повністю стоїть, стейт-машина (Idle/Charging/Dashing/Cooldown) на паузі
        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0f);
            return;
        }

        // Поки триває поштовх - власна логіка слайма (Idle/Charging/Dashing/Cooldown) не втручається
        if (isStaggered)
        {
            animator.SetFloat("Speed", 1f);
            return;
        }

        switch (state)
        {
            case SlimeState.Idle:
                UpdateIdleState();
                break;

            case SlimeState.Charging:
                UpdateChargingState();
                break;

            case SlimeState.Dashing:
                UpdateDashingState();
                break;

            case SlimeState.Cooldown:
                UpdateCooldownState();
                break;
        }
    }

    // ---------- IDLE ----------
    // Стоїть на місці, поки гравець не потрапить у радіус виявлення - тоді починає заряд ривка
    void UpdateIdleState()
    {
        rb.linearVelocity = Vector2.zero;
        animator.SetFloat("Speed", 0f);

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= data.detectionRange)
        {
            StartCharging();
        }
    }

    // ---------- CHARGE ----------
    void StartCharging()
    {
        state = SlimeState.Charging;
        stateTimer = data.chargeDuration;

        rb.linearVelocity = Vector2.zero;
        animator.SetFloat("Speed", 0f);
        animator.SetBool("Charging", true);

        // Напрямок ривка вже визначаємо зараз - якщо не дозволено підправляти під час заряду,
        // саме цей напрямок і буде використано в дальшому ривку
        dashDirection = ((Vector2)(player.position - transform.position)).normalized;
        UpdateFacing(dashDirection);
    }

    void UpdateChargingState()
    {
        // Слайм стоїть на місці і "трясеться" (сама тряска - за анімацією/шейдером в Animator)
        rb.linearVelocity = Vector2.zero;

        if (data.trackPlayerDuringCharge)
        {
            dashDirection = ((Vector2)(player.position - transform.position)).normalized;
            UpdateFacing(dashDirection);
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            StartDashing();
        }
    }

    // ---------- DASH ----------
    void StartDashing()
    {
        state = SlimeState.Dashing;
        stateTimer = data.dashDuration;

        animator.SetBool("Charging", false);
        animator.SetBool("Dashing", true);
        animator.SetFloat("Speed", 1f);

        rb.linearVelocity = dashDirection * data.dashSpeed * speedMultiplier;
    }

    void UpdateDashingState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            StartCooldown();
        }
    }

    // ---------- COOLDOWN ----------
    void StartCooldown()
    {
        state = SlimeState.Cooldown;
        stateTimer = data.postDashCooldown;

        rb.linearVelocity = Vector2.zero;
        animator.SetBool("Dashing", false);
        animator.SetFloat("Speed", 0f);
    }

    void UpdateCooldownState()
    {
        rb.linearVelocity = Vector2.zero;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = SlimeState.Idle;
        }
    }

    // ---------- Допоміжне ----------
    void UpdateFacing(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            animator.SetInteger("Direction", direction.y > 0f ? 1 : 0);
        }
        else
        {
            animator.SetInteger("Direction", 2);
            spriteRenderer.flipX = direction.x > 0f;
        }
    }

    // Слайм б'є контактом (наприклад, під час ривка врізається в гравця)
    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealContactDamage(collision.collider);

        // Якщо влетів у стіну під час ривка - зупиняємо ривок раніше, ніж закінчиться dashDuration
        if (state == SlimeState.Dashing && collision.collider.CompareTag("Wall"))
        {
            StartCooldown();
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.collider);
    }

    void TryDealContactDamage(Collider2D other)
    {
        if (playerHealth == null) return;
        if (!other.CompareTag("Player")) return;
        if (Time.time < lastContactDamageTime + data.attackCooldown) return;

        lastContactDamageTime = Time.time;
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