using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Причепи на кореневий об'єкт панелі магазину карток (Canvas -> CardShopPanel), який ЗАВЖДИ активний.
// Сама панель (panelRoot) вмикається/вимикається через Open/Close - так само, як в ShopUI.
public class CardShopUI : MonoBehaviour
{
    [System.Serializable]
    public class TierUI
    {
        public GameObject root;
        public TMP_Text picksText;     // напр. "4 картки"
        public GameObject coinGroup;
        public Button coinButton;
        public TMP_Text coinText;
        public GameObject heartGroup;
        public Button heartButton;
        public TMP_Text heartText;
    }

    [Header("Посилання")]
    public CardShopManager manager;
    public GameObject panelRoot;
    public CardSlotUI[] slots;             // 8 карток

    [Header("Спрайти рубашки і переходу")]
    [Tooltip("Рубашка, рівна (те, що бачить гравець, поки картка закрита)")]
    public Sprite backSprite;
    [Tooltip("Кадри переходу по порядку між рубашкою і лицем. Напр. один кадр - картка боком. Можна кілька.")]
    public Sprite[] flipFrames;

    [Header("Тарифи (по одному елементу на кожен тариф з manager.tiers, в тому ж порядку)")]
    public TierUI[] tierUIs;

    [Header("Тексти")]
    public TMP_Text infoText;
    public string payPromptText = "Заплати, щоб відкрити картки";
    public string picksLeftFormat = "Відкрий карток: {0}";
    public string picksFormat = "{0} шт.";

    void Start()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].Setup(i, manager, backSprite, flipFrames);

        for (int i = 0; i < tierUIs.Length; i++)
        {
            int tierIndex = i; // копія для замикання
            TierUI t = tierUIs[i];
            if (t.coinButton != null)
            {
                t.coinButton.onClick.RemoveAllListeners();
                t.coinButton.onClick.AddListener(() => manager.TryPay(tierIndex, false));
            }
            if (t.heartButton != null)
            {
                t.heartButton.onClick.RemoveAllListeners();
                t.heartButton.onClick.AddListener(() => manager.TryPay(tierIndex, true));
            }
        }

        Refresh();
    }

    void OnEnable()
    {
        if (manager == null) return;
        manager.OnStateChanged += Refresh;
        manager.OnCardRevealed += HandleCardRevealed;
        // OnMessage більше не показуємо в infoText - реагує продавець (SellerPanelUI)
    }

    void OnDisable()
    {
        if (manager == null) return;
        manager.OnStateChanged -= Refresh;
        manager.OnCardRevealed -= HandleCardRevealed;
    }

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Refresh()
    {
        if (manager == null) return;

        bool canPick = manager.PicksLeft > 0;

        // Картки
        for (int i = 0; i < slots.Length; i++)
        {
            bool exists = i < manager.CardCount;
            slots[i].gameObject.SetActive(exists);
            if (!exists) continue;

            slots[i].Sync(manager.IsRevealed(i), manager.GetCard(i), canPick);
        }

        // Тарифи
        for (int i = 0; i < tierUIs.Length; i++)
        {
            TierUI t = tierUIs[i];
            bool exists = i < manager.tiers.Count;
            if (t.root != null) t.root.SetActive(exists);
            if (!exists) continue;

            CardPayTier tier = manager.tiers[i];
            if (t.picksText != null) t.picksText.text = string.Format(picksFormat, tier.picks);

            bool byCoins = tier.coinPrice > 0;
            if (t.coinGroup != null) t.coinGroup.SetActive(byCoins);
            if (byCoins && t.coinText != null) t.coinText.text = tier.coinPrice.ToString();

            bool byHearts = tier.heartCost > 0;
            if (t.heartGroup != null) t.heartGroup.SetActive(byHearts);
            if (byHearts && t.heartText != null) t.heartText.text = tier.heartCost.ToString();

            // Поки не відкрив оплачені картки - платити знову не можна
            if (t.coinButton != null) t.coinButton.interactable = !canPick;
            if (t.heartButton != null) t.heartButton.interactable = !canPick;
        }

        if (infoText != null)
            infoText.text = canPick ? string.Format(picksLeftFormat, manager.PicksLeft) : payPromptText;
    }

    private void HandleCardRevealed(int index, CardData data)
    {
        if (index >= 0 && index < slots.Length)
            slots[index].Flip(data);
    }

    private void HandleMessage(string message)
    {
        if (infoText != null) infoText.text = message; // зникне при наступному Refresh
    }
}