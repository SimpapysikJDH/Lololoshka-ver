using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Причепи на префаб одного слота товару в магазині (5 екземплярів під ShopUI.slots).
public class ShopSlotUI : MonoBehaviour
{
    [Header("Візуал")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descriptionText;

    [Header("Монетки")]
    public GameObject coinPriceGroup;   // контейнер з іконкою монетки + ціною - вимикається, якщо coinPrice = 0
    public TMP_Text coinPriceText;
    public Button buyWithCoinsButton;

    [Header("Серце (життя)")]
    public GameObject heartPriceGroup;  // контейнер з іконкою серця + ціною - вимикається, якщо heartCost = 0
    public TMP_Text heartPriceText;
    public Button buyWithHeartButton;

    [Header("Продано")]
    public GameObject soldOverlay;      // напис "ПРОДАНО" / затемнення поверх іконки

    private ShopManager shopManager;
    private int slotIndex;

    public void Setup(ShopOffer offer, int index, ShopManager manager)
    {
        shopManager = manager;
        slotIndex = index;

        if (offer == null || offer.artifact == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (icon != null) icon.sprite = offer.artifact.icon;
        if (nameText != null) nameText.text = offer.artifact.artifactName;
        if (descriptionText != null) descriptionText.text = offer.artifact.description;

        bool canBuyWithCoins = offer.coinPrice > 0;
        if (coinPriceGroup != null) coinPriceGroup.SetActive(canBuyWithCoins);
        if (canBuyWithCoins && coinPriceText != null) coinPriceText.text = offer.coinPrice.ToString();

        bool canBuyWithHeart = offer.heartCost > 0;
        if (heartPriceGroup != null) heartPriceGroup.SetActive(canBuyWithHeart);
        if (canBuyWithHeart && heartPriceText != null) heartPriceText.text = offer.heartCost.ToString();

        if (buyWithCoinsButton != null)
        {
            buyWithCoinsButton.onClick.RemoveAllListeners();
            buyWithCoinsButton.onClick.AddListener(() => shopManager.TryPurchase(slotIndex, false));
        }

        if (buyWithHeartButton != null)
        {
            buyWithHeartButton.onClick.RemoveAllListeners();
            buyWithHeartButton.onClick.AddListener(() => shopManager.TryPurchase(slotIndex, true));
        }

        SetSold(manager.IsSlotSold(index));
    }

    public void SetSold(bool sold)
    {
        if (soldOverlay != null) soldOverlay.SetActive(sold);
        if (buyWithCoinsButton != null) buyWithCoinsButton.interactable = !sold;
        if (buyWithHeartButton != null) buyWithHeartButton.interactable = !sold;
    }
}
