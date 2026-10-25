using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Меню налаштувань: два повзунки гучності (загальна і музика).
/// Повісьте на панель налаштувань (або будь-який об'єкт у Canvas)
/// і призначте поля в Inspector.
/// Кнопки "Налаштування" і "Закрити" підключаються до методів Open() / Close().
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Панель налаштувань")]
    [Tooltip("Об'єкт-панель, який показується/ховається")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Повзунки")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;

    [Header("Старт")]
    [Tooltip("Ховати панель при запуску сцени")]
    [SerializeField] private bool hideOnStart = true;

    private void Start()
    {
        SetupSlider(masterSlider);
        SetupSlider(musicSlider);

        MusicManager manager = MusicManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("SettingsMenu: у сцені немає MusicManager — повзунки не працюватимуть.");
        }
        else
        {
            // Показуємо збережені значення, не викликаючи події
            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(manager.MasterVolume);
                masterSlider.onValueChanged.AddListener(manager.SetMasterVolume);
            }

            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(manager.MusicVolume);
                musicSlider.onValueChanged.AddListener(manager.SetMusicVolume);
            }
        }

        if (settingsPanel != null && hideOnStart)
            settingsPanel.SetActive(false);
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    // Ці методи підключаються до кнопок (OnClick)
    public void Open()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void Close()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Toggle()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}
