using UnityEngine;

// Причепи на GameObject гравця (той самий, де PlayerMovement і PlayerHealth)
public class PlayerStats : MonoBehaviour
{
    [Header("Швидкість гравця")]
    public float moveSpeed = 5f;

    [Header("Снаряди - базові")]
    public float projectileSpeed = 5f;
    public float damage = 1f;
    public float attackRange = 6f;      // дальність польоту кульки
    public float attackCooldown = 0.4f; // затримка між пострілами
    public float projectileSizeMultiplier = 1f; // 1 = звичайний розмір, 1.5 = +50% розміру снаряда

    [Header("Снаряди - спецефекти (вмикаються артефактами)")]
    public bool projectileBounce = false;      // відбивається від стін замість зникнення
    public int projectileBounceCount = 2;      // скільки разів встигає відбитись, перш ніж зникне
    public bool projectileHoming = false;      // заокруглюється (летить за найближчим монстром)
    public float projectileHomingStrength = 3f; // наскільки різко "заокруглюється" (вищий = крутіший поворот)
    public int projectilePierceCount = 0;      // скільки монстрів кулька пробиває наскрізь (0 = зникає після 1-го)
    public bool projectilePierceInfinite = false; // ігнорує projectilePierceCount - летить крізь УСІХ монстрів

    [Header("Снаряди - типи польоту (вмикаються артефактами, можна поєднувати)")]
    public bool projectileZigzag = false;
    public float projectileZigzagFrequency = 6f;   // швидкість "хвилі" зигзагу
    public float projectileZigzagAmplitude = 25f;  // максимальний кут відхилення (градуси)

    public bool projectileChaotic = false;
    public float projectileChaosStrength = 60f;    // макс. кут випадкового повороту за один "ривок" (градуси)

    public bool projectileOrbit = false;           // снаряд летить по колу навколо гравця, а не вперед
    public float projectileOrbitRadius => attackRange;     // радіус кола = звичайний attackRange (сам оновлюється разом з ним)

    [Tooltip("Швидкість обертання орбіти (град/сек) = projectileSpeed * цей множник. " +
             "projectileSpeed - лінійна швидкість польоту кульки (типово 3-10), а орбіті потрібні " +
             "ГРАДУСИ за секунду (типово 100-300) - без множника кулька крутилась би на 3-10 градусів " +
             "за секунду і виглядала майже нерухомою. За замовчуванням 36, щоб типовий projectileSpeed=5 " +
             "давав ті самі 180 град/сек, що стояли в орбіти до прив'язки до projectileSpeed.")]
    public float orbitSpeedMultiplier = 36f;
    public float projectileOrbitSpeed => projectileSpeed * orbitSpeedMultiplier;  // швидкість обертання, град/сек - росте разом з projectileSpeed

    public float projectileOrbitDuration = 4f;     // скільки секунд крутиться, перш ніж зникнути (окремий незалежний стат)

    [Header("Особливі ресурси")]
    public int spark = 0;  // Іскра
    public int luck = 0;   // Удача

    [Header("Валюта")]
    public int coins = 0;  // Монетки, які випадають з монстрів

    // Підписуйся на цю подію в UI-скрипті, щоб оновлювати лічильник монет на екрані
    public event System.Action<int> OnCoinsChanged;

    // Підписуйся на цю подію (наприклад, в PlayerStatsUI), щоб оновлювати панель статів
    // на екрані щоразу, коли артефакт або будь-що інше змінює стати гравця.
    public event System.Action OnStatsChanged;

    // Підписуйся на цю подію, щоб показати спливаючу підказку "+2" / "-1" біля конкретного стату.
    // statKey - ключ стату ("moveSpeed", "damage", "attackRange", "attackCooldown",
    // "projectileSpeed", "projectileSize", "spark", "luck"), delta - реальна зміна
    // значення (може відрізнятись від переданого amount через Clamp/Max).
    public event System.Action<string, float> OnStatChanged;

    private void NotifyStatsChanged(string statKey = null, float delta = 0f)
    {
        OnStatsChanged?.Invoke();
        if (statKey != null && delta != 0f)
            OnStatChanged?.Invoke(statKey, delta);
    }

    // --- Методи для артефактів ---
    // Amount може бути як додатнім (бонус), так і від'ємним (штраф/віднімання стату).
    // Всюди стоїть Mathf.Max/Clamp, щоб стат не пішов у нуль чи в мінус і не зламав гру.
    public void AddMoveSpeed(float amount)
    {
        float oldValue = moveSpeed;
        moveSpeed = Mathf.Max(0.5f, moveSpeed + amount);
        NotifyStatsChanged("moveSpeed", moveSpeed - oldValue);
    }

    public void AddProjectileSpeed(float amount)
    {
        float oldValue = projectileSpeed;
        projectileSpeed = Mathf.Max(0.5f, projectileSpeed + amount);
        NotifyStatsChanged("projectileSpeed", projectileSpeed - oldValue);
    }

    public void AddDamage(float amount)
    {
        float oldValue = damage;
        damage = Mathf.Max(0.1f, damage + amount);
        NotifyStatsChanged("damage", damage - oldValue);
    }

    public void AddAttackRange(float amount)
    {
        float oldValue = attackRange;
        attackRange = Mathf.Max(0.5f, attackRange + amount);
        NotifyStatsChanged("attackRange", attackRange - oldValue);
    }

    public void ReduceAttackCooldown(float amount)
    {
        float oldValue = attackCooldown;
        attackCooldown = Mathf.Max(0.05f, attackCooldown - amount);
        NotifyStatsChanged("attackCooldown", attackCooldown - oldValue);
    }

    public void AddProjectileSize(float amount)
    {
        float oldValue = projectileSizeMultiplier;
        projectileSizeMultiplier = Mathf.Max(0.1f, projectileSizeMultiplier + amount);
        NotifyStatsChanged("projectileSize", projectileSizeMultiplier - oldValue);
    }

    public void AddSpark(int amount)
    {
        int oldValue = spark;
        spark = Mathf.Max(0, spark + amount);
        NotifyStatsChanged("spark", spark - oldValue);
    }

    public void AddLuck(int amount)
    {
        int oldValue = luck;
        luck = Mathf.Max(0, luck + amount);
        NotifyStatsChanged("luck", luck - oldValue);
    }

    public void EnableBounce(int extraBounces)
    {
        projectileBounce = true;
        projectileBounceCount = Mathf.Max(0, projectileBounceCount + extraBounces);
        NotifyStatsChanged();
    }

    public void EnableHoming(float strength)
    {
        projectileHoming = true;
        projectileHomingStrength = strength;
        NotifyStatsChanged();
    }

    public void AddPierce(int amount)
    {
        projectilePierceCount = Mathf.Max(0, projectilePierceCount + amount);
        NotifyStatsChanged();
    }

    public void EnableInfinitePierce()
    {
        projectilePierceInfinite = true;
        NotifyStatsChanged();
    }

    public void EnableZigzag(float frequencyBonus, float amplitudeBonus)
    {
        projectileZigzag = true;
        projectileZigzagFrequency = Mathf.Max(0.1f, projectileZigzagFrequency + frequencyBonus);
        projectileZigzagAmplitude = Mathf.Max(0f, projectileZigzagAmplitude + amplitudeBonus);
        NotifyStatsChanged();
    }

    public void EnableChaotic(float strengthBonus)
    {
        projectileChaotic = true;
        projectileChaosStrength = Mathf.Max(0f, projectileChaosStrength + strengthBonus);
        NotifyStatsChanged();
    }

    // Увага: для снаряда, що постійно крутиться навколо гравця, задавай в артефакті
    // великий pierceBonus (наприклад +999) або enableInfinitePierce, інакше кулька
    // зникне після першого ж влучання
    public void EnableOrbit(float durationBonus)
    {
        projectileOrbit = true;
        // Радіус і швидкість обертання більше не окремі стати - вони завжди дорівнюють
        // звичайним attackRange і projectileSpeed, тому самі оновлюються, якщо ці стати
        // зміняться від інших артефактів. Тут задається лише тривалість орбіти.
        projectileOrbitDuration = Mathf.Max(0.1f, projectileOrbitDuration + durationBonus);
        NotifyStatsChanged();
    }

    // --- Монетки ---
    public void AddCoins(int amount)
    {
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    // Для витрат у крамниці тощо. Повертає true, якщо вистачило монет і списання відбулось
    public bool SpendCoins(int amount)
    {
        if (coins < amount) return false;

        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }
}