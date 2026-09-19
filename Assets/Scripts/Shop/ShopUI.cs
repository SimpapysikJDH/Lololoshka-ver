using UnityEngine;

// Причепи на кореневий об'єкт панелі магазину (Canvas -> ShopPanel). Підписується на ShopManager
// і перемальовує слоти щоразу, як асортимент або статус покупок змінюється (в т.ч. у інших гравців).
public class ShopUI : MonoBehaviour
{
    [Header("Посилання")]
    public ShopManager shopManager;
    public GameObject panelRoot;         // сама панель (вимикається/вмикається через Open/Close)
    public ShopSlotUI[] slots;           // по одному елементу на товар (5 штук)

    void OnEnable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopUpdated += Refresh;
            shopManager.OnPurchaseDenied += HandlePurchaseDenied;
            Refresh(); // якщо асортимент уже згенерувався раніше - підхопимо його зараз
        }
    }

    void OnDisable()
    {
        if (shopManager != null)
        {
            shopManager.OnShopUpdated -= Refresh;
            shopManager.OnPurchaseDenied -= HandlePurchaseDenied;
        }
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
        if (shopManager == null || slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            ShopOffer offer = shopManager.GetOffer(i);
            slots[i].Setup(offer, i, shopManager);
        }
    }

    // Товар уже забрав інший гравець швидше за нас (мультиплеєр) - тут можна показати
    // спливаючу підказку типу "Вже продано!" над відповідним слотом
    private void HandlePurchaseDenied(int slotIndex)
    {
        Debug.Log($"Слот {slotIndex} вже продано іншим гравцем.");
        // TODO: спливаюча підказка в UI, якщо потрібно
    }
}