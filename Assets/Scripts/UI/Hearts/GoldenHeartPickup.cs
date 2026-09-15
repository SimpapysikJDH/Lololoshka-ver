using UnityEngine;

// Причепи на GameObject золотого серця на карті
// Collider2D на цьому об'єкті має бути Is Trigger = true
public class GoldenHeartPickup : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.AddGoldenHeart();
            Destroy(gameObject);
        }
    }
}
