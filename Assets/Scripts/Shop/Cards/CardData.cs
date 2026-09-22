using System.Globalization;
using UnityEngine;

public enum CardStatType
{
    MoveSpeed,
    ProjectileSpeed,
    Damage,
    AttackRange,
    AttackCooldown,   // додатнє значення = ЗМЕНШУЄ перезарядку (стріляє частіше)
    ProjectileSize,
    Spark,            // ціле число
    Luck,             // ціле число
    MaxHearts         // в ПОЛОВИНКАХ сердець: 2 = одне серце (ціле число)
}

// Створюється через Assets -> Create -> Shop -> Card.
// Одна картка = один стат + значення + чи вона червона (мінусова).
// value завжди пиши ДОДАТНІМ - знак мінус додається автоматично, якщо стоїть isNegative.
[CreateAssetMenu(fileName = "NewCard", menuName = "Shop/Card")]
public class CardData : ScriptableObject
{
    [Header("Ефект")]
    public CardStatType stat = CardStatType.ProjectileSpeed;
    [Tooltip("Розмір зміни (завжди додатнє число). Напр. 0.1")]
    public float value = 0.1f;
    [Tooltip("true = червона картка: значення віднімається замість додавання")]
    public bool isNegative = false;

    [Header("Випадання")]
    [Tooltip("Вага для випадкового вибору (більше = частіше)")]
    public int weight = 1;

    [Header("Спрайти (лицьова сторона)")]
    public Sprite frontSprite;      // картинка картки (рівна, фінальна)
    [Tooltip("Необов'язково: свій проміжний кадр саме цієї картки. Зазвичай лиши ПУСТИМ - бік беремо спільний з CardShopUI.flipFrames")]
    public Sprite frontTiltSprite;

    public float SignedValue => isNegative ? -Mathf.Abs(value) : Mathf.Abs(value);

    public void Apply(GameObject player)
    {
        if (player == null) return;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        float v = SignedValue;

        switch (stat)
        {
            case CardStatType.MoveSpeed: if (stats != null) stats.AddMoveSpeed(v); break;
            case CardStatType.ProjectileSpeed: if (stats != null) stats.AddProjectileSpeed(v); break;
            case CardStatType.Damage: if (stats != null) stats.AddDamage(v); break;
            case CardStatType.AttackRange: if (stats != null) stats.AddAttackRange(v); break;
            case CardStatType.AttackCooldown: if (stats != null) stats.ReduceAttackCooldown(v); break;
            case CardStatType.ProjectileSize: if (stats != null) stats.AddProjectileSize(v); break;
            case CardStatType.Spark: if (stats != null) stats.AddSpark(Mathf.RoundToInt(v)); break;
            case CardStatType.Luck: if (stats != null) stats.AddLuck(Mathf.RoundToInt(v)); break;
            case CardStatType.MaxHearts:
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null) health.AddMaxHearts(Mathf.RoundToInt(v));
                break;
        }
    }

    public string StatName
    {
        get
        {
            switch (stat)
            {
                case CardStatType.MoveSpeed: return "Швидкість";
                case CardStatType.ProjectileSpeed: return "Швидкість кульки";
                case CardStatType.Damage: return "Шкода";
                case CardStatType.AttackRange: return "Дальність";
                case CardStatType.AttackCooldown: return "Перезарядка";
                case CardStatType.ProjectileSize: return "Розмір кульки";
                case CardStatType.Spark: return "Іскра";
                case CardStatType.Luck: return "Удача";
                case CardStatType.MaxHearts: return "Серця";
                default: return stat.ToString();
            }
        }
    }

    // Текст для показу гравцю після відкриття, напр. "+0.1 Швидкість кульки"
    public string GetEffectLabel()
    {
        float shown = SignedValue;

        if (stat == CardStatType.AttackCooldown) shown = -shown;                     // показуємо реальну зміну перезарядки
        if (stat == CardStatType.MaxHearts) shown = Mathf.RoundToInt(shown) / 2f;    // половинки -> серця
        if (stat == CardStatType.Spark || stat == CardStatType.Luck) shown = Mathf.RoundToInt(shown);

        string sign = shown >= 0f ? "+" : "-";
        return $"{sign}{Mathf.Abs(shown).ToString("0.##", CultureInfo.InvariantCulture)} {StatName}";
    }
}