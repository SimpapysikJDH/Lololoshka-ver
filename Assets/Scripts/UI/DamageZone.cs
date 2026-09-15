using UnityEngine;

// Причепи на GameObject блоку/пастки, що завдає шкоди
// Collider2D на цьому об'єкті має бути Is Trigger = true
public class DamageZone : MonoBehaviour
{
    [Header("Скільки шкоди завдає (2 = повне серце)")]
    public int damageAmount = 2;

    // OnTriggerStay2D викликається кожен фізичний кадр, поки гравець стоїть на блоці.
    // Сама недоторканність (invincibility) в PlayerHealth не дасть шкодити занадто часто.
    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
        }
    }
}
