using UnityEngine;
using TMPro; // якщо використовуєш звичайний UI Text замість TextMeshPro - дивись коментар нижче

// Причепи на GameObject з текстом на Canvas (наприклад "CoinsText")
// Потрібен компонент TextMeshProUGUI на цьому ж об'єкті
public class CoinUI : MonoBehaviour
{
    [Header("Текст, куди виводиться кількість монет")]
    public TextMeshProUGUI coinText;

    [Header("Формат тексту (0 - буде замінено на число)")]
    public string format = "{0}";

    private PlayerStats playerStats;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerStats = playerObj.GetComponent<PlayerStats>();
        }

        if (playerStats != null)
        {
            playerStats.OnCoinsChanged += UpdateCoinText;
            UpdateCoinText(playerStats.coins); // одразу показати поточну кількість при старті
        }
    }

    void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnCoinsChanged -= UpdateCoinText;
        }
    }

    void UpdateCoinText(int amount)
    {
        if (coinText != null)
        {
            coinText.text = string.Format(format, amount);
        }
    }
}

// ---- Якщо в тебе НЕ TextMeshPro, а звичайний UI.Text ----
// 1. Видали рядок "using TMPro;" зверху
// 2. Заміни "TextMeshProUGUI" на "UnityEngine.UI.Text" (2 місця: поле coinText і using UnityEngine.UI;)
