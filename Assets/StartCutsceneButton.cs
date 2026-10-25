using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

/// <summary>
/// Повісьте цей скрипт на порожній об'єкт (наприклад, CutsceneManager).
/// Натискання Play -> усе меню плавно зникає -> грає катсцена -> екран затемнюється -> наступна сцена.
///
/// Катсцена може бути:
///  - Timeline (PlayableDirector), або
///  - відео (VideoPlayer).
/// Достатньо заповнити одне з двох полів.
/// Панель затемнення і CanvasGroup для меню створюються автоматично.
/// </summary>
public class StartCutsceneButton : MonoBehaviour
{
    [Header("Кнопка Play")]
    [SerializeField] private Button button;

    [Header("Меню, яке плавно зникає після натискання")]
    [Tooltip("Усе, що має зникнути: кнопка Options, панель налаштувань, заголовок тощо. " +
             "Кнопку Play додавати не обов'язково. Зручно перетягнути один батьківський об'єкт з усім меню. " +
             "Неактивні об'єкти (наприклад, закрита панель налаштувань) пропускаються.")]
    [SerializeField] private GameObject[] uiToFadeOut;

    [Tooltip("Скільки секунд меню зникає")]
    [SerializeField] private float uiFadeDuration = 0.6f;

    [Tooltip("Запускати катсцену лише після того, як меню повністю зникло")]
    [SerializeField] private bool startCutsceneAfterUiFade = true;

    [Header("Катсцена (заповніть одне з двох)")]
    [SerializeField] private PlayableDirector timelineCutscene;
    [SerializeField] private VideoPlayer videoCutscene;

    [Header("Швидкість катсцени")]
    [Tooltip("1 = звичайна, 0.5 = вдвічі повільніше, 0.25 = вчетверо повільніше")]
    [Range(0.1f, 2f)]
    [SerializeField] private float playbackSpeed = 1f;

    [Header("Затемнення в кінці")]
    [Tooltip("Скільки секунд триває затемнення екрана")]
    [SerializeField] private float fadeDuration = 1.5f;
    [Tooltip("Колір, у який затемнюється екран")]
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Наступна сцена")]
    [Tooltip("Назва сцени (вона має бути додана в File > Build Settings)")]
    [SerializeField] private string nextSceneName = "Level2";

    [Header("Додатково")]
    [Tooltip("Дозволити пропустити катсцену клавішею Space")]
    [SerializeField] private bool allowSkip = true;

    private bool clicked;
    private bool cutscenePlaying;
    private bool sceneLoading;

    private CanvasGroup fadeGroup;

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>();

        if (timelineCutscene != null)
            timelineCutscene.playOnAwake = false;

        if (videoCutscene != null)
        {
            videoCutscene.playOnAwake = false;
            videoCutscene.gameObject.SetActive(false);
        }

        CreateFadeOverlay();
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }

    // Створює повноекранну панель для затемнення (спочатку повністю прозору)
    private void CreateFadeOverlay()
    {
        GameObject canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // поверх усього

        fadeGroup = canvasGO.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        GameObject imageGO = new GameObject("FadeImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        Image image = imageGO.AddComponent<Image>();
        image.color = fadeColor;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void OnButtonClicked()
    {
        if (clicked) return;
        clicked = true;

        StartCoroutine(PlaySequence());
    }

    // 1. Меню зникає -> 2. Катсцена
    private IEnumerator PlaySequence()
    {
        if (!startCutsceneAfterUiFade)
            StartCutscene();

        yield return FadeOutMenu();

        if (startCutsceneAfterUiFade)
            StartCutscene();
    }

    private IEnumerator FadeOutMenu()
    {
        // Збираємо всі об'єкти меню без повторів (кнопка Play теж зникає)
        HashSet<GameObject> objects = new HashSet<GameObject>();

        if (button != null)
            objects.Add(button.gameObject);

        if (uiToFadeOut != null)
        {
            foreach (GameObject go in uiToFadeOut)
                if (go != null) objects.Add(go);
        }

        List<GameObject> active = new List<GameObject>();
        List<CanvasGroup> groups = new List<CanvasGroup>();

        foreach (GameObject go in objects)
        {
            if (!go.activeInHierarchy) continue; // закриті панелі пропускаємо

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            // Щоб під час зникання нічого не натискалося
            cg.interactable = false;
            cg.blocksRaycasts = false;

            active.Add(go);
            groups.Add(cg);
        }

        float t = 0f;
        float duration = Mathf.Max(0.01f, uiFadeDuration);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);

            foreach (CanvasGroup cg in groups)
                cg.alpha = k;

            yield return null;
        }

        foreach (CanvasGroup cg in groups)
            cg.alpha = 0f;

        foreach (GameObject go in active)
            go.SetActive(false);
    }

    private void StartCutscene()
    {
        if (sceneLoading) return; // якщо вже пропустили — нічого не запускаємо

        cutscenePlaying = true;

        if (timelineCutscene != null)
        {
            timelineCutscene.stopped += OnTimelineStopped;
            timelineCutscene.Play();

            // Швидкість Timeline можна міняти лише після Play(), коли створено граф
            if (timelineCutscene.playableGraph.IsValid())
                timelineCutscene.playableGraph.GetRootPlayable(0).SetSpeed(playbackSpeed);
        }
        else if (videoCutscene != null)
        {
            videoCutscene.gameObject.SetActive(true);
            videoCutscene.loopPointReached += OnVideoFinished;

            if (videoCutscene.canSetPlaybackSpeed)
                videoCutscene.playbackSpeed = playbackSpeed;

            videoCutscene.Play();
        }
        else
        {
            Debug.LogWarning("Катсцену не призначено — одразу затемнюємо екран і йдемо далі.");
            StartFadeAndLoad();
        }
    }

    private void Update()
    {
        if (allowSkip && cutscenePlaying && !sceneLoading && Input.GetKeyDown(KeyCode.Space))
            StartFadeAndLoad();
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        director.stopped -= OnTimelineStopped;
        StartFadeAndLoad();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        vp.loopPointReached -= OnVideoFinished;
        StartFadeAndLoad();
    }

    // 3. Затемнення + перехід на наступну сцену
    private void StartFadeAndLoad()
    {
        if (sceneLoading) return;
        sceneLoading = true;
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        fadeGroup.blocksRaycasts = true; // щоб під час затемнення нічого не натискалося

        float t = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        fadeGroup.alpha = 1f;
        SceneManager.LoadScene(nextSceneName);
    }
}