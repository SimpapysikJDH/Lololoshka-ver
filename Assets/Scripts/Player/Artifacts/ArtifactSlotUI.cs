using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Причепи на префаб одного слоту інвентаря (Image - фон/рамка слоту, потрібен Raycast
// Target увімкнений на цьому Image, інакше наведення миші не спрацює).
// Дочірні об'єкти всередині префабу:
//   └─ Icon        (Image)                            <- тягнеш у поле iconImage нижче
//   └─ CountText   (TextMeshPro - Text, необов'язково) <- тягнеш у поле countText, показує "x2", "x3" і т.д.
public class ArtifactSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Іконка артефакту")]
    public Image iconImage;

    [Header("Текст кількості (якщо артефакт підібрано декілька разів)")]
    public TMP_Text countText;

    private ArtifactData data;
    private int count;
    private InventoryUI inventoryUI;

    // Викликається з InventoryUI.RefreshSlots() одразу після Instantiate цього слоту
    public void Setup(ArtifactData artifactData, int artifactCount, InventoryUI ui)
    {
        data = artifactData;
        count = artifactCount;
        inventoryUI = ui;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (countText != null)
            countText.text = count > 1 ? $"x{count}" : string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryUI != null && data != null)
            inventoryUI.ShowInfo(data, count);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI != null)
            inventoryUI.HideInfo();
    }
}
