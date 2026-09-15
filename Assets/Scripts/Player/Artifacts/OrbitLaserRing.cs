using System.Collections.Generic;
using UnityEngine;

// Комбо-ефект "Орбіта + Станції": лазерне кільце навколо гравця.
// - Слідує за гравцем (завжди по центру).
// - Радіус кільця = attackRange гравця, шкода за тік = damage гравця - ті самі звичайні стати,
//   що впливають і на звичайну кульку (Projectile). Розмір кільця завжди 1:1 з attackRange.
// - Крутиться візуально (текстура лінії "перетікає" по колу, якщо текстуру задано) - це чисто
//   візуальний параметр (comboRingScrollSpeed), зі статами гравця не пов'язаний.
// - ГОЛОВНЕ: живе ОБМЕЖЕНИЙ час (duration), після чого саме себе знищує і звільняє місце під наступне.
//
// Створюється виключно через StationController.SpawnComboOrbitLaser() -
// не потрібно вручну вішати цей компонент на префаби.
[RequireComponent(typeof(LineRenderer))]
public class OrbitLaserRing : MonoBehaviour
{
    private StationController owner;  // щоб повідомити, коли кільце себе знищить - StationController дозволить наступне
    private Transform anchor;         // гравець - кільце завжди лишається навколо нього
    private float radius;
    private float lineWidth;
    private float textureScrollSpeed; // швидкість "перетікання" текстури по колу (чисто візуал)
    private float duration;           // скільки секунд кільце існує, перш ніж зникнути
    private float damagePerTick;      // = stats.damage гравця - шкода одразу за кожен тік, а не "за секунду"
    private float damageTickInterval;
    private LayerMask enemyLayer;
    private int segments;

    // Homing: кільце більше НЕ лишається ідеальним колом і НЕ пускає окремий тонкий промінь -
    // натомість усе кільце цілком витягується в овальну "капсулу" (як бігова доріжка/скрепка)
    // в бік найближчого ворога, лишаючись тієї ж товщини лінії. Коли ворога нема - капсула
    // плавно стягується назад у звичайне коло (stretchLength -> 0).
    private bool homingBendEnabled;
    private float homingBendStrength;   // = PlayerStats.homingStrength, впливає на те, наскільки сильно витягується капсула
    private float homingSearchRadius;   // на скільки далі за сам радіус кільця шукати ворога

    private LineRenderer lr;
    private float elapsed;
    private float damageTickTimer;
    private float textureOffset;

    // Розтяг не з'являється миттєво (виглядало б як "різкий стрибок") - напрямок і довжина
    // плавно доростають/втягуються з кожним кадром до цільових значень, тому кільце виглядає
    // так, ніби воно АКТИВНО ТЯГНЕТЬСЯ до ворога, а не телепортується в нову форму.
    private const float BendSmoothSpeed = 6f;            // чим більше - тим швидше капсула встигає "дотягнутись"
    private const float StretchLengthScale = 0.15f;      // "сирий" стат homingStrength -> довжина розтягу (як частка radius)
    private const float StretchMaxLengthFraction = 1.5f; // максимальний розтяг відносно radius
    private Vector3 currentBendDir = Vector3.right;
    private float currentStretchLength; // 0 = звичайне коло, > 0 = капсула витягнута на цю відстань в бік ворога

    // Скільки точок кільця йде на "дальню" (біля гравця) і "ближню" (до ворога) півкапсули.
    // Разом дають плавну капсулу, а при stretchLength = 0 обидві половини складаються в рівне коло.
    private int farArcSegments;
    private int nearArcSegments;
    private Vector3[] ringPoints; // реальні точки кільця з ПОТОЧНОГО кадру - ApplyDamage() рахує шкоду саме по них

    public void Setup(StationController ownerController, Transform anchorTransform, float ringRadius, float scrollSpeed, float lifetimeSeconds,
        float width, float dmgPerTick, float tickInterval, LayerMask enemies,
        Color fallbackColor, Texture2D texture, Gradient gradient, int ringSegments,
        bool enableHomingBend = false, float homingBendStrengthValue = 0f, float homingSearchRadiusValue = 2.5f)
    {
        owner = ownerController;
        anchor = anchorTransform;
        radius = Mathf.Max(0.2f, ringRadius);
        lineWidth = width;
        textureScrollSpeed = scrollSpeed;
        duration = Mathf.Max(0.3f, lifetimeSeconds); // мінімум, щоб кільце не зникало миттєво від помилкового 0
        damagePerTick = Mathf.Max(0f, dmgPerTick);
        damageTickInterval = Mathf.Max(0.05f, tickInterval);
        enemyLayer = enemies;
        segments = Mathf.Max(16, ringSegments);

        homingBendEnabled = enableHomingBend;
        homingBendStrength = Mathf.Max(0f, homingBendStrengthValue);
        homingSearchRadius = Mathf.Max(0.1f, homingSearchRadiusValue);

        farArcSegments = Mathf.Max(4, segments / 2);
        nearArcSegments = Mathf.Max(4, segments - farArcSegments);
        ringPoints = new Vector3[farArcSegments + 1 + nearArcSegments + 1];

        lr = GetComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = ringPoints.Length;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.textureMode = LineTextureMode.Tile;
        lr.sortingOrder = 5;

        if (gradient != null && gradient.colorKeys.Length > 0)
        {
            lr.colorGradient = gradient;
        }
        else
        {
            lr.startColor = fallbackColor;
            lr.endColor = fallbackColor;
        }

        Material mat = new Material(Shader.Find("Sprites/Default"));
        if (texture != null)
        {
            mat.mainTexture = texture;
            float circumference = 2f * Mathf.PI * radius;
            mat.mainTextureScale = new Vector2(Mathf.Max(1f, circumference * 0.5f), 1f);
        }
        lr.material = mat;

        DrawRing();
    }

    void Update()
    {
        if (anchor != null)
            transform.position = anchor.position; // кільце завжди тримається навколо гравця, навіть якщо той рухається

        elapsed += Time.deltaTime;

        DrawRing();

        if (lr.material.mainTexture != null)
        {
            textureOffset -= textureScrollSpeed * Time.deltaTime * 0.1f;
            lr.material.mainTextureOffset = new Vector2(textureOffset, 0f);
        }

        damageTickTimer -= Time.deltaTime;
        if (damageTickTimer <= 0f)
        {
            damageTickTimer = damageTickInterval;
            ApplyDamage();
        }

        // ГОЛОВНЕ: кільце обмежене в часі - коли лічильник дійшов до duration, воно зникає само
        if (elapsed >= duration)
        {
            if (owner != null)
                owner.OnComboRingExpired();
            Destroy(gameObject);
        }
    }

    private void DrawRing()
    {
        Vector3 center = transform.position;

        Vector3? nearestEnemyDir = homingBendEnabled
            ? GetNearestEnemyDirection(center, radius + homingSearchRadius)
            : null;

        float targetStretch = 0f;
        Vector3 targetDir = currentBendDir;

        if (nearestEnemyDir.HasValue)
        {
            targetDir = nearestEnemyDir.Value;
            targetStretch = radius * Mathf.Clamp(homingBendStrength * StretchLengthScale, 0f, StretchMaxLengthFraction);
        }

        // Плавно "тягнемо" капсулу до цілі замість миттєвого стрибка - і напрямок, і довжина
        // розтягу окремо, кожен кадр трохи ближче (незалежно від FPS).
        float smoothT = 1f - Mathf.Exp(-BendSmoothSpeed * Time.deltaTime);
        currentBendDir = Vector3.Slerp(currentBendDir, targetDir, smoothT).normalized;
        currentStretchLength = Mathf.Lerp(currentStretchLength, targetStretch, smoothT);

        DrawCapsule(center, currentBendDir, currentStretchLength);
    }

    // Малює контур "капсули" (Minkowski-сума відрізка farCap->nearCap з колом радіусом radius) -
    // тобто дві півкола (навколо кожного кінця) + дві прямі сторони між ними. При stretchLength = 0
    // farCap і nearCap збігаються, і обидві півкола разом складаються рівно в звичайне коло -
    // тому кільце плавно й безшовно перетворюється з кола на витягнуту капсулу і назад.
    private void DrawCapsule(Vector3 center, Vector3 axis, float stretchLength)
    {
        Vector3 perp = new Vector3(-axis.y, axis.x, 0f);
        Vector3 farCap = center;                       // "задній" кінець - лишається на гравці
        Vector3 nearCap = center + axis * stretchLength; // "передній" кінець - тягнеться в бік ворога

        int idx = 0;

        // Дальня півкапсула: половина кола навколо farCap, ЗВЕРНЕНА ГЕТЬ від ворога (кут 90°..270°
        // відносно axis) - точки farCap + perp*radius .. farCap - perp*radius, проходячи позаду.
        for (int i = 0; i <= farArcSegments; i++)
        {
            float theta = Mathf.Lerp(90f, 270f, (float)i / farArcSegments) * Mathf.Deg2Rad;
            Vector3 offset = (Mathf.Cos(theta) * axis + Mathf.Sin(theta) * perp) * radius;
            Vector3 point = farCap + offset;
            ringPoints[idx] = point;
            lr.SetPosition(idx, point);
            idx++;
        }

        // Ближня півкапсула: половина кола навколо nearCap, ЗВЕРНЕНА В БІК ворога (кут 270°..450°,
        // тобто ті самі 270°..0°..90° по колу) - продовжує від попередньої точки (farCap - perp*radius)
        // прямою стороною до (nearCap - perp*radius), огинає вістря в бік цілі, і замикається назад
        // прямою стороною до першої точки дальньої півкапсули через loop = true на LineRenderer.
        for (int i = 0; i <= nearArcSegments; i++)
        {
            float theta = Mathf.Lerp(270f, 450f, (float)i / nearArcSegments) * Mathf.Deg2Rad;
            Vector3 offset = (Mathf.Cos(theta) * axis + Mathf.Sin(theta) * perp) * radius;
            Vector3 point = nearCap + offset;
            ringPoints[idx] = point;
            lr.SetPosition(idx, point);
            idx++;
        }
    }

    // Знаходить НАЙБЛИЖЧОГО ворога навколо кільця (крім самого гравця) і повертає напрямок
    // до нього від центру. null, якщо нікого немає в межах пошукового радіусу.
    private Vector3? GetNearestEnemyDirection(Vector3 center, float searchRadius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, searchRadius, enemyLayer);

        float nearestDist = float.MaxValue;
        Vector3? nearestDir = null;

        for (int i = 0; i < hits.Length; i++)
        {
            if (anchor != null && (hits[i].transform == anchor || hits[i].transform.IsChildOf(anchor)))
                continue;

            Vector3 toEnemy = hits[i].transform.position - center;
            float dist = toEnemy.magnitude;
            if (dist < 0.01f || dist >= nearestDist) continue;

            nearestDist = dist;
            nearestDir = toEnemy / dist;
        }

        return nearestDir;
    }

    // Шкода завжди рахується по РЕАЛЬНИХ точках поточної форми (коло чи витягнута капсула) -
    // капсулами вздовж кожного сегмента, тому урон точно накриває те, що намальовано на екрані.
    private void ApplyDamage()
    {
        if (damagePerTick <= 0f) return;

        HashSet<IDamageable> hitThisTick = new HashSet<IDamageable>();
        float checkRadius = lineWidth * 0.5f + 0.15f;

        for (int i = 0; i < ringPoints.Length; i++)
        {
            Vector3 a = ringPoints[i];
            Vector3 b = ringPoints[(i + 1) % ringPoints.Length];
            CollectDamageableAlongSegment(a, b, checkRadius, hitThisTick);
        }

        foreach (IDamageable damageable in hitThisTick)
            damageable.TakeDamage(damagePerTick);
    }

    // Збирає IDamageable вздовж одного сегмента (капсула між двома сусідніми точками) - так
    // шкода реально накриває ту ж форму, що намальована на екрані.
    private void CollectDamageableAlongSegment(Vector3 a, Vector3 b, float checkRadius, HashSet<IDamageable> result)
    {
        Vector2 dir = b - a;
        float length = dir.magnitude;

        Collider2D[] hits;
        if (length < 0.001f)
        {
            hits = Physics2D.OverlapCircleAll(a, checkRadius, enemyLayer);
        }
        else
        {
            Vector2 segCenter = (a + b) * 0.5f;
            float angle = Vector2.SignedAngle(Vector2.up, dir);
            Vector2 size = new Vector2(checkRadius * 2f, length + checkRadius * 2f);
            hits = Physics2D.OverlapCapsuleAll(segCenter, size, CapsuleDirection2D.Vertical, angle, enemyLayer);
        }

        for (int i = 0; i < hits.Length; i++)
        {
            // Гравець теж може потрапити в маску, якщо шар "Enemy" не знайдено і StationController
            // перевіряє УСІ шари - тому явно ігноруємо самого гравця (anchor) і його дочірні колайдери.
            if (anchor != null && (hits[i].transform == anchor || hits[i].transform.IsChildOf(anchor)))
                continue;

            IDamageable damageable = hits[i].GetComponent<IDamageable>();
            if (damageable != null)
                result.Add(damageable);
        }
    }
}