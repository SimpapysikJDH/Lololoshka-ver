using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SimpleArtifactPickup : MonoBehaviour
{
    private ArtifactRoomSpawner spawner;
    private bool picked = false;

    public void Setup(ArtifactRoomSpawner roomSpawner)
    {
        spawner = roomSpawner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (picked || !other.CompareTag("Player")) return;

        picked = true;

        // Повідомляємо спавнер, що цей предмет вибрано (щоб він знищив решту)
        if (spawner != null)
        {
            spawner.OnItemPicked(gameObject);
        }

        Destroy(gameObject);
    }
}