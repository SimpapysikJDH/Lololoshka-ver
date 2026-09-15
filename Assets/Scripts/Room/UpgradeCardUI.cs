using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Причепи на панель Canvas, яка з'являється при виборі прокачки біля статуї.
//
// Структура ієрархії в Canvas:
// UpgradePanel (цей скрипт, вимикається автоматично в Awake)
//   └─ CardsContainer (Horizontal Layout Group)
//        └─ Card_0  (Image + Button - сама картинка кнопки і є іконкою прокачки)
//        └─ Card_1  (те саме)
//        └─ Card_2  (те саме)
//
// Ніяких дочірніх об'єктів всередині карток не треба - спрайт кнопки (Image.sprite)
// підміняється напряму на option.icon з UpgradeOption.
//
// Зроби наперед стільки карток-слотів, скільки максимум карток хоче показувати статуя
// (значення cardsToOffer в UpgradeStatue, зазвичай 3), і перетягни їх по порядку в cardSlots нижче.
// Зайві слоти (якщо карток запропоновано менше) скрипт сам ховає.
public class UpgradeCardUI : MonoBehaviour
{
    [Serializable]
    public class CardSlot
    {
        public GameObject root;   // весь GameObject картки (вмикається/вимикається)
        public Button button;     // Image на цьому ж об'єкті буде замінена на іконку прокачки
    }

    [Header("Слоти карток (перетягни заздалегідь створені картки з Canvas)")]
    public List<CardSlot> cardSlots = new List<CardSlot>();

    [Header("Корінь усієї панелі (якщо порожньо - береться цей GameObject)")]
    public GameObject panelRoot;

    private Action<UpgradeOption> onChosenCallback;

    void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        panelRoot.SetActive(false);
    }

    // Викликається зі статуї: показує картки і чекає, поки гравець клацне одну з них
    public void ShowCards(List<UpgradeOption> options, Action<UpgradeOption> onChosen)
    {
        onChosenCallback = onChosen;
        panelRoot.SetActive(true);

        for (int i = 0; i < cardSlots.Count; i++)
        {
            CardSlot slot = cardSlots[i];
            if (slot.root == null) continue;

            if (i >= options.Count)
            {
                // Ховаємо зайві слоти, якщо карток пропонується менше, ніж є слотів у UI
                slot.root.SetActive(false);
                continue;
            }

            slot.root.SetActive(true);
            UpgradeOption option = options[i];

            if (slot.button != null)
            {
                // Сам спрайт кнопки і є іконкою прокачки - окремий Image не потрібен
                Image buttonImage = slot.button.GetComponent<Image>();
                if (buttonImage != null && option.icon != null)
                    buttonImage.sprite = option.icon;

                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() => ChooseCard(option));
            }
        }
    }

    void ChooseCard(UpgradeOption chosen)
    {
        panelRoot.SetActive(false);
        onChosenCallback?.Invoke(chosen);
        onChosenCallback = null;
    }
}