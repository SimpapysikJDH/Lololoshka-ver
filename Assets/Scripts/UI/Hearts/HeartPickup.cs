using UnityEngine;

// Причепи на GameObject серця, яке лежить на карті
// Collider2D на цьому об'єкті має бути Is Trigger = true
public class HeartPickup : MonoBehaviour
{
    [Header("Скільки лікує (2 = повне серце, 1 = половина)")]
    public int healAmount = 2;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Heal(healAmount);
            Destroy(gameObject);
        }
    }
}
