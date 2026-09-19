using UnityEngine;
using UnityEngine.UI;

// Причепи на UI об'єкт хп-бару боса (класичний бар зверху екрана, Canvas -> BossHealthBar)
// Потрібен Slider (Min=0, Max=1) з заповненням через Fill Area/Fill
public class BossHealthBarUI : MonoBehaviour
{
    [Header("Посилання")]
    [Tooltip("Признач сюди MonsterHealth боса (компонент на GameObject боса)")]
    public MonsterHealth bossHealth;

    public Slider healthSlider;

    [Tooltip("Опційно: назва боса зверху бару")]
    public Text bossNameText;
    public string bossName = "Бос";

    [Tooltip("Весь GameObject бару - ховається автоматично, коли бос помирає")]
    public GameObject barRoot;

    void OnEnable()
    {
        if (bossHealth != null)
            bossHealth.OnHealthChanged += HandleHealthChanged;
    }

    void OnDisable()
    {
        if (bossHealth != null)
            bossHealth.OnHealthChanged -= HandleHealthChanged;
    }

    void Start()
    {
        if (bossNameText != null)
            bossNameText.text = bossName;

        if (bossHealth != null)
            HandleHealthChanged(bossHealth.CurrentHP, bossHealth.MaxHP);
    }

    void HandleHealthChanged(float current, float max)
    {
        if (healthSlider != null && max > 0f)
            healthSlider.value = current / max;

        if (current <= 0f && barRoot != null)
            barRoot.SetActive(false);
    }
}
