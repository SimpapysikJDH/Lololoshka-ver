using UnityEngine;
using UnityEngine.InputSystem;

// Причепи на порожній GameObject з Collider2D (Is Trigger = true) в кімнаті магазину.
// Показує підказку "Натисни E, щоб відкрити магазин", поки гравець стоїть у зоні.
[RequireComponent(typeof(Collider2D))]
public class ShopZoneTrigger : MonoBehaviour
{
    public ShopUI shopUI;
    public GameObject promptHint; // напис-підказка над магазином, вмикається/вимикається автоматично

    private bool playerInside = false;

    void Update()
    {
        if (!playerInside) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (shopUI.panelRoot != null && shopUI.panelRoot.activeSelf)
                shopUI.Close();
            else
                shopUI.Open();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        if (promptHint != null) promptHint.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        if (promptHint != null) promptHint.SetActive(false);
        shopUI.Close();
    }
}
