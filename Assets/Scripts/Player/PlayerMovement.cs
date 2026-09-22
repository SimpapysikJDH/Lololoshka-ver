using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviourPun
{
    [Header("Межі території (якщо потрібно обмежити рух)")]
    public bool useBounds = false;
    public float minX = -10f;
    public float maxX = 10f;
    public float minY = -10f;
    public float maxY = 10f;

    [Header("Стрибок (якщо потрібен)")]
    public bool canJump = false;
    public float jumpForce = 8f;
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.1f;

    [Header("Затримка перед рухом (коротке натискання = лише розвернутись)")]
    public float holdToMoveThreshold = 0.15f; // скільки секунд треба утримувати клавішу, щоб персонаж почав рухатись

    [Header("Під час діалогу/катсцени - розвертати персонажа обличчям до камери (Direction = Down)")]
    public bool faceCameraWhenBusy = true;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private PlayerStats stats;

    private Vector2 moveInput;       // фактичний ввід з клавіатури (напрямок, куди натиснуто)
    private Vector2 appliedVelocity; // швидкість, яку РЕАЛЬНО застосовуємо (0, поки триває коротке натискання)
    private float holdTimer = 0f;
    private bool isGrounded;
    private bool isBusy = false; // true під час анімацій типу "підняття артефакту" чи атаки

    // Публічний доступ для інших скриптів (наприклад PlayerAttack), щоб знати куди "дивиться" гравець
    public int LastDirection { get; private set; } = 0; // 0 = Down, 1 = Up, 2 = Side
    public bool FacingRight => spriteRenderer.flipX; // за поточною логікою flipX=true = дивиться вправо

    // Повертає напрямок, куди зараз "дивиться" гравець, як одиничний вектор
    // (потрібно, наприклад, для DashForwardRoutine в ActiveArtifactData)
    public Vector2 GetFacingDirection()
    {
        switch (LastDirection)
        {
            case 0: return Vector2.down;
            case 1: return Vector2.up;
            default: return FacingRight ? Vector2.right : Vector2.left; // 2 = Side
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        stats = GetComponent<PlayerStats>();
    }

    // Викликай це з інших скриптів (наприклад ArtifactPickup), щоб заблокувати
    // рух і руху-анімації на час програвання іншої анімації (Pickup, атака тощо)
    public void SetBusy(bool busy)
    {
        isBusy = busy;
        animator.SetBool("IsBusy", busy);

        if (busy)
        {
            // Одразу зупиняємо персонажа і скидаємо Speed, щоб не було руху "по інерції"
            moveInput = Vector2.zero;
            appliedVelocity = Vector2.zero;
            holdTimer = 0f;
            rb.linearVelocity = Vector2.zero;
            animator.SetFloat("Speed", 0f);

            // Розвертаємо персонажа обличчям до камери (Idle Down), незалежно від того,
            // в який бік він рухався перед цим - так виглядає природно для діалогу/катсцени
            if (faceCameraWhenBusy)
            {
                LastDirection = 0; // 0 = Down
                animator.SetInteger("Direction", 0);
            }
        }
    }

    void Update()
    {

        if (!photonView.IsMine)
        {
            return;
        }
        // Поки isBusy — повністю ігноруємо ввід гравця (персонаж не рухається)
        if (isBusy)
            return;

        // Зчитуємо ввід гравця через новий Input System (WASD)
        moveInput = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveInput.x = -1f;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveInput.x = 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            moveInput.y = -1f;
        else if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            moveInput.y = 1f;

        // Нормалізуємо, щоб рух по діагоналі не був швидшим
        if (moveInput.magnitude > 1f)
            moveInput.Normalize();

        bool hasInput = moveInput.magnitude > 0.1f;

        if (hasInput)
        {
            holdTimer += Time.deltaTime;
        }
        else
        {
            holdTimer = 0f;
        }

        // Поки не пройшов поріг утримання - рухатись НЕ дозволяємо (тільки дивимось у бік)
        bool canMoveNow = holdTimer >= holdToMoveThreshold;
        appliedVelocity = canMoveNow ? moveInput : Vector2.zero;

        // Передаємо швидкість в Animator, щоб перемикались Idle/Walk
        // (Idle, поки триває коротке натискання; Walk - тільки коли справді почав рухатись)
        animator.SetFloat("Speed", appliedVelocity.magnitude);

        // Визначаємо напрямок для Animator (0 = Down, 1 = Up, 2 = Side)
        // Оновлюємо ЗАВЖДИ при наявності вводу (навіть коротке натискання), щоб персонаж
        // одразу розвертався в потрібну сторону, ще до того як почне рухатись
        if (hasInput)
        {
            if (Mathf.Abs(moveInput.y) > Mathf.Abs(moveInput.x))
            {
                LastDirection = moveInput.y > 0f ? 1 : 0;
            }
            else
            {
                LastDirection = 2;
            }
            animator.SetInteger("Direction", LastDirection);
        }

        // Розвертаємо спрайт в бік руху (для Side анімації) - теж одразу, незалежно від holdTimer
        if (moveInput.x > 0.01f)
            spriteRenderer.flipX = true;
        else if (moveInput.x < -0.01f)
            spriteRenderer.flipX = false;

        // Стрибок (якщо потрібен — платформер)
        if (canJump)
        {
            isGrounded = groundCheck != null &&
                Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            if (isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
        }
    }

    void FixedUpdate()
    {

        if (!photonView.IsMine)
        {
            return;
        }
        // Поки isBusy (наприклад під час DashForwardRoutine з ActiveArtifactData) -
        // швидкістю керує зовнішній код, і FixedUpdate НЕ повинен її перезаписувати
        // на appliedVelocity (0) щокадру - інакше ривок постійно "гаситься" фізикою
        // раніше, ніж встигає застосуватись.
        if (!isBusy)
        {
            // Рух персонажа через фізику (плавний і без проходження крізь стіни)
            // Швидкість тепер береться з PlayerStats, щоб артефакти могли її змінювати
            // appliedVelocity дорівнює нулю, поки триває коротке натискання (гравець лише дивиться в бік)
            Vector2 targetVelocity = new Vector2(appliedVelocity.x * stats.moveSpeed, rb.linearVelocity.y);

            if (!canJump)
            {
                // Топ-даун режим: рух і по Y теж
                targetVelocity.y = appliedVelocity.y * stats.moveSpeed;
            }

            rb.linearVelocity = targetVelocity;
        }

        // Обмежуємо персонажа в межах заданої території (діє завжди, навіть під час isBusy)
        if (useBounds)
        {
            Vector2 clampedPosition = rb.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, minY, maxY);
            rb.position = clampedPosition;
        }
    }
}