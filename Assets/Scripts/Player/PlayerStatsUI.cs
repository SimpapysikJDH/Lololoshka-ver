using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using TMPro;

// Причепи цей скрипт на порожню панель у лівій частині UI (там, де на скріні обведено кружком).
// Структура ієрархії, яку варто зробити в Canvas:
//
// StatsPanel (Image/Panel, з Vertical Layout Group)
//   └─ StatsUI (цей скрипт)
//   └─ MoveSpeedText     (TextMeshPro - Text)
//   └─ DamageText        (TextMeshPro - Text)
//   └─ AttackRangeText   (TextMeshPro - Text)
//   └─ CooldownText      (TextMeshPro - Text)
//   └─ ProjSpeedText     (TextMeshPro - Text)
//   └─ ProjSizeText      (TextMeshPro - Text)
//   └─ SparkText         (TextMeshPro - Text)
//   └─ LuckText          (TextMeshPro - Text)
//
// Перетягни ці Text-об'єкти у відповідні поля нижче в інспекторі.
// Якщо якийсь стат показувати не треба - просто залиш поле пустим, скрипт це обробить.
public class PlayerStatsUI : MonoBehaviour
{
    [Header("Посилання на гравця")]
    [Tooltip("Якщо не задано - скрипт сам знайде PlayerStats на сцені при старті")]
    public PlayerStats playerStats;

    [Header("Посилання на станції (необов'язково)")]
    [Tooltip("Якщо не задано - скрипт сам спробує знайти StationController на тому ж об'єкті, що й PlayerStats, при старті. Поки фіча станцій не увімкнена артефактом, поля станцій просто лишаються порожні.")]
    public StationController stationController;

    [Header("Текстові поля (TextMeshPro), не обов'язково заповнювати всі")]
    public TMP_Text moveSpeedText;
    public TMP_Text damageText;
    public TMP_Text attackRangeText;
    public TMP_Text attackCooldownText;
    public TMP_Text projectileSpeedText;
    public TMP_Text projectileSizeText;
    public TMP_Text sparkText;
    public TMP_Text luckText;

    [Header("Формати виводу (0.0 і т.д. - кількість знаків після коми)")]
    public string moveSpeedFormat = "Швидкість: {0:0.0}";
    public string damageFormat = "Шкода: {0:0.#}";
    public string attackRangeFormat = "Дальність: {0:0.#}";
    public string attackCooldownFormat = "Перезарядка: {0:0.00}с";
    public string projectileSpeedFormat = "Швидкість кульки: {0:0.#}";
    public string projectileSizeFormat = "Розмір кульки: x{0:0.0#}";
    public string sparkFormat = "Іскра: {0}";
    public string luckFormat = "Удача: {0}";

    [Header("Спливаюча підказка про зміну стату (+2 / -1)")]
    [Tooltip("Скільки секунд тримати підказку про зміну стату на екрані")]
    public float statChangePopupDuration = 1.5f;
    public Color statIncreaseColor = new Color(0.3f, 0.9f, 0.3f); // зелений = плюс
    public Color statDecreaseColor = new Color(0.9f, 0.3f, 0.3f); // червоний = мінус

    // Внутрішнє представлення одного стату: яке текстове поле, який формат,
    // чи це ціле число, і як дістати поточне значення з PlayerStats.
    private class StatField
    {
        public TMP_Text text;
        public string format;
        public bool isInt;
        public System.Func<float> getValue;
        public Coroutine popupRoutine;
    }

    private Dictionary<string, StatField> fields;

    private void Awake()
    {
        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();

        // Підписуємось ЗАЗДАЛЕГІДЬ (ще до того, як гравець щось підбере), щоб не пропустити
        // момент, коли артефакт станцій створить StationController і одразу ж поміняє його стати.
        StationController.OnAnyStationControllerCreated += HandleStationControllerCreated;

        if (stationController == null && playerStats != null)
            stationController = playerStats.GetComponent<StationController>();

        BuildFieldMap();
    }

    private void OnDestroy()
    {
        StationController.OnAnyStationControllerCreated -= HandleStationControllerCreated;
    }

    // Викликається СИНХРОННО з StationController.Awake() в момент AddComponent -
    // тобто ще ДО того, як ArtifactData встигне викликати EnableFeature/SetLinkDamage/тощо.
    private void HandleStationControllerCreated(StationController created)
    {
        if (playerStats == null || created == null) return;
        if (created.gameObject != playerStats.gameObject) return; // не наш гравець - ігноруємо

        SubscribeStationController(created);
    }

    private void BuildFieldMap()
    {
        fields = new Dictionary<string, StatField>
        {
            ["moveSpeed"] = new StatField
            {
                text = moveSpeedText,
                format = moveSpeedFormat,
                isInt = false,
                getValue = () => playerStats.moveSpeed
            },
            ["damage"] = new StatField
            {
                text = damageText,
                format = damageFormat,
                isInt = false,
                getValue = () => playerStats.damage
            },
            ["attackRange"] = new StatField
            {
                text = attackRangeText,
                format = attackRangeFormat,
                isInt = false,
                getValue = () => playerStats.attackRange
            },
            ["attackCooldown"] = new StatField
            {
                text = attackCooldownText,
                format = attackCooldownFormat,
                isInt = false,
                getValue = () => playerStats.attackCooldown
            },
            ["projectileSpeed"] = new StatField
            {
                text = projectileSpeedText,
                format = projectileSpeedFormat,
                isInt = false,
                getValue = () => playerStats.projectileSpeed
            },
            ["projectileSize"] = new StatField
            {
                text = projectileSizeText,
                format = projectileSizeFormat,
                isInt = false,
                getValue = () => playerStats.projectileSizeMultiplier
            },
            ["spark"] = new StatField
            {
                text = sparkText,
                format = sparkFormat,
                isInt = true,
                getValue = () => playerStats.spark
            },
            ["luck"] = new StatField
            {
                text = luckText,
                format = luckFormat,
                isInt = true,
                getValue = () => playerStats.luck
            },
        };
    }

    private float stationBindCheckTimer;
    private StationController subscribedStationController; // на що зараз реально підписані OnStatsChanged/OnStatChanged

    private void OnEnable()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged += RefreshUI;
            playerStats.OnStatChanged += HandleStatChanged;
        }

        if (stationController != null)
        {
            SubscribeStationController(stationController);
        }
        else
        {
            // Може артефакт станцій вже підібраний, а посилання просто не встигло проставитись
            // (наприклад, панель була вимкнена в момент підбору) - пробуємо знайти одразу.
            TryBindStationController();
        }

        // Показати актуальні значення одразу при увімкненні панелі
        RefreshUI();
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= RefreshUI;
            playerStats.OnStatChanged -= HandleStatChanged;
        }

        UnsubscribeStationController();
    }

    private void Update()
    {
        // Підстраховка на випадок, якщо статична подія з якоїсь причини не спрацювала
        // (наприклад, StationController був доданий, поки цей скрипт був вимкнений/неактивний).
        if (stationController != null) return;

        stationBindCheckTimer -= Time.deltaTime;
        if (stationBindCheckTimer <= 0f)
        {
            stationBindCheckTimer = 0.5f;
            TryBindStationController();
        }
    }

    // Знаходить щойно підібраний StationController і підписується на його події,
    // щоб панель одразу почала показувати стати станції.
    private void TryBindStationController()
    {
        if (playerStats == null) return;

        StationController found = playerStats.GetComponent<StationController>();
        if (found == null) return;

        SubscribeStationController(found);
    }

    // Єдина точка підписки на StationController - гарантує, що ми ніколи не підписані
    // двічі (що подвоїло б попапи) і що стара підписка знімається при зміні інстансу.
    private void SubscribeStationController(StationController controller)
    {
        if (controller == null || controller == subscribedStationController) return;

        UnsubscribeStationController();

        stationController = controller;
        subscribedStationController = controller;
        controller.OnStatsChanged += RefreshUI;
        controller.OnStatChanged += HandleStatChanged;

        RefreshUI();
    }

    private void UnsubscribeStationController()
    {
        if (subscribedStationController == null) return;

        subscribedStationController.OnStatsChanged -= RefreshUI;
        subscribedStationController.OnStatChanged -= HandleStatChanged;
        subscribedStationController = null;
    }

    public void RefreshUI()
    {
        if (playerStats == null || fields == null) return;

        foreach (var kvp in fields)
        {
            if (kvp.Value.popupRoutine == null) // не чіпаємо поле, поки на ньому крутиться попап
                SetBaseText(kvp.Value);
        }
    }

    // Записує звичайний (без +/- підказки) текст стату, завжди з крапкою замість коми.
    private void SetBaseText(StatField field)
    {
        if (field.text == null) return;
        field.text.text = FormatValue(field);
    }

    // Формат значення стату з крапкою як роздільником, незалежно від системної локалі
    // (щоб не показувало "5,0" замість "5.0").
    private string FormatValue(StatField field)
    {
        if (field.isInt)
            return string.Format(CultureInfo.InvariantCulture, field.format, Mathf.RoundToInt(field.getValue()));

        return string.Format(CultureInfo.InvariantCulture, field.format, field.getValue());
    }

    // Форматує саму дельту (без знаку - знак додається окремо) тим самим стилем чисел.
    private string FormatDelta(StatField field, float delta)
    {
        float absDelta = Mathf.Abs(delta);
        if (field.isInt)
            return Mathf.RoundToInt(absDelta).ToString(CultureInfo.InvariantCulture);

        return absDelta.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void HandleStatChanged(string statKey, float delta)
    {
        if (fields == null || !fields.TryGetValue(statKey, out StatField field)) return;
        if (field.text == null) return;

        if (field.popupRoutine != null)
            StopCoroutine(field.popupRoutine);

        field.popupRoutine = StartCoroutine(ShowStatChangePopup(field, delta));
    }

    private IEnumerator ShowStatChangePopup(StatField field, float delta)
    {
        string baseText = FormatValue(field);
        bool isPositive = delta > 0f;
        string sign = isPositive ? "+" : "-";
        string colorHex = ColorUtility.ToHtmlStringRGB(isPositive ? statIncreaseColor : statDecreaseColor);
        string deltaText = FormatDelta(field, delta);

        field.text.text = $"{baseText} <color=#{colorHex}>({sign}{deltaText})</color>";

        yield return new WaitForSeconds(statChangePopupDuration);

        // Показуємо актуальне значення (могло змінитись ще раз, поки чекали)
        field.text.text = FormatValue(field);
        field.popupRoutine = null;
    }
}