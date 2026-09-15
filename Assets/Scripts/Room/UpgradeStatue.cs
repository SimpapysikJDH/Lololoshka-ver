using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Один варіант прокачки в пулі статуї. Заповнюється в інспекторі списком upgradePool.
[Serializable]
public class UpgradeOption
{
    public string title;
    [TextArea] public string description;
    public UpgradeType type;

    [Tooltip("Для MaxHealth - в 'половинках серця' (2 = ціле серце). " +
             "Для інших - звичайне значення бонусу (як в AddMoveSpeed/AddDamage і т.д.)")]
    public float amount = 1f;

    public Sprite icon; // необов'язково, для картинки на картці
}

public enum UpgradeType
{
    MaxHealth,
    MoveSpeed,
    Damage,
    ProjectileSpeed
}

// Причепи на GameObject статуї. Потрібен Collider2D (тригер) для виявлення гравця в радіусі.
[RequireComponent(typeof(Collider2D))]
public class UpgradeStatue : MonoBehaviour
{
    [Header("Клавіша взаємодії")]
    public Key interactKey = Key.E;

    [Header("Підказка 'Натисни E' (world space об'єкт над статуєю, необов'язково)")]
    public GameObject interactionPrompt;

    [Header("Скільки карток пропонувати на вибір")]
    [Range(2, 4)] public int cardsToOffer = 3;

    [Header("Пул можливих прокачок - додай варіанти (+ХП, +швидкість, +урон, +швидкість снаряду і т.д.)")]
    public List<UpgradeOption> upgradePool = new List<UpgradeOption>();

    [Header("UI вибору карток (перетягни панель зі скриптом UpgradeCardUI, з ANY сцени/канвасу)")]
    public UpgradeCardUI cardUI;

    [Header("Партікли після використання (постав як дочірній ParticleSystem, Play On Awake = вимкнено)")]
    public ParticleSystem usedParticles;

    [Header("Звук (необов'язково)")]
    public AudioSource audioSource;
    public AudioClip activateSfx;
    public AudioClip usedSfx;

    private Collider2D statueCollider;
    private bool playerInRange = false;
    private bool used = false;
    private Transform playerInStatue;
    private PlayerMovement playerMovement;

    void Awake()
    {
        statueCollider = GetComponent<Collider2D>();
        statueCollider.isTrigger = true;

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    void Update()
    {
        if (used || !playerInRange) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[interactKey].wasPressedThisFrame)
        {
            Activate();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        playerInStatue = other.transform;
        playerMovement = other.GetComponent<PlayerMovement>();

        if (interactionPrompt != null)
            interactionPrompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    void Activate()
    {
        if (cardUI == null)
        {
            Debug.LogWarning($"[{name}] UpgradeStatue: не задано cardUI - нема куди показувати картки.");
            return;
        }
        if (upgradePool == null || upgradePool.Count == 0)
        {
            Debug.LogWarning($"[{name}] UpgradeStatue: пул прокачок порожній.");
            return;
        }

        used = true; // одразу блокуємо повторне натискання, поки триває вибір

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Блокуємо рух гравця на час вибору картки (той самий SetBusy, що й під час атаки/діалогу)
        if (playerMovement != null)
            playerMovement.SetBusy(true);

        PlaySfx(activateSfx);

        List<UpgradeOption> offered = PickRandomOptions(cardsToOffer);
        cardUI.ShowCards(offered, OnCardChosen);
    }

    List<UpgradeOption> PickRandomOptions(int count)
    {
        // Перемішуємо копію пулу, щоб не показати одну й ту саму картку двічі за раз
        List<UpgradeOption> shuffled = new List<UpgradeOption>(upgradePool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled.Take(Mathf.Min(count, shuffled.Count)).ToList();
    }

    void OnCardChosen(UpgradeOption chosen)
    {
        ApplyUpgrade(chosen);

        if (playerMovement != null)
            playerMovement.SetBusy(false);

        PlayUsedEffect();
    }

    void ApplyUpgrade(UpgradeOption option)
    {
        if (playerInStatue == null) return;

        PlayerStats stats = playerInStatue.GetComponent<PlayerStats>();
        PlayerHealth health = playerInStatue.GetComponent<PlayerHealth>();

        switch (option.type)
        {
            case UpgradeType.MaxHealth:
                if (health != null)
                    health.AddMaxHearts(Mathf.RoundToInt(option.amount));
                break;

            case UpgradeType.MoveSpeed:
                if (stats != null)
                    stats.AddMoveSpeed(option.amount);
                break;

            case UpgradeType.Damage:
                if (stats != null)
                    stats.AddDamage(option.amount);
                break;

            case UpgradeType.ProjectileSpeed:
                if (stats != null)
                    stats.AddProjectileSpeed(option.amount);
                break;
        }
    }

    void PlayUsedEffect()
    {
        PlaySfx(usedSfx);

        if (usedParticles != null)
            usedParticles.Play();

        // Статуя лишається видимою на місці. used вже = true (виставлено в Activate),
        // тому OnTriggerEnter2D і Update більше не дадуть відкрити картки повторно -
        // саму статую вимикати/видаляти не треба.
        // Вимикаємо тригер, щоб гравець навіть при вході в зону більше не бачив підказку "E".
        statueCollider.enabled = false;
    }

    void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}