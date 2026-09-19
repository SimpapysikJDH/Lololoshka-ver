using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;


public class InventoryUI : MonoBehaviour
{
    [Header("Гравець")]
    [Tooltip("Якщо не задано - скрипт сам знайде ArtifactInventory на сцені при старті")]
    public ArtifactInventory inventory;

    [Header("Клавіша відкриття/закриття інвентаря")]
    public Key toggleKey = Key.E;

    [Header("Корінь панелі (об'єкт, що вмикається/вимикається - НЕ той, де висить цей скрипт)")]
    public GameObject panelRoot;

    [Header("Сітка слотів")]
    public Transform slotsGrid;      // порожній об'єкт з Grid Layout Group, куди спавляться слоти
    public GameObject slotPrefab;    // префаб одного слоту (Image + ArtifactSlotUI)

    [Header("Панель інформації про артефакт (показується при наведенні на слот)")]
    public GameObject infoPanel;
    public Image infoIcon;
    public TMP_Text infoNameText;
    public TMP_Text infoDescriptionText;

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();
    private bool isOpen = false;

    void Awake()
    {
        if (inventory == null)
            inventory = FindObjectOfType<ArtifactInventory>();

        if (panelRoot != null)
            panelRoot.SetActive(false); // інвентар стартово закритий, незалежно від стану в Editor'і

        isOpen = false;
    }

    void OnEnable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged += RefreshSlots;
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshSlots;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            Toggle();
    }

    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        isOpen = true;
        HideInfo();
        RefreshSlots();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        isOpen = false;
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    // Перебудовує сітку слотів з нуля - викликається і при кожному підборі артефакту
    // (поки інвентар відкритий), і одразу при відкритті (Open()), щоб список був актуальний.
    private void RefreshSlots()
    {
        if (slotsGrid == null || slotPrefab == null || inventory == null) return;

        foreach (GameObject slot in spawnedSlots)
            Destroy(slot);
        spawnedSlots.Clear();

        foreach (var entry in inventory.Entries)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotsGrid);
            ArtifactSlotUI slotUI = slotObj.GetComponent<ArtifactSlotUI>();
            if (slotUI != null)
                slotUI.Setup(entry.data, entry.count, this);

            spawnedSlots.Add(slotObj);
        }
    }

    // Викликається з ArtifactSlotUI.OnPointerEnter() при наведенні миші на слот
    public void ShowInfo(ArtifactData data, int count)
    {
        if (infoPanel == null || data == null) return;

        infoPanel.SetActive(true);

        if (infoIcon != null)
        {
            infoIcon.sprite = data.icon;
            infoIcon.enabled = data.icon != null;
        }

        if (infoNameText != null)
            infoNameText.text = count > 1 ? $"{data.artifactName} x{count}" : data.artifactName;

        if (infoDescriptionText != null)
            infoDescriptionText.text = data.description;
    }

    public void HideInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}
