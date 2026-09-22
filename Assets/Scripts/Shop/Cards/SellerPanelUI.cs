using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Події, на які продавець реагує фразою. Їх кидають ShopManager і CardShopManager.
public enum SellerEvent
{
    Greeting,         // гравець відкрив магазин
    Purchased,        // купив артефакт
    NotEnoughCoins,   // не вистачає монет
    NotEnoughHearts,  // не вистачає сердець
    AlreadySold,      // товар уже забрав інший гравець
    Paid,             // (картки) оплатив тариф
    PayFirst,         // (картки) спробував платити, не відкривши оплачені картки
    GoodCard,         // (картки) випала хороша картка
    BadCard           // (картки) випала червона картка
}

// Віконце продавця: іконка (портрет) + панелька-картинка з текстом, який продавець "говорить".
// ВБУДОВАНИХ ФРАЗ НЕМАЄ - продавець каже лише те, що ти впишеш у списки в інспекторі.
// Якщо список для якоїсь події порожній - на цю подію продавець мовчить.
//
// ЯК НАЛАШТУВАТИ:
// 1) Всередині panelRoot магазину (ShopUI.panelRoot / CardShopUI.panelRoot) створи порожній об'єкт "SellerPanel"
//    і причепи на нього цей скрипт. Він має бути ДОЧІРНІМ до panelRoot - тоді при відкритті
//    магазину він сам увімкнеться і продавець привітається.
// 2) Всередині SellerPanel:
//      - Image "Portrait"  -> іконка продавця   (в поле portraitImage)
//      - Image "TextPanel" -> картинка-панелька (в поле textPanelImage)
//          - TextMeshPro "SpeechText" (дочірній до TextPanel) -> в поле speechText
// 3) У полі shopManager АБО cardShopManager перетягни менеджер свого магазину (одне з двох).
// 4) Заповни списки фраз. Якщо в списку кілька фраз - вибирається випадкова.
public class SellerPanelUI : MonoBehaviour
{
    [Header("Посилання на UI")]
    [Tooltip("Іконка продавця")]
    public Image portraitImage;
    [Tooltip("Картинка-панелька, на якій лежить текст")]
    public Image textPanelImage;
    public TMP_Text speechText;

    [Header("Джерело подій (став ОДИН з двох)")]
    public ShopManager shopManager;
    public CardShopManager cardShopManager;

    [Header("Спрайти продавця (необов'язково)")]
    public Sprite normalSprite;
    public Sprite happySprite;
    public Sprite sadSprite;
    [Tooltip("Скільки секунд тримається емоція, потім повертається звичайний спрайт")]
    public float moodDuration = 2f;

    [Header("Текст")]
    [Tooltip("Весь текст продавця завжди КАПСОМ (незалежно від того, як ти його написав у списках)")]
    public bool allCaps = true;
    [Tooltip("Ефект друкарської машинки")]
    public bool typewriter = true;
    public float charDelay = 0.03f;
    [Tooltip("Через скільки секунд тиші продавець дасть підказку зі списку idleHints. 0 = без підказок")]
    public float idleHintInterval = 10f;
    [Tooltip("Ховати панельку з текстом, поки продавцю нічого казати (напр. список фраз порожній)")]
    public bool hidePanelWhenEmpty = false;

    [Header("Твої фрази (одна або кілька на подію; кілька = випадкова)")]
    [TextArea] public string[] greeting;
    [TextArea] public string[] idleHints;
    [TextArea] public string[] purchased;
    [TextArea] public string[] notEnoughCoins;
    [TextArea] public string[] notEnoughHearts;
    [TextArea] public string[] alreadySold;
    [TextArea] public string[] paid;
    [TextArea] public string[] payFirst;
    [TextArea] public string[] goodCard;
    [TextArea] public string[] badCard;

    private Coroutine speakRoutine;
    private Coroutine moodRoutine;
    private float lastSpeakTime;
    private bool speaking;
    private string lastPhrase;

    void OnEnable()
    {
        if (shopManager != null) shopManager.OnSellerEvent += Handle;
        if (cardShopManager != null) cardShopManager.OnSellerEvent += Handle;

        SetMood(normalSprite);
        ClearText();
        lastSpeakTime = Time.unscaledTime;
        Handle(SellerEvent.Greeting);

        if (idleHintInterval > 0f) StartCoroutine(IdleLoop());
    }

    void OnDisable()
    {
        if (shopManager != null) shopManager.OnSellerEvent -= Handle;
        if (cardShopManager != null) cardShopManager.OnSellerEvent -= Handle;

        StopAllCoroutines();
        speakRoutine = null;
        moodRoutine = null;
        speaking = false;
    }

    // Можна викликати і руками з будь-якого скрипта: seller.Say("Свій текст");
    public void Say(string text)
    {
        if (speechText == null || string.IsNullOrEmpty(text)) return;

        lastPhrase = text; // зберігаємо оригінал, щоб RandomPhrase правильно порівнював
        lastSpeakTime = Time.unscaledTime;

        if (textPanelImage != null) textPanelImage.gameObject.SetActive(true);

        string shown = allCaps ? text.ToUpperInvariant() : text;

        if (speakRoutine != null) StopCoroutine(speakRoutine);
        speakRoutine = StartCoroutine(SpeakRoutine(shown));
    }

    private void Handle(SellerEvent e)
    {
        string phrase = RandomPhrase(GetPhrases(e));

        // Порожній список -> продавець мовчить (фраза не змінюється)
        if (!string.IsNullOrEmpty(phrase)) Say(phrase);

        switch (e)
        {
            case SellerEvent.Purchased:
            case SellerEvent.GoodCard:
                ShowMood(happySprite);
                break;
            case SellerEvent.NotEnoughCoins:
            case SellerEvent.NotEnoughHearts:
            case SellerEvent.AlreadySold:
            case SellerEvent.BadCard:
                ShowMood(sadSprite);
                break;
        }
    }

    private string[] GetPhrases(SellerEvent e)
    {
        switch (e)
        {
            case SellerEvent.Greeting: return greeting;
            case SellerEvent.Purchased: return purchased;
            case SellerEvent.NotEnoughCoins: return notEnoughCoins;
            case SellerEvent.NotEnoughHearts: return notEnoughHearts;
            case SellerEvent.AlreadySold: return alreadySold;
            case SellerEvent.Paid: return paid;
            case SellerEvent.PayFirst: return payFirst;
            case SellerEvent.GoodCard: return goodCard;
            case SellerEvent.BadCard: return badCard;
            default: return null;
        }
    }

    // Випадкова фраза, але не та сама, що щойно була (якщо є з чого обирати)
    private string RandomPhrase(string[] phrases)
    {
        if (phrases == null || phrases.Length == 0) return null;
        if (phrases.Length == 1) return phrases[0];

        string result;
        int guard = 0;
        do
        {
            result = phrases[Random.Range(0, phrases.Length)];
            guard++;
        } while (result == lastPhrase && guard < 10);
        return result;
    }

    private void ClearText()
    {
        if (speechText != null) speechText.text = "";
        if (hidePanelWhenEmpty && textPanelImage != null) textPanelImage.gameObject.SetActive(false);
    }

    private IEnumerator SpeakRoutine(string text)
    {
        speaking = true;
        speechText.text = text;

        if (typewriter && charDelay > 0f)
        {
            speechText.maxVisibleCharacters = 0;
            speechText.ForceMeshUpdate();
            int total = speechText.textInfo.characterCount;

            for (int i = 1; i <= total; i++)
            {
                speechText.maxVisibleCharacters = i;
                yield return new WaitForSecondsRealtime(charDelay);
            }
        }

        speechText.maxVisibleCharacters = 99999;
        speaking = false;
        lastSpeakTime = Time.unscaledTime;
        speakRoutine = null;
    }

    // Раз на секунду перевіряє: якщо продавець давно мовчить - каже підказку зі списку idleHints
    private IEnumerator IdleLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (speaking) continue;
            if (Time.unscaledTime - lastSpeakTime < idleHintInterval) continue;

            string phrase = RandomPhrase(idleHints);
            if (!string.IsNullOrEmpty(phrase)) Say(phrase);
            else lastSpeakTime = Time.unscaledTime; // нема підказок - не перевіряємо щосекунди
        }
    }

    // --- Емоції (зміна спрайта портрета) ---

    private void ShowMood(Sprite mood)
    {
        if (mood == null) return;
        if (moodRoutine != null) StopCoroutine(moodRoutine);
        moodRoutine = StartCoroutine(MoodRoutine(mood));
    }

    private IEnumerator MoodRoutine(Sprite mood)
    {
        SetMood(mood);
        yield return new WaitForSecondsRealtime(moodDuration);
        SetMood(normalSprite);
        moodRoutine = null;
    }

    private void SetMood(Sprite sprite)
    {
        if (portraitImage != null && sprite != null) portraitImage.sprite = sprite;
    }
}