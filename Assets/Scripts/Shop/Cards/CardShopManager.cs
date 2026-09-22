using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// Один тариф оплати: скільки платиш -> скільки карток можна відкрити.
[System.Serializable]
public class CardPayTier
{
    [Tooltip("Скільки карток гравець зможе відкрити за цю оплату")]
    public int picks = 1;
    [Tooltip("Ціна в монетках. 0 = за монети недоступно")]
    public int coinPrice = 10;
    [Tooltip("Ціна в половинках серця (2 = одне серце). 0 = за життя недоступно")]
    public int heartCost = 0;
}

// Причепи на порожній GameObject в кімнаті (ЛОКАЛЬНО для кожного гравця, без Photon-синхронізації:
// у кожного гравця свої картки й свої відкриття).
// Потік: гравець відкриває панель -> бачить N карток рубашкою вгору -> платить за тариф ->
// клікає на стільки карток, скільки дає тариф -> кожна відкрита картка одразу міняє стат.
public class CardShopManager : MonoBehaviour
{
    [Header("Пул карток (сюди всі CardData - і сірі, і червоні)")]
    public List<CardData> cardPool = new List<CardData>();

    [Header("Розклад карток на столі")]
    [Min(1)] public int cardCount = 8;
    [Tooltip("Мінімум червоних (мінусових) карток серед розкладу")]
    public int minNegativeCards = 2;
    [Tooltip("Максимум червоних карток. Хочеш фіксовано 4 червоні - постав min = max = 4")]
    public int maxNegativeCards = 4;
    [Tooltip("Чи можуть в одному розкладі бути однакові картки")]
    public bool allowDuplicates = true;
    [Tooltip("true = при кожній новій оплаті всі картки перемішуються заново рубашкою вгору. false = невідкриті лишаються, розклад оновлюється лише коли все відкрито")]
    public bool reshuffleOnEachPayment = true;

    [Header("Тарифи (скільки платиш -> скільки карток відкриваєш)")]
    public List<CardPayTier> tiers = new List<CardPayTier>
    {
        new CardPayTier { picks = 1, coinPrice = 10 },
        new CardPayTier { picks = 4, coinPrice = 30 }
    };

    private CardData[] cards;
    private bool[] revealed;
    private int picksLeft;

    // Будь-яка зміна стану (новий розклад, оплата, вибір) - UI перемальовується
    public event System.Action OnStateChanged;
    // Картку щойно відкрили (index, дані) - UI програє анімацію перевороту
    public event System.Action<int, CardData> OnCardRevealed;
    // Текстове повідомлення для гравця ("Не вистачає монет" тощо)
    public event System.Action<string> OnMessage;
    // Для віконця продавця (SellerPanelUI)
    public event System.Action<SellerEvent> OnSellerEvent;

    public int CardCount => cards != null ? cards.Length : 0;
    public int PicksLeft => picksLeft;

    void Start()
    {
        Generate();
    }

    public bool IsRevealed(int index)
    {
        return revealed != null && index >= 0 && index < revealed.Length && revealed[index];
    }

    public CardData GetCard(int index)
    {
        if (cards == null || index < 0 || index >= cards.Length) return null;
        return cards[index];
    }

    // --- Генерація розкладу ---

    public void Generate()
    {
        int count = Mathf.Max(1, cardCount);

        List<CardData> positives = new List<CardData>();
        List<CardData> negatives = new List<CardData>();
        foreach (var c in cardPool)
        {
            if (c == null) continue;
            if (c.isNegative) negatives.Add(c); else positives.Add(c);
        }

        int minN = Mathf.Clamp(minNegativeCards, 0, count);
        int maxN = Mathf.Clamp(maxNegativeCards, minN, count);
        int negCount = Random.Range(minN, maxN + 1);

        // Захист, якщо в пулі бракує якогось типу карток
        if (negatives.Count == 0) negCount = 0;
        if (positives.Count == 0) negCount = count;

        List<CardData> result = new List<CardData>();
        result.AddRange(Draw(negatives, negCount));
        result.AddRange(Draw(positives, count - negCount));
        Shuffle(result);

        cards = result.ToArray();
        revealed = new bool[cards.Length];
        picksLeft = 0;

        OnStateChanged?.Invoke();
    }

    private List<CardData> Draw(List<CardData> source, int n)
    {
        List<CardData> res = new List<CardData>();
        if (source.Count == 0 || n <= 0) return res;

        List<CardData> bag = new List<CardData>(source);
        for (int i = 0; i < n; i++)
        {
            if (bag.Count == 0) bag.AddRange(source); // пул закінчився - дозволяємо повтори
            CardData picked = PickWeighted(bag);
            res.Add(picked);
            if (!allowDuplicates) bag.Remove(picked);
        }
        return res;
    }

    private CardData PickWeighted(List<CardData> list)
    {
        int total = 0;
        foreach (var c in list) total += Mathf.Max(1, c.weight);

        int roll = Random.Range(0, total);
        int cumulative = 0;
        foreach (var c in list)
        {
            cumulative += Mathf.Max(1, c.weight);
            if (roll < cumulative) return c;
        }
        return list[list.Count - 1];
    }

    private void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // --- Оплата ---

    // Викликай з UI. useHearts = true, якщо гравець платить життям
    public void TryPay(int tierIndex, bool useHearts)
    {
        if (cards == null) return;

        if (picksLeft > 0)
        {
            OnMessage?.Invoke("Спочатку відкрий оплачені картки!");
            OnSellerEvent?.Invoke(SellerEvent.PayFirst);
            return;
        }
        if (tierIndex < 0 || tierIndex >= tiers.Count) return;

        CardPayTier tier = tiers[tierIndex];
        GameObject player = FindLocalPlayer();
        if (player == null) return;

        if (useHearts)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null || tier.heartCost <= 0) return;
            if (health.maxHearts * 2 - tier.heartCost < 2)
            {
                OnMessage?.Invoke("Не вистачає сердець!");
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughHearts);
                return;
            }
            health.AddMaxHearts(-tier.heartCost);
        }
        else
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats == null || tier.coinPrice <= 0) return;
            if (!stats.SpendCoins(tier.coinPrice))
            {
                OnMessage?.Invoke("Не вистачає монет!");
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughCoins);
                return;
            }
        }

        // Новий розклад: або щоразу, або коли всі картки вже відкриті
        if (reshuffleOnEachPayment || UnrevealedCount() == 0)
            Generate();

        picksLeft = Mathf.Min(tier.picks, UnrevealedCount());
        OnSellerEvent?.Invoke(SellerEvent.Paid);
        OnStateChanged?.Invoke();
    }

    private int UnrevealedCount()
    {
        int n = 0;
        for (int i = 0; i < revealed.Length; i++)
            if (!revealed[i]) n++;
        return n;
    }

    // --- Вибір картки ---

    // Викликається зі слота, коли гравець клікає на картку
    public void PickCard(int index)
    {
        if (cards == null || picksLeft <= 0) return;
        if (index < 0 || index >= cards.Length || revealed[index]) return;

        GameObject player = FindLocalPlayer();
        if (player == null) return;

        revealed[index] = true;
        picksLeft--;

        cards[index].Apply(player);

        OnCardRevealed?.Invoke(index, cards[index]);
        OnSellerEvent?.Invoke(cards[index].isNegative ? SellerEvent.BadCard : SellerEvent.GoodCard);
        OnStateChanged?.Invoke();
    }

    // Той самий пошук локального гравця, що й в ShopManager
    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            PhotonView pv = p.GetComponent<PhotonView>();
            if (pv == null || pv.IsMine) return p;
        }
        return players.Length > 0 ? players[0] : null;
    }
}