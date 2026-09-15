using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [Header("Напрямок переходу з цих дверей")]
    public Vector2Int direction = Vector2Int.up; // Виставляй в інспекторі: (0,1) для верхніх, (1,0) для правих і т.д.

    [Header("На скільки далеко телепортувати гравця")]
    public float teleportDistance = 100f; // Має збігатися з кроком сітки з Кроку 1

    [Header("Додатковий зсув, щоб гравець вийшов З дверей, а не застряг у них")]
    public float entranceOffset = 4f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Рахуємо вектор телепортації (куди летимо)
            Vector3 teleportVector = new Vector3(direction.x * teleportDistance, direction.y * teleportDistance, 0f);

            // 2. Рахуємо невеличкий зсув вперед, щоб гравець переступив поріг нової кімнати
            Vector3 stepInside = new Vector3(direction.x * entranceOffset, direction.y * entranceOffset, 0f);

            // 3. Переміщуємо гравця
            other.transform.position += teleportVector + stepInside;

            // Зверни увагу: Камера автоматично зловить гравця в новій кімнаті, 
            // оскільки їїCameraController слідує за новою кімнатою через OnTriggerEnter2D самої кімнати!
        }
    }
}
