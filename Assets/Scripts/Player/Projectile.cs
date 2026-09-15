using UnityEngine;

// Причепи на префаб кульки (Sprite + Rigidbody2D + Collider2D з Is Trigger)
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    private float damage;
    private float maxRange;
    private Vector2 startPosition;
    private Rigidbody2D rb;
    private Vector3 baseScale; // оригінальний розмір префабу, щоб множити від нього, а не від 1

    private bool canBounce;
    private int bouncesLeft;
    private bool isHoming;
    private float homingStrength;
    private int pierceLeft;
    private bool pierceInfinite;
    private GameObject owner; // хто випустив кульку (гравець або монстр) - щоб не бити по собі

    // Базовий напрямок польоту "по суті" (без урахування зигзагу).
    // Його міняють: homing (плавно), chaotic (ривками), bounce (відбиття).
    // Zigzag НЕ чіпає цей вектор - він лише тимчасово відхиляє від нього кожен кадр.
    private Vector2 currentDirection;
    private float baseSpeed;
    private float spawnTime; // Time.time на момент Setup() - для фази зигзагу (працює і в польоті, і в орбіті)

    // --- Зигзаг ---
    private bool isZigzag;
    private float zigzagFrequency;
    private float zigzagAmplitude; // в градусах

    // --- Хаотичний рух ---
    private bool isChaotic;
    private float chaosStrength;         // макс. кут випадкового повороту за раз (градуси)
    private readonly float chaosInterval = 0.15f; // як часто міняється напрямок
    private float chaosTimer = 0f;
    private float chaosOrbitOffset = 0f; // накопичений випадковий зсув кута ПІД ЧАС орбіти (окремо від currentDirection)

    // --- Закручування (орбіта) навколо гравця ---
    private bool isOrbiting;
    private Transform orbitCenter;
    private float orbitRadius;
    private float orbitSpeed;    // градусів за секунду
    private float orbitDuration; // скільки секунд крутиться, перш ніж зникнути
    private float orbitAngle;
    private float orbitTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // повністю відключає фізичні поштовхи/зіткнення
        baseScale = transform.localScale;
    }

    // Викликається одразу після Instantiate, налаштовує кульку під поточні стати гравця
    // shooter - хто випустив кульку (передавай gameObject стрільця), щоб кулька не била по ньому самому
    public void Setup(Vector2 direction, float speed, float dmg, float range,
        bool bounce = false, int bounceCount = 0,
        bool homing = false, float homingStr = 3f,
        int pierceCount = 0,
        bool pierceIsInfinite = false,
        GameObject shooter = null,
        bool zigzag = false, float zigzagFreq = 6f, float zigzagAmp = 25f,
        bool chaotic = false, float chaosStr = 60f,
        bool orbit = false, float orbitRad = 2f, float orbitSpd = 180f, float orbitDur = 4f,
        float sizeMultiplier = 1f)
    {
        damage = dmg;
        maxRange = range;
        startPosition = transform.position;

        transform.localScale = baseScale * sizeMultiplier; // міняє видимий розмір і розмір колайдера разом

        canBounce = bounce;
        bouncesLeft = bounceCount;
        isHoming = homing;
        homingStrength = homingStr;
        pierceLeft = pierceCount;
        pierceInfinite = pierceIsInfinite;
        owner = shooter;

        isZigzag = zigzag;
        zigzagFrequency = zigzagFreq;
        zigzagAmplitude = zigzagAmp;

        isChaotic = chaotic;
        chaosStrength = chaosStr;

        isOrbiting = orbit;
        orbitRadius = orbitRad;
        orbitSpeed = orbitSpd;
        orbitDuration = orbitDur;
        orbitCenter = shooter != null ? shooter.transform : null;

        currentDirection = direction.normalized;
        baseSpeed = speed;
        spawnTime = Time.time;

        if (isOrbiting && orbitCenter != null)
        {
            // Стартовий кут орбіти = напрямок, куди дивився гравець при пострілі,
            // щоб кулька почала крутитись з того боку, куди вистрілили
            orbitAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.position = OrbitPosition(orbitAngle);
            UpdateOrbitRotation(orbitAngle);
            rb.linearVelocity = Vector2.zero; // рухаємось вручну по колу, а не через фізику
        }
        else
        {
            rb.linearVelocity = currentDirection * baseSpeed;
            UpdateRotation();
        }
    }

    void FixedUpdate()
    {
        if (isOrbiting)
            return; // орбітальний рух рахується в Update()

        if (isHoming)
        {
            Transform target = FindNearestMonster();
            if (target != null)
            {
                Vector2 desiredDirection = ((Vector2)target.position - rb.position).normalized;
                currentDirection = Vector2.Lerp(currentDirection, desiredDirection, homingStrength * Time.fixedDeltaTime).normalized;
            }
        }

        if (isChaotic)
        {
            chaosTimer -= Time.fixedDeltaTime;
            if (chaosTimer <= 0f)
            {
                float randomAngle = Random.Range(-chaosStrength, chaosStrength);
                currentDirection = RotateVector(currentDirection, randomAngle).normalized;
                chaosTimer = chaosInterval;
            }
        }

        // Зигзаг НЕ міняє currentDirection - він лише тимчасово відхиляє видиму швидкість
        // від "справжнього" напрямку, тому й не накопичується сам на собі кадр за кадром
        Vector2 finalDirection = currentDirection;
        if (isZigzag)
        {
            float t = Time.time - spawnTime;
            float angleOffset = zigzagAmplitude * Mathf.Sin(t * zigzagFrequency);
            finalDirection = RotateVector(currentDirection, angleOffset);
        }

        rb.linearVelocity = finalDirection.normalized * baseSpeed;
        UpdateRotation();
    }

    void Update()
    {
        if (isOrbiting)
        {
            orbitTimer += Time.deltaTime;
            orbitAngle += orbitSpeed * Time.deltaTime; // "чистий" прогрес орбіти, без домішок зигзагу/хаосу

            // Кут, який реально йде на позицію - orbitAngle + додаткові відхилення.
            // Ці відхилення НЕ пишемо назад в orbitAngle, інакше вони накопичувались б самі на собі.
            float visualAngle = orbitAngle;

            if (isChaotic)
            {
                chaosTimer -= Time.deltaTime;
                if (chaosTimer <= 0f)
                {
                    chaosOrbitOffset += Random.Range(-chaosStrength, chaosStrength);
                    chaosTimer = chaosInterval;
                }
                visualAngle += chaosOrbitOffset;
            }

            if (isZigzag)
            {
                float t = Time.time - spawnTime;
                visualAngle += zigzagAmplitude * Mathf.Sin(t * zigzagFrequency);
            }

            transform.position = OrbitPosition(visualAngle);
            UpdateOrbitRotation(visualAngle);

            if (orbitTimer >= orbitDuration)
            {
                Destroy(gameObject);
            }
            return; // поки крутиться навколо гравця - звичайну дальність польоту не рахуємо
        }

        float traveled = Vector2.Distance(startPosition, transform.position);
        if (traveled >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    // Позиція кульки на колі навколо orbitCenter при заданому куті
    // (кут передається ззовні, бо він може включати домішки зигзагу/хаосу поверх "чистого" orbitAngle)
    Vector2 OrbitPosition(float angle)
    {
        Vector2 center = orbitCenter != null ? (Vector2)orbitCenter.position : (Vector2)transform.position;
        float rad = angle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
        return center + offset;
    }

    // Розвертає спрайт по дотичній до кола, щоб виглядало, ніби кулька справді "крутиться"
    void UpdateOrbitRotation(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector2 tangent = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * Mathf.Sign(orbitSpeed);
        float angleDeg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    void UpdateRotation()
    {
        Vector2 dir = rb.linearVelocity;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // Повертає вектор на заданий кут (в градусах), проти годинникової стрілки
    Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // Шукає найближчого монстра в сцені (для самонаведення)
    // Використовуємо MonsterHealth, бо він є на будь-якому типі монстра (MonsterController, FlowerMonsterController і т.д.)
    Transform FindNearestMonster()
    {
        MonsterHealth[] monsters = FindObjectsByType<MonsterHealth>(FindObjectsSortMode.None);
        Transform nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (MonsterHealth monster in monsters)
        {
            float dist = Vector2.Distance(transform.position, monster.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = monster.transform;
            }
        }

        return nearest;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ігноруємо власного стрільця - кулька просто летить крізь нього, без шкоди й без знищення
        if (owner != null && other.transform.root.gameObject == owner)
            return;

        // Якщо стрілець - монстр (має MonsterHealth), кулька б'є ТІЛЬКИ гравця,
        // а інших монстрів ігнорує (щоб монстри не фрагали одне одного випадково)
        if (owner != null && owner.GetComponent<MonsterHealth>() != null)
        {
            if (!other.CompareTag("Player"))
            {
                // Не гравець і не стіна - просто ігноруємо (пролітаємо крізь інших монстрів)
                if (!other.CompareTag("Wall"))
                    return;
            }
        }

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);

            // Безкінечне пробиття - кулька просто летить далі, монстра ігноруємо як перешкоду
            if (pierceInfinite)
                return;

            // Якщо є запас пробиття - летимо далі крізь монстра
            if (pierceLeft > 0)
            {
                pierceLeft--;
                return;
            }

            Destroy(gameObject);
            return;
        }

        // Влучання в стіну (орбітальна кулька в стіни зазвичай не влітає, але про всяк випадок теж працює)
        if (other.CompareTag("Wall"))
        {
            if (canBounce && bouncesLeft > 0)
            {
                bouncesLeft--;
                Bounce(other);
            }
            else if (!isOrbiting)
            {
                Destroy(gameObject);
            }
        }
    }

    // Відбиває кульку від стіни, ніби дзеркальне відображення напрямку відносно нормалі поверхні
    void Bounce(Collider2D wall)
    {
        Vector2 closestPoint = wall.ClosestPoint(transform.position);
        Vector2 normal = ((Vector2)transform.position - closestPoint).normalized;

        if (normal == Vector2.zero)
            normal = -currentDirection; // запасний варіант, якщо кулька влучила точно в центр

        currentDirection = Vector2.Reflect(currentDirection, normal).normalized;
        rb.linearVelocity = currentDirection * baseSpeed;
        UpdateRotation();
    }
}