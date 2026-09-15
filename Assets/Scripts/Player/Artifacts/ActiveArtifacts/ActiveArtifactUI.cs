using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Причепи на GameObject-слот внизу екрану (Canvas).
// Структура, яку варто зробити:
//
// ActiveArtifactSlot (Image - рамка слота)
//   └─ Icon              (Image - тьмяна "база" іконки, завжди видно в кольорі cooldownColor)
//   └─ CooldownFillImage (Image, Image Type = Filled, Fill Method = Radial 360,
//                         Fill Origin = Bottom, Clockwise = true.
//                         Постав його ТОЧНО ПОВЕРХ Icon (той самий розмір/позиція/anchors) -
//                         спрайт і колір (readyColor) підставляються скриптом самі,
//                         в інспекторі саму Image можна лишити порожньою.
//                         Це і є "заповнення кольором": поки кулдаун - лише частина
//                         іконки закольорована, решта лишається тьмяною основою знизу.)
//   └─ EmptySlotIcon     (Image - напівпрозора іконка "пусто", показується коли артефакту немає)
//   └─ ChargesText       (TextMeshPro - "x3" і т.п., ховається якщо у артефакту лише 1 заряд)
public class ActiveArtifactUI : MonoBehaviour
{
    [Header("Посилання на слот гравця")]
    [Tooltip("Якщо не задано - скрипт сам знайде ActiveArtifactSlot на сцені при старті")]
    public ActiveArtifactSlot slot;

    [Header("UI елементи")]
    public Image iconImage;
    public GameObject emptySlotIcon;

    [Tooltip("Image з Image Type = Filled (Radial 360), що показує прогрес кулдауну")]
    public Image cooldownFillImage;

    public TMP_Text chargesText;

    [Header("Кольори іконки")]
    [Tooltip("Колір іконки, коли артефакт готовий до використання")]
    public Color readyColor = Color.white;

    [Tooltip("Колір іконки, коли артефакт на кулдауні (зазвичай темніший/приглушений)")]
    public Color cooldownColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Плавність заповнення кулдауну")]
    [Tooltip("Швидкість, з якою fillAmount плавно доганяє ціль (більше = швидше)")]
    public float fillLerpSpeed = 4f;

    // Скільки кімнат треба було зачистити, коли кулдаун щойно почався.
    // Потрібно, щоб порахувати частку (fillAmount) від 0 до 1.
    private int maxRoomsForCurrentCooldown = 0;

    // Цільове значення fillAmount, до якого Update() плавно веде поточне
    private float targetFillAmount = 0f;

    // Хто був у слоті на попередній виклик HandleArtifactChanged - потрібно, щоб
    // відрізнити "поклали НОВИЙ артефакт" від "той самий, просто витратили заряд"
    private ActiveArtifactData lastArtifact = null;

    void OnEnable()
    {
        if (slot == null)
            slot = FindObjectOfType<ActiveArtifactSlot>();

        // Примусово виставляємо потрібний режим заливки кодом - не важливо,
        // що показує (чи не показує) інспектор, головне, що це РЕАЛЬНО
        // UnityEngine.UI.Image, і на ній фізично є ці поля.
        if (cooldownFillImage != null)
        {
            cooldownFillImage.type = Image.Type.Filled;
            cooldownFillImage.fillMethod = Image.FillMethod.Radial360;
            cooldownFillImage.fillOrigin = (int)Image.Origin360.Bottom;
            cooldownFillImage.fillClockwise = true;
        }

        if (slot != null)
        {
            slot.OnArtifactChanged += HandleArtifactChanged;
            slot.OnCooldownChanged += HandleCooldownChanged;

            // Показати актуальний стан одразу при увімкненні UI
            HandleArtifactChanged(slot.currentArtifact, slot.chargesLeft);
            HandleCooldownChanged(slot.roomsUntilReady);
        }
    }

    void OnDisable()
    {
        if (slot != null)
        {
            slot.OnArtifactChanged -= HandleArtifactChanged;
            slot.OnCooldownChanged -= HandleCooldownChanged;
        }
    }

    void Update()
    {
        if (cooldownFillImage == null) return;

        float current = cooldownFillImage.fillAmount;
        if (!Mathf.Approximately(current, targetFillAmount))
            cooldownFillImage.fillAmount = Mathf.MoveTowards(current, targetFillAmount, fillLerpSpeed * Time.deltaTime);
    }

    private void HandleArtifactChanged(ActiveArtifactData artifact, int chargesLeft)
    {
        bool hasArtifact = artifact != null;

        // isNewArtifact = true, лише якщо це справді ІНШИЙ предмет, ніж був у слоті
        // на попередній виклик - а не просто витрачений заряд того самого артефакту
        bool isNewArtifact = artifact != lastArtifact;
        lastArtifact = artifact;

        if (iconImage != null)
        {
            iconImage.enabled = hasArtifact;
            iconImage.sprite = hasArtifact ? artifact.icon : null;
            iconImage.color = cooldownColor; // база завжди тьмяна - "заповнена" частина видно через cooldownFillImage
        }

        if (cooldownFillImage != null)
        {
            cooldownFillImage.sprite = hasArtifact ? artifact.icon : null;
            cooldownFillImage.color = readyColor;
            cooldownFillImage.gameObject.SetActive(hasArtifact);
        }

        if (emptySlotIcon != null)
            emptySlotIcon.SetActive(!hasArtifact);

        if (chargesText != null)
        {
            bool showCharges = hasArtifact && artifact.charges > 1;
            chargesText.gameObject.SetActive(showCharges);
            if (showCharges)
                chargesText.text = "x" + chargesLeft;
        }

        if (!hasArtifact)
        {
            maxRoomsForCurrentCooldown = 0;
            targetFillAmount = 0f;
            if (cooldownFillImage != null)
                cooldownFillImage.fillAmount = 0f;
            return;
        }

        // ВАЖЛИВО: заливку "снапаємо" на повний колір ТІЛЬКИ якщо це справді новий
        // артефакт у слоті. Якщо просто витратили заряд того самого - кулдаун-анімацію,
        // яку вже запустив HandleCooldownChanged, чіпати не можна, інакше вона миттєво
        // "стрибає" назад у повний колір замість плавного згасання в сірий.
        if (isNewArtifact)
        {
            maxRoomsForCurrentCooldown = 0;
            targetFillAmount = 1f;
            if (cooldownFillImage != null)
                cooldownFillImage.fillAmount = 1f;
        }
    }

    private void HandleCooldownChanged(int roomsLeft)
    {
        bool onCooldown = roomsLeft > 0;

        // Запам'ятовуємо, з якого значення почався кулдаун (максимум),
        // щоб порахувати прогрес у діапазоні 0..1
        if (onCooldown && roomsLeft > maxRoomsForCurrentCooldown)
            maxRoomsForCurrentCooldown = roomsLeft;

        if (!onCooldown)
            maxRoomsForCurrentCooldown = 0;

        // Ціль для плавного "заповнення кольором" іконки:
        // 0 = щойно активували (іконка повністю тьмяна, ще нічого не заповнено),
        // 1 = кулдаун закінчився (іконка повністю в кольорі, готова).
        // Значення fillAmount тут НЕ виставляється напряму - лише ціль,
        // Update() сам плавно веде поточне значення до неї.
        targetFillAmount = (onCooldown && maxRoomsForCurrentCooldown > 0)
            ? 1f - (float)roomsLeft / maxRoomsForCurrentCooldown
            : 1f;
    }
}