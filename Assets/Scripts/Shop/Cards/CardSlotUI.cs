using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Причепи на префаб однієї картки (Image + Button). 8 екземплярів під CardShopUI.slots.
// Переворот: рубашка -> рубашка (нахилена) -> лице (нахилене) -> лице,
// це кадри з твого спрайт-листа.
public class CardSlotUI : MonoBehaviour
{
    [Header("Посилання")]
    public Image cardImage;
    public Button button;
    [Tooltip("Необов'язково: текст під карткою з результатом, напр. '+0.1 Швидкість кульки'")]
    public TMP_Text resultText;

    [Header("Анімація")]
    public float flipFrameTime = 0.08f;

    [Header("Кольори тексту результату")]
    public Color positiveColor = new Color(0.3f, 0.9f, 0.3f);
    public Color negativeColor = new Color(0.9f, 0.3f, 0.3f);

    private CardShopManager manager;
    private Sprite backSprite;
    private Sprite[] flipFrames;
    private Coroutine flipRoutine;
    private bool showingFront;

    // Викликається один раз з CardShopUI
    public void Setup(int index, CardShopManager shopManager, Sprite back, Sprite[] frames)
    {
        manager = shopManager;
        backSprite = back;
        flipFrames = frames;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => manager.PickCard(index));
        }

        ShowBack();
    }

    // Викликається з CardShopUI.Refresh() щоразу, коли стан змінився
    public void Sync(bool revealed, CardData data, bool clickable)
    {
        if (revealed)
        {
            if (!showingFront) ShowFrontInstant(data); // напр. панель відкрили вже після відкриття картки
        }
        else
        {
            if (showingFront) ShowBack(); // новий розклад - знову рубашкою вгору
        }

        if (button != null) button.interactable = clickable && !revealed;
    }

    // Програє анімацію перевороту (викликається з CardShopUI, коли гравець відкрив картку)
    public void Flip(CardData data)
    {
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        showingFront = true;
        flipRoutine = StartCoroutine(FlipRoutine(data));
    }

    private IEnumerator FlipRoutine(CardData data)
    {
        if (resultText != null) resultText.text = "";

        // 1) рубашка (вже стоїть) -> 2) проміжні кадри (бік) -> 3) лице
        if (flipFrames != null)
        {
            foreach (Sprite frame in flipFrames)
            {
                if (frame == null) continue;
                cardImage.sprite = frame;
                yield return new WaitForSecondsRealtime(flipFrameTime);
            }
        }

        // Необов'язково: власний проміжний кадр цієї картки (якщо заданий в CardData)
        if (data.frontTiltSprite != null)
        {
            cardImage.sprite = data.frontTiltSprite;
            yield return new WaitForSecondsRealtime(flipFrameTime);
        }

        cardImage.sprite = data.frontSprite;
        ShowResultText(data);
        flipRoutine = null;
    }

    private void ShowFrontInstant(CardData data)
    {
        if (flipRoutine != null) { StopCoroutine(flipRoutine); flipRoutine = null; }
        showingFront = true;
        cardImage.sprite = data.frontSprite;
        ShowResultText(data);
    }

    private void ShowBack()
    {
        if (flipRoutine != null) { StopCoroutine(flipRoutine); flipRoutine = null; }
        showingFront = false;
        cardImage.sprite = backSprite;
        if (resultText != null) resultText.text = "";
    }

    private void ShowResultText(CardData data)
    {
        if (resultText == null) return;
        resultText.text = data.GetEffectLabel();
        resultText.color = data.isNegative ? negativeColor : positiveColor;
    }
}