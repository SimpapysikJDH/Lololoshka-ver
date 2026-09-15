using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Причепи на GameObject FadeCanvas (корінь, з Canvas + CanvasGroup)
// Це singleton, що переживає перехід між сценами
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Перетягни сюди CanvasGroup із чорним Image")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Тривалість затемнення / освітлення (сек)")]
    public float fadeDuration = 0.6f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    // Викликай цей метод (напр. з UnityEvent onDialogueComplete)
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        // Поки триває фейд - блокуємо клики по UI/грі
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.blocksRaycasts = true;

        // 1. Затемнення (0 -> 1)
        yield return Fade(0f, 1f);

        // 2. Завантаження нової сцени
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
            yield return null;

        // 3. Освітлення (1 -> 0) вже на новій сцені
        yield return Fade(1f, 0f);

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // unscaled - працює навіть якщо десь стоїть Time.timeScale = 0
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}
