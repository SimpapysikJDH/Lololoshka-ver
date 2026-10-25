using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Менеджер музики для всієї гри.
///  - не знищується при зміні сцени (грає безперервно);
///  - для кожної сцени можна задати свою музику;
///  - плавно перемикає треки (crossfade);
///  - зберігає гучність (Master і Music) між запусками гри.
///
/// Покладіть на порожній об'єкт "MusicManager" (краще зробити з нього Prefab
/// і покласти в кожну сцену — дублікати самі себе знищать).
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Serializable]
    public class SceneMusic
    {
        [Tooltip("Точна назва сцени")]
        public string sceneName;
        [Tooltip("Музика для цієї сцени. Порожньо = у цій сцені тиша")]
        public AudioClip clip;
    }

    public static MusicManager Instance { get; private set; }

    [Header("Музика по сценах")]
    [SerializeField] private List<SceneMusic> sceneMusic = new List<SceneMusic>();

    [Tooltip("Музика для сцен, яких немає в списку вище (можна лишити порожнім)")]
    [SerializeField] private AudioClip defaultMusic;

    [Header("Перехід між треками")]
    [Tooltip("Скільки секунд триває плавна зміна музики")]
    [SerializeField] private float crossfadeTime = 1.5f;

    private const string MasterKey = "MasterVolume";
    private const string MusicKey = "MusicVolume";

    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }

    private readonly AudioSource[] sources = new AudioSource[2];
    private readonly float[] fades = new float[2];
    private readonly float[] fadeTargets = new float[2];
    private int current;

    private void Awake()
    {
        // Якщо менеджер уже існує (перейшли в сцену, де теж є менеджер) — цей зайвий
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.7f);
        AudioListener.volume = MasterVolume;

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].loop = true;
            sources[i].spatialBlend = 0f; // 2D-звук
            sources[i].volume = 0f;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Перша сцена: запускаємо її музику
        if (Instance == this)
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeTime);

        for (int i = 0; i < sources.Length; i++)
        {
            fades[i] = Mathf.MoveTowards(fades[i], fadeTargets[i], step);
            sources[i].volume = fades[i] * MusicVolume;

            if (fades[i] <= 0f && fadeTargets[i] <= 0f && sources[i].isPlaying)
                sources[i].Stop();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip clip = defaultMusic;

        foreach (SceneMusic entry in sceneMusic)
        {
            if (entry.sceneName == scene.name)
            {
                clip = entry.clip; // може бути null = тиша
                break;
            }
        }

        PlayMusic(clip);
    }

    /// <summary>Плавно перемикає на новий трек (null = плавно вимкнути музику).</summary>
    public void PlayMusic(AudioClip clip)
    {
        // Той самий трек уже грає — не перезапускаємо
        if (clip != null && sources[current].clip == clip &&
            sources[current].isPlaying && fadeTargets[current] > 0f)
            return;

        // Старий трек затухає
        fadeTargets[current] = 0f;

        if (clip == null) return;

        // Новий трек наростає на іншому джерелі
        current = 1 - current;
        sources[current].clip = clip;
        sources[current].loop = true;
        fades[current] = 0f;
        fadeTargets[current] = 1f;
        sources[current].Play();
    }

    /// <summary>Плавно вимкнути музику (наприклад, на час катсцени).</summary>
    public void FadeOut()
    {
        PlayMusic(null);
    }

    // ---------- Налаштування гучності ----------

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.Save();
    }
}
