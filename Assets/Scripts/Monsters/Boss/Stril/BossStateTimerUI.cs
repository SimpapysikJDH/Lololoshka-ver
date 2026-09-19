using UnityEngine;
using UnityEngine.UI;

// Причепи на UI об'єкт "стану боса" - показує назву поточної атаки,
// а під час перепочинку (після attacksBeforeRest атак) - таймер до наступної серії атак
public class BossStateTimerUI : MonoBehaviour
{
    [Header("Посилання")]
    [Tooltip("Признач сюди BossController боса")]
    public BossController boss;

    [Header("Назва поточної атаки (опційно)")]
    public Text attackNameText;

    [Header("Таймер перепочинку")]
    [Tooltip("Об'єкт з текстом/баром перепочинку - вмикається лише поки бос відпочиває")]
    public GameObject restTimerRoot;
    public Text restTimerText;
    public Slider restTimerSlider;

    private float restDuration;

    void OnEnable()
    {
        if (boss != null)
        {
            boss.OnAttackStarted += HandleAttackStarted;
            boss.OnRestStateChanged += HandleRestStateChanged;
        }
    }

    void OnDisable()
    {
        if (boss != null)
        {
            boss.OnAttackStarted -= HandleAttackStarted;
            boss.OnRestStateChanged -= HandleRestStateChanged;
        }
    }

    void Start()
    {
        if (restTimerRoot != null)
            restTimerRoot.SetActive(false);
    }

    void HandleAttackStarted(string attackName)
    {
        if (attackNameText != null)
            attackNameText.text = attackName;
    }

    void HandleRestStateChanged(bool isResting, float duration)
    {
        restDuration = duration;

        if (restTimerRoot != null)
            restTimerRoot.SetActive(isResting);

        if (isResting && attackNameText != null)
            attackNameText.text = "Перепочинок...";
    }

    void Update()
    {
        if (boss == null || !boss.IsResting) return;

        if (restTimerText != null)
            restTimerText.text = Mathf.CeilToInt(boss.RestTimeRemaining).ToString();

        if (restTimerSlider != null && restDuration > 0f)
            restTimerSlider.value = boss.RestTimeRemaining / restDuration;
    }
}
