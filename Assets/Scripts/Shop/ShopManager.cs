using UnityEngine;
using System.Collections.Generic;

// Причепи на порожній GameObject в кімнаті магазину (один на кімнату).
// Асортимент генерується при вході в магазин (Start). UI (ShopUI) підписується
// на OnShopUpdated і сама перемальовує панель.
public class ShopManager : MonoBehaviour
{
    [Header("Каталог товарів (весь пул - і гарантовані, і випадкові разом)")]
    public List<ShopOffer> shopPool = new List<ShopOffer>();

    [Header("Скільки товарів показувати одночасно")]
    public int totalSlots = 5;

    // Поточний асортимент як індекси в shopPool
    private int[] currentOfferIndices;
    private bool[] slotSold;

    // Підписуйся тут (ShopUI), щоб перемальовувати панель, коли асортимент/статус слотів змінюється
    public event System.Action OnShopUpdated;

    // Для віконця продавця (SellerPanelUI): продавець реагує фразою на покупку/нестачу грошей
    public event System.Action<SellerEvent> OnSellerEvent;

    void Start()
    {
        GenerateShop();
    }

    // --- Генерація асортименту ---

    public void GenerateShop()
    {
        List<ShopOffer> chosen = PickOffers();
        currentOfferIndices = new int[totalSlots];
        slotSold = new bool[totalSlots];

        for (int i = 0; i < totalSlots; i++)
            currentOfferIndices[i] = (i < chosen.Count) ? shopPool.IndexOf(chosen[i]) : -1;

        OnShopUpdated?.Invoke();
    }

    private List<ShopOffer> PickOffers()
    {
        List<ShopOffer> result = new List<ShopOffer>();

        // 1) Гарантовані товари - завжди в наборі (в порядку, як стоять в списку каталогу)
        foreach (var offer in shopPool)
        {
            if (result.Count >= totalSlots) break;
            if (offer.guaranteed) result.Add(offer);
        }

        // 2) Решту слотів заповнюємо випадково з НЕ гарантованих товарів, з урахуванням ваги,
        // без повторів в межах одного заходу в магазин
        List<ShopOffer> pool = new List<ShopOffer>();
        foreach (var offer in shopPool)
            if (!offer.guaranteed) pool.Add(offer);

        while (result.Count < totalSlots && pool.Count > 0)
        {
            ShopOffer picked = PickWeighted(pool);
            result.Add(picked);
            pool.Remove(picked);
        }

        return result;
    }

    private ShopOffer PickWeighted(List<ShopOffer> pool)
    {
        int totalWeight = 0;
        foreach (var o in pool) totalWeight += Mathf.Max(1, o.weight);

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var o in pool)
        {
            cumulative += Mathf.Max(1, o.weight);
            if (roll < cumulative) return o;
        }
        return pool[pool.Count - 1]; // fallback, не повинно статись
    }

    // --- Покупка ---

    // Викликай з UI при натисканні кнопки купівлі. useHearts = true, якщо гравець платить життям
    public void TryPurchase(int slotIndex, bool useHearts)
    {
        if (currentOfferIndices == null || slotIndex < 0 || slotIndex >= totalSlots) return;

        if (slotSold[slotIndex])
        {
            OnSellerEvent?.Invoke(SellerEvent.AlreadySold);
            return;
        }

        ShopOffer offer = GetOffer(slotIndex);
        if (offer == null || offer.artifact == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[Shop] Не знайдено об'єкт з тегом Player");
            return;
        }

        if (useHearts)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null || offer.heartCost <= 0) return;

            // Після покупки має лишитись хоча б 1 серце
            if (health.maxHearts * 2 - offer.heartCost < 2)
            {
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughHearts);
                return;
            }

            health.AddMaxHearts(-offer.heartCost);
        }
        else
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats == null || offer.coinPrice <= 0) return;

            if (!stats.SpendCoins(offer.coinPrice)) // не вистачає монет
            {
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughCoins);
                return;
            }
        }

        slotSold[slotIndex] = true;
        offer.artifact.ApplyEffect(player);
        OnShopUpdated?.Invoke();
        OnSellerEvent?.Invoke(SellerEvent.Purchased);
    }

    // --- Допоміжне ---

    public ShopOffer GetOffer(int slotIndex)
    {
        if (currentOfferIndices == null || slotIndex < 0 || slotIndex >= currentOfferIndices.Length) return null;
        int poolIndex = currentOfferIndices[slotIndex];
        if (poolIndex < 0 || poolIndex >= shopPool.Count) return null;
        return shopPool[poolIndex];
    }

    public bool IsSlotSold(int slotIndex)
    {
        return slotSold != null && slotIndex >= 0 && slotIndex < slotSold.Length && slotSold[slotIndex];
    }
}