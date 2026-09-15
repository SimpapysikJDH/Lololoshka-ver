using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ArtifactRoomSpawner : MonoBehaviour
{
    [Header("Пул префабів артефактів (із папки Project)")]
    public GameObject[] itemPrefabs;

    [Header("Точки спавну в кімнаті (дочірні об'єкти)")]
    public Transform[] spawnPoints;

    [Header("Кількість предметів")]
    [Range(1, 10)] public int minCount = 1;
    [Range(1, 10)] public int maxCount = 3;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private bool hasSpawned = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hasSpawned && other.CompareTag("Player"))
        {
            Debug.Log($"[{name}] Гравець зайшов у кімнату артефактів! Запускаємо спавн...");
            SpawnArtifacts();
        }
    }

    public void SpawnArtifacts()
    {
        if (hasSpawned) return;

        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogError($"[{name}] ПОМИЛКА: Масив Item Prefabs порожній у Префабі кімнати!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"[{name}] ПОМИЛКА: Масив Spawn Points порожній у Префабі кімнати!");
            return;
        }

        hasSpawned = true;

        List<GameObject> pool = new List<GameObject>(itemPrefabs);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int targetCount = Random.Range(minCount, maxCount + 1);
        int finalCount = Mathf.Min(targetCount, pool.Count, spawnPoints.Length);

        for (int i = 0; i < finalCount; i++)
        {
            GameObject obj = Instantiate(pool[i], spawnPoints[i].position, Quaternion.identity, transform);
            spawnedItems.Add(obj);

            ArtifactPickup pickup = obj.GetComponent<ArtifactPickup>();
            if (pickup != null)
            {
                pickup.Setup(this);
            }
            else
            {
                Debug.LogWarning($"[{name}] На префабі {pool[i].name} немає компонента ArtifactPickup!");
            }
        }

        Debug.Log($"[{name}] Успішно заспавнено {finalCount} артефактів!");
    }

    public void OnItemPicked(GameObject pickedItem)
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null && item != pickedItem)
            {
                Destroy(item);
            }
        }
        spawnedItems.Clear();
    }
}