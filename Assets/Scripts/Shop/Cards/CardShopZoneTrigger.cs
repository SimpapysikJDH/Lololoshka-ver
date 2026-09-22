using UnityEngine;
using UnityEngine.InputSystem;

// Причепи на порожній GameObject з Collider2D (Is Trigger = true) біля столу з картками.
// Те саме, що ShopZoneTrigger, тільки для CardShopUI. E відкриває/закриває панель.
[RequireComponent(typeof(Collider2D))]
public class CardShopZoneTrigger : MonoBehaviour
{
    public CardShopUI cardShopUI;
    public GameObject promptHint;

    private bool playerInside = false;

    void Update()
    {
        if (!playerInside) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (cardShopUI.panelRoot != null && cardShopUI.panelRoot.activeSelf)
                cardShopUI.Close();
            else
                cardShopUI.Open();
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
        cardShopUI.Close();
    }
}
