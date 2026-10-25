using UnityEngine;

// Причепи на кореневий об'єкт панелі магазину (Canvas -> ShopPanel). Підписується на ShopManager
// і перемальовує слоти щоразу, як асортимент або статус покупок змінюється.
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
            Refresh(); // якщо асортимент уже згенерувався раніше - підхопимо його зараз
        }
    }

    void OnDisable()
    {
        if (shopManager != null)
            shopManager.OnShopUpdated -= Refresh;
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
}