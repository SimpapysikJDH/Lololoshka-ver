using System;
using System.Collections;
using UnityEngine;

// Причепи на GameObject монстра
public class MonsterHealth : MonoBehaviour, IDamageable
{
    // Підписуйся на це в RoomController, щоб знати коли монстр помер
    public event Action OnDeath;

    // НОВЕ: подія для UI (наприклад хп-бар боса). Кидає (currentHP, maxHP) кожного разу,
    // коли здоров'я змінюється (Initialize і TakeDamage)
    public event Action<float, float> OnHealthChanged;

    // Якщо false - MonsterHealth сам НЕ знищить об'єкт після Die().
    // Постав false у своєму контролері (наприклад SlimeController), якщо хочеш
    // спочатку програти анімацію смерті, а знищити об'єкт вже самостійно.
    // Для решти монстрів за замовчуванням true - поведінка та ж, що й раніше.
    [HideInInspector] public bool destroyOnDeath = true;

    [Header("Монетки (префаб з CoinPickup.cs)")]
    public GameObject coinPrefab; // признач в інспекторі на префабі кожного монстра

    [Header("Спалах при отриманні шкоди")]
    [Tooltip("Якщо не задано вручну - скрипт сам спробує знайти SpriteRenderer на цьому ж об'єкті або в дітях")]
    public SpriteRenderer spriteRenderer;
    public Color damageFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
    public float damageFlashDuration = 0.1f;

    public bool IsDead { get; private set; } = false;

    // НОВЕ: публічний доступ до поточного/максимального ХП, для UI хп-барів
    public float CurrentHP => currentHP;
    public float MaxHP => data != null ? data.maxHP : 0f;

    private float currentHP;
    private MonsterData data;
    private Color originalSpriteColor;
    private Coroutine flashRoutine;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            originalSpriteColor = spriteRenderer.color;
    }

    public void Initialize(MonsterData monsterData)
    {
        data = monsterData;
        currentHP = data.maxHP;

        OnHealthChanged?.Invoke(currentHP, data.maxHP);
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;

        OnHealthChanged?.Invoke(Mathf.Max(currentHP, 0f), data != null ? data.maxHP : 0f);

        if (spriteRenderer != null && amount > 0f)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color = damageFlashColor;
        yield return new WaitForSeconds(damageFlashDuration);
        spriteRenderer.color = originalSpriteColor;
        flashRoutine = null;
    }

    private void Die()
    {
        if (IsDead) return; // щоб Die() не спрацював двічі, якщо шкода прийде ще раз тим самим кадром
        IsDead = true;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            if (spriteRenderer != null)
                spriteRenderer.color = originalSpriteColor;
        }

        DropCoins();

        OnDeath?.Invoke();
        // Тут пізніше додамо: дроп предметів, звук тощо

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    // Спавнить випадкову кількість монеток навколо точки смерті монстра
    private void DropCoins()
    {
        if (coinPrefab == null || data == null) return;

        int count = UnityEngine.Random.Range(data.minCoinDrop, data.maxCoinDrop + 1);

        for (int i = 0; i < count; i++)
        {
            // Невеликий випадковий розкид, щоб монетки не спавнились одна в одній
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.3f;
            Vector3 spawnPos = transform.position + (Vector3)offset;

            Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        }
    }
}