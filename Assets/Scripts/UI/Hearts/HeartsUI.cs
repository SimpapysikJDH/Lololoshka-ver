using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Причепи на порожній GameObject в Canvas (наприклад "HeartsUI")
public class HeartsUI : MonoBehaviour
{
    [Header("Посилання")]
    public PlayerHealth playerHealth; // перетягни сюди GameObject гравця

    [Header("Один спільний контейнер для всіх іконок")]
    public Transform heartsContainer; // об'єкт з Grid Layout Group (переносить рядки сам)

    [Header("Префаби іконок")]
    public GameObject heartIconPrefab;       // звичайне серце
    public GameObject goldenHeartIconPrefab; // золоте серце

    [Header("Спрайти звичайних сердець")]
    public Sprite fullHeartSprite;
    public Sprite halfHeartSprite;
    public Sprite emptyHeartSprite; // темне серце

    private readonly List<Image> heartIcons = new List<Image>();
    private readonly List<GameObject> goldenHeartIcons = new List<GameObject>();

    void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += UpdateHearts;
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHearts;
    }

    void Start()
    {
        if (playerHealth != null)
            UpdateHearts(playerHealth.currentHP, playerHealth.maxHearts, playerHealth.goldenHeartCount);
    }

    void UpdateHearts(int currentHP, int maxHearts, int goldenHeartCount)
    {
        while (heartIcons.Count < maxHearts)
        {
            GameObject newIcon = Instantiate(heartIconPrefab, heartsContainer);
            newIcon.transform.localScale = Vector3.one; // страховка від зміщеного масштабу префабу
            Image img = newIcon.GetComponent<Image>();
            newIcon.transform.SetSiblingIndex(heartIcons.Count);
            heartIcons.Add(img);
        }
        while (heartIcons.Count > maxHearts)
        {
            int lastIndex = heartIcons.Count - 1;
            Destroy(heartIcons[lastIndex].gameObject);
            heartIcons.RemoveAt(lastIndex);
        }

        for (int i = 0; i < heartIcons.Count; i++)
        {
            int heartValue = currentHP - (i * 2);

            if (heartValue >= 2)
                heartIcons[i].sprite = fullHeartSprite;
            else if (heartValue == 1)
                heartIcons[i].sprite = halfHeartSprite;
            else
                heartIcons[i].sprite = emptyHeartSprite;
        }

        while (goldenHeartIcons.Count < goldenHeartCount)
        {
            GameObject newIcon = Instantiate(goldenHeartIconPrefab, heartsContainer);
            newIcon.transform.localScale = Vector3.one; // страховка від зміщеного масштабу префабу
            newIcon.transform.SetAsLastSibling();
            goldenHeartIcons.Add(newIcon);
        }
        while (goldenHeartIcons.Count > goldenHeartCount)
        {
            int lastIndex = goldenHeartIcons.Count - 1;
            Destroy(goldenHeartIcons[lastIndex]);
            goldenHeartIcons.RemoveAt(lastIndex);
        }
    }
}