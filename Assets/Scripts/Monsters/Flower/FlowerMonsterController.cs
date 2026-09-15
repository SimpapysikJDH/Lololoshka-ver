using UnityEngine;

// Причепи на GameObject квітки
// Потрібні компоненти: MonsterHealth, SpriteRenderer, Animator
// Rigidbody2D НЕ потрібен - квітка не рухається
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class FlowerMonsterController : MonoBehaviour, IDamageable
{
    [Header("Налаштування цієї квітки")]
    public FlowerMonsterData data;

    private MonsterHealth health;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Transform player;
    private PlayerHealth playerHealth;

    private enum State { Hidden, Opening, Open, Closing }
    private State state = State.Hidden;

    // В Hidden цей таймер - кулдаун до наступного відкриття
    // В Opening/Closing - скільки лишилось до кінця анімації
    private float stateTimer = 0f;

    private float lastAttackTime = -999f;
    private bool isInvincible = false;

    void Awake()
    {
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
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        switch (state)
        {
            case State.Hidden:
                // stateTimer тут - кулдаун після попереднього закриття
                if (stateTimer > 0f)
                {
                    stateTimer -= Time.deltaTime;
                    break;
                }

                if (distance <= data.revealRange)
                    EnterOpening();
                break;

            case State.Opening:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    EnterOpen();
                break;

            case State.Open:
                if (distance <= data.attackRange && Time.time >= lastAttackTime + data.attackCooldown)
                    Attack();

                // Закриваємось, тільки коли гравець відійшов на hideRange (з запасом, щоб не тремтіло на межі)
                if (distance > data.hideRange)
                    EnterClosing();
                break;

            case State.Closing:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    EnterHidden();
                break;
        }
    }

    void EnterOpening()
    {
        state = State.Opening;
        stateTimer = data.openAnimDuration;
        animator.SetBool("Open", true);
    }

    void EnterOpen()
    {
        state = State.Open;
    }

    void EnterClosing()
    {
        state = State.Closing;
        stateTimer = data.closeAnimDuration;
        animator.SetBool("Open", false);
    }

    void EnterHidden()
    {
        state = State.Hidden;
        stateTimer = data.reopenCooldown;
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

    // Зручно бачити радіуси прямо в редакторі, не запускаючи гру
    void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, data.revealRange);

        Gizmos.color = new Color(1f, 0.7f, 0.4f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, data.hideRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);
    }
}