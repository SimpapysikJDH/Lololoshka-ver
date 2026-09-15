using System.Collections.Generic;
using UnityEngine;

// Причепи на GameObject гравця (той самий, де PlayerStats)
// Зберігає список усіх підібраних артефактів - для вікна інвентаря (по E).
// Реєстрація артефакту відбувається автоматично з ArtifactData.ApplyEffect(),
// тобто нічого додатково викликати не треба - просто щоб цей компонент був на гравці.
public class ArtifactInventory : MonoBehaviour
{
    // Один рядок в інвентарі: сам артефакт + скільки разів його підібрано
    [System.Serializable]
    public class ArtifactEntry
    {
        public ArtifactData data;
        public int count;

        public ArtifactEntry(ArtifactData data)
        {
            this.data = data;
            this.count = 1;
        }
    }

    // Список підібраних артефактів у порядку підбору (для UI інвентаря)
    private readonly List<ArtifactEntry> entries = new List<ArtifactEntry>();
    public IReadOnlyList<ArtifactEntry> Entries => entries;

    // Підписуйся на цю подію (наприклад, в InventoryUI), щоб оновити список слотів
    // щоразу, коли гравець підбирає новий артефакт (або ще один дублікат вже наявного).
    public event System.Action OnInventoryChanged;

    // Викликається з ArtifactData.ApplyEffect() одразу при підборі артефакту.
    // Якщо артефакт того ж типу (той самий ScriptableObject-асет) вже є в інвентарі -
    // просто збільшує лічильник замість того, щоб додавати другий однаковий слот.
    public void AddArtifact(ArtifactData data)
    {
        if (data == null) return;

        ArtifactEntry existing = entries.Find(e => e.data == data);
        if (existing != null)
            existing.count++;
        else
            entries.Add(new ArtifactEntry(data));

        OnInventoryChanged?.Invoke();
    }
}
