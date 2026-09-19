using UnityEngine;

// Причепи на КОРІНЬ об'єкта гравця (той самий, де PlayerMovement, PlayerStats, PlayerHealth).
// Робить так, щоб гравець (і всі його стати/артефакти/монетки на ньому) пережив перехід
// між сценами Level1 -> Shop -> Level2 -> Shop -> ..., замість того щоб знищитись
// разом зі старою сценою (а з ним - і весь прогрес забігу).
public class PersistentPlayer : MonoBehaviour
{
    // Захист від дублікатів: якщо в НОВІЙ сцені раптом опиниться ще один об'єкт
    // з цим скриптом (наприклад, хтось лишив гравця в самому префабі сцени
    // "про всяк випадок") - знищуємо саме його, а не того, що вже живе
    // і переніс на собі весь прогрес забігу.
    private static PersistentPlayer instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
