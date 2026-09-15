using System;
using UnityEngine;

// Причепи на GameObject монстра
public class MonsterHealth : MonoBehaviour, IDamageable
{
    // Підписуйся на це в RoomController, щоб знати коли монстр помер
    public event Action OnDeath;

    // Якщо false - MonsterHealth сам НЕ знищить об'єкт після Die().
    // Постав false у своєму контролері (наприклад SlimeController), якщо хочеш
    // спочатку програти анімацію смерті, а знищити об'єкт вже самостійно.
    // Для решти монстрів за замовчуванням true - поведінка та ж, що й раніше.
    [HideInInspector] public bool destroyOnDeath = true;

    [Header("Монетки (префаб з CoinPickup.cs)")]
    public GameObject coinPrefab; // признач в інспекторі на префабі кожного монстра

    public bool IsDead { get; private set; } = false;

    private float currentHP;
    private MonsterData data;

    public void Initialize(MonsterData monsterData)
    {
        data = monsterData;
        currentHP = data.maxHP;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead) return; // щоб Die() не спрацював двічі, якщо шкода прийде ще раз тим самим кадром
        IsDead = true;

        DropCoins();

        OnDeath?.Invoke();
        // Тут пізніше додамо: дроп предметів, звук тощо

        if (destroyOnDeath)
            Destroy(gameObject);
    }

    // Спавнить випадкову кількість монеток навколо точки смерті монстра
    private void DropCoins()
    {
        if (coinPrefab == null || data == null) return;

        int count = UnityEngine.Random.Range(data.minCoinDrop, data.maxCoinDrop + 1);

        for (int i = 0; i < count; i++)
        {
            // Невеликий випадковий розкид, щоб монетки не спавнились одна в одній
            Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.3f;
            Vector3 spawnPos = transform.position + (Vector3)offset;

            Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        }
    }
}