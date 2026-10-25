using NUnit.Framework.Interfaces;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Причепи на GameObject гравця (той самий, де PlayerMovement)
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Здоров'я (1 серце = 2 одиниці)")]
    public int maxHearts = 3;          // скільки сердець максимум
    public int currentHP;              // поточне здоров'я в одиницях

    [Header("Золоті серця (бонус збоку, необмежена кількість)")]
    public int goldenHeartCount = 0;

    [Header("Екран смерті")]
    [Tooltip("Назва сцени головного меню (має бути додана в Build Settings)")]
    public string menuSceneName = "MainMenu";
    [Tooltip("Шрифт для екрану смерті (з українськими літерами). Якщо порожньо - візьметься шрифт будь-якого TMP-тексту в сцені1")]
    public TMP_FontAsset deathScreenFont;

    [Header("Недоторканність після удару")]
    public float invincibilityDuration = 1f;
    public float flickerInterval = 0.1f; // як часто мигає спрайт під час недоторканності

    private bool isInvincible = false;
    private bool isDead = false;
    private SpriteRenderer spriteRenderer;

    // Підписуйся на цю подію в UI-скрипті, щоб оновлювати картинки сердець
    public event Action<int, int, int> OnHealthChanged; // (currentHP, maxHearts, goldenHeartCount)

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        currentHP = maxHearts * 2; // на старті повне здоров'я
        RunStats.StartRun();       // починаємо відлік часу забігу (спрацює лише раз)
        NotifyHealthChanged();
    }

    // Реалізація IDamageable - Projectile.cs (і взагалі будь-яка кулька) шукає саме цей метод
    // через GetComponent<IDamageable>(). Просто округлюємо до цілого і передаємо в звичайний TakeDamage(int)
    public void TakeDamage(float amount)
    {
        TakeDamage(Mathf.RoundToInt(amount));
    }

    public void TakeDamage(int amount)
    {
        if (isDead || isInvincible) return;

        // Золоте серце забирає удар повністю, замість звичайного здоров'я
        // Якщо їх декілька — знімається тільки одне за раз
        if (goldenHeartCount > 0)
        {
            goldenHeartCount--;
            StartCoroutine(InvincibilityRoutine());
            NotifyHealthChanged();
            return;
        }

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHearts * 2);
        NotifyHealthChanged();

        StartCoroutine(InvincibilityRoutine());

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHearts * 2);
        NotifyHealthChanged();
    }

    // Викликай це з артефакту, що збільшує/зменшує максимальне здоров'я
    // extraHalfHearts: 2 = одне повне нове серце, 1 = пів серця, -2 = мінус одне серце (від'ємні артефакти теж працюють)
    public void AddMaxHearts(int extraHalfHearts)
    {
        int oldMaxHP = maxHearts * 2;
        int newMaxHP = oldMaxHP + extraHalfHearts;
        newMaxHP = Mathf.Max(2, newMaxHP); // не даємо максимальному здоров'ю впасти нижче 1 серця

        maxHearts = Mathf.CeilToInt(newMaxHP / 2f);
        currentHP += extraHalfHearts; // нове серце одразу повне (або забирається, якщо віднімаємо)
        currentHP = Mathf.Clamp(currentHP, 1, maxHearts * 2); // залишаємо гравцю мінімум 1 HP, щоб не вбити його артефактом

        NotifyHealthChanged();
    }

    public void AddGoldenHeart()
    {
        goldenHeartCount++;
        NotifyHealthChanged();
    }

    // Тимчасова недоторканність з довільною тривалістю (на відміну від InvincibilityRoutine,
    // яка спрацьовує лише на invincibilityDuration після удару). Використовується, наприклад,
    // активним артефактом "Insurances" типу Invincibility з ActiveArtifactData.
    public void GrantTemporaryInvincibility(float duration)
    {
        StartCoroutine(TemporaryInvincibilityRoutine(duration));
    }

    private IEnumerator TemporaryInvincibilityRoutine(float duration)
    {
        isInvincible = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(flickerInterval);
            elapsed += flickerInterval;
        }

        spriteRenderer.enabled = true;
        isInvincible = false;
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHP, maxHearts, goldenHeartCount);
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        float elapsed = 0f;

        // Миготіння спрайту, щоб було видно недоторканність
        while (elapsed < invincibilityDuration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(flickerInterval);
            elapsed += flickerInterval;
        }

        spriteRenderer.enabled = true;
        isInvincible = false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Зупиняємо блимання спрайта і лишаємо гравця видимим
        StopAllCoroutines();
        spriteRenderer.enabled = true;

        // Забираємо керування, поки відкрита плашка смерті
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        PlayerAttack attack = GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = false;

        ActiveArtifactSlot activeSlot = GetComponent<ActiveArtifactSlot>();
        if (activeSlot != null) activeSlot.enabled = false;

        DeathScreen.Show(gameObject, menuSceneName, deathScreenFont);
    }
}