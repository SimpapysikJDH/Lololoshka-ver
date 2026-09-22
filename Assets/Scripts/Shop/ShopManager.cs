using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

// Причепи на порожній GameObject в кімнаті магазину (один на кімнату, з PhotonView, Scene Object).
// Асортимент СПІЛЬНИЙ на всіх гравців - генерується один раз на MasterClient і розсилається
// всім через RPC. Хто перший купив слот - той забрав, решта гравців бачать "Продано".
// UI (ShopUI) підписується на OnShopUpdated і сама перемальовує панель.
[RequireComponent(typeof(PhotonView))]
public class ShopManager : MonoBehaviourPun
{
    [Header("Каталог товарів (весь пул - і гарантовані, і випадкові разом)")]
    public List<ShopOffer> shopPool = new List<ShopOffer>();

    [Header("Скільки товарів показувати одночасно")]
    public int totalSlots = 5;

    // Поточний асортимент зберігаємо як індекси в shopPool (щоб передавати по мережі простими int,
    // самі ShopOffer/ArtifactData по RPC не передаються)
    private int[] currentOfferIndices;
    private bool[] slotSold;

    // Підписуйся тут (ShopUI), щоб перемальовувати панель щоразу, як асортимент/статус слотів змінюється
    public event System.Action OnShopUpdated;

    // Викликається, коли гравцю відмовили в покупці (слот уже забрав хтось інший, поки летів запит)
    public event System.Action<int> OnPurchaseDenied;

    // Для віконця продавця (SellerPanelUI): продавець реагує фразою на покупку/відмову/нестачу грошей
    public event System.Action<SellerEvent> OnSellerEvent;

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            GenerateShop();
        }
        else
        {
            // Не-мастер (в т.ч. гравець, що зайшов пізніше) - запитуємо поточний стан у мастера
            photonView.RPC(nameof(RPC_RequestShopState), RpcTarget.MasterClient);
        }
    }

    // --- Генерація асортименту (виконує лише MasterClient) ---

    public void GenerateShop()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        List<ShopOffer> chosen = PickOffers();
        currentOfferIndices = new int[totalSlots];
        slotSold = new bool[totalSlots];

        for (int i = 0; i < totalSlots; i++)
            currentOfferIndices[i] = (i < chosen.Count) ? shopPool.IndexOf(chosen[i]) : -1;

        photonView.RPC(nameof(RPC_SyncShopState), RpcTarget.All, currentOfferIndices, slotSold);
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

    // --- Синхронізація стану між клієнтами ---

    [PunRPC]
    private void RPC_SyncShopState(int[] offerIndices, bool[] sold)
    {
        currentOfferIndices = offerIndices;
        slotSold = sold;
        OnShopUpdated?.Invoke();
    }

    [PunRPC]
    private void RPC_RequestShopState(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Мастер ще не встиг згенерувати асортимент (запит прийшов раніше за Start()) -
        // просто ігноруємо, гравець отримає стан трохи пізніше звичайним RPC_SyncShopState
        if (currentOfferIndices == null) return;

        photonView.RPC(nameof(RPC_SyncShopState), info.Sender, currentOfferIndices, slotSold);
    }

    // --- Покупка ---

    // Викликай це з UI при натисканні кнопки купівлі. useHearts = true, якщо гравець платить життям
    public void TryPurchase(int slotIndex, bool useHearts)
    {
        Debug.Log($"[Shop] TryPurchase викликано, slot={slotIndex}, useHearts={useHearts}");

        if (currentOfferIndices == null || slotIndex < 0 || slotIndex >= totalSlots)
        {
            Debug.Log("[Shop] СТОП: currentOfferIndices == null або невірний slotIndex");
            return;
        }
        if (slotSold[slotIndex])
        {
            Debug.Log("[Shop] СТОП: слот вже проданий");
            return;
        }

        ShopOffer offer = GetOffer(slotIndex);
        if (offer == null)
        {
            Debug.Log("[Shop] СТОП: offer == null (GetOffer повернув null)");
            return;
        }

        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null)
        {
            Debug.Log("[Shop] СТОП: localPlayer == null (не знайдено об'єкт з тегом Player)");
            return;
        }

        if (useHearts)
        {
            PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
            if (health == null || offer.heartCost <= 0)
            {
                Debug.Log("[Shop] СТОП: health == null або heartCost <= 0");
                return;
            }
            if (health.maxHearts * 2 - offer.heartCost < 2)
            {
                Debug.Log("[Shop] СТОП: недостатньо сердець");
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughHearts);
                return;
            }
        }
        else
        {
            PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
            if (stats == null || offer.coinPrice <= 0)
            {
                Debug.Log("[Shop] СТОП: stats == null або coinPrice <= 0");
                return;
            }
            if (stats.coins < offer.coinPrice)
            {
                Debug.Log($"[Shop] СТОП: не вистачає монет ({stats.coins} < {offer.coinPrice})");
                OnSellerEvent?.Invoke(SellerEvent.NotEnoughCoins);
                return;
            }
        }

        Debug.Log("[Shop] Всі перевірки пройдено, шлю RPC на MasterClient...");
        photonView.RPC(nameof(RPC_RequestPurchase), RpcTarget.MasterClient,
            slotIndex, useHearts, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RPC_RequestPurchase(int slotIndex, bool useHearts, int buyerActorNumber)
    {
        Debug.Log($"[Shop] RPC_RequestPurchase отримано на MasterClient, slot={slotIndex}");

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[Shop] СТОП: цей клієнт не MasterClient");
            return;
        }

        if (slotIndex < 0 || slotIndex >= totalSlots || slotSold[slotIndex])
        {
            Debug.Log("[Shop] СТОП: слот вже зайнятий/невірний - відправляю відмову");
            // Хтось встиг купити раніше, поки летів запит - повідомляємо лише покупця, що не вийшло
            Player buyer = PhotonNetwork.CurrentRoom.GetPlayer(buyerActorNumber);
            if (buyer != null)
                photonView.RPC(nameof(RPC_PurchaseDenied), buyer, slotIndex);
            return;
        }

        slotSold[slotIndex] = true;
        Debug.Log("[Shop] Покупка підтверджена, шлю RPC_ConfirmPurchase всім");
        photonView.RPC(nameof(RPC_ConfirmPurchase), RpcTarget.All, slotIndex, useHearts, buyerActorNumber);
    }

    [PunRPC]
    private void RPC_ConfirmPurchase(int slotIndex, bool useHearts, int buyerActorNumber)
    {
        slotSold[slotIndex] = true;
        OnShopUpdated?.Invoke();

        // Списання валюти й ефект артефакту застосовуються ЛИШЕ на клієнті самого покупця -
        // у кожного гравця своя локальна PlayerStats/PlayerHealth
        if (PhotonNetwork.LocalPlayer.ActorNumber != buyerActorNumber) return;

        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;

        ShopOffer offer = GetOffer(slotIndex);
        if (offer == null || offer.artifact == null) return;

        if (useHearts)
        {
            PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
            if (health != null) health.AddMaxHearts(-offer.heartCost);
        }
        else
        {
            PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
            if (stats != null) stats.SpendCoins(offer.coinPrice);
        }

        offer.artifact.ApplyEffect(localPlayer);
        OnSellerEvent?.Invoke(SellerEvent.Purchased);
    }

    [PunRPC]
    private void RPC_PurchaseDenied(int slotIndex)
    {
        OnPurchaseDenied?.Invoke(slotIndex);
        OnSellerEvent?.Invoke(SellerEvent.AlreadySold);
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

    // Знаходить GameObject ЛОКАЛЬНОГО гравця (той, чий photonView.IsMine == true).
    // Якщо мультиплеєр ще не до кінця налаштований (як зараз закоментовано в PlayerMovement.cs /
    // CoinPickup.cs) - просто поверне перший знайдений об'єкт з тегом "Player".
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