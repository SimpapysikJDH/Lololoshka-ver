using UnityEngine;

// Один товар в каталозі магазину - артефакт + ціна(и) + чи гарантований у видачі.
// Кладеться в список ShopManager.shopPool (весь каталог - і гарантовані, і випадкові товари, разом).
[System.Serializable]
public class ShopOffer
{
    public ArtifactData artifact;

    [Header("Ціна")]
    [Tooltip("Ціна в монетках. 0 = не продається за монети")]
    public int coinPrice = 10;

    [Tooltip("Ціна в половинках серця (2 = одне серце, 1 = пів серця). 0 = не продається за життя")]
    public int heartCost = 0;

    [Header("Випадання")]
    [Tooltip("Якщо true - цей товар ЗАВЖДИ буде серед товарів магазину (не бере участі у рандомі)")]
    public bool guaranteed = false;

    [Tooltip("Вага для випадкового вибору серед НЕ гарантованих товарів (більше = частіше випадає). Не враховується для гарантованих товарів.")]
    public int weight = 1;
}
