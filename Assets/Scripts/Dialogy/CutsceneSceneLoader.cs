using UnityEngine;
using UnityEngine.Playables;

// Причепи на той самий GameObject, де вже висить PlayableDirector (напр. "After TimeLine")
// Коли Timeline дограє до кінця - автоматично запускається fade-перехід на вказану сцену
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneSceneLoader : MonoBehaviour
{
    [Header("Назва сцени, куди перейти після катсцени")]
    public string nextSceneName = "SampleScene";

    private PlayableDirector director;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
    }

    void OnEnable()
    {
        director.stopped += OnCutsceneFinished;
    }

    void OnDisable()
    {
        director.stopped -= OnCutsceneFinished;
    }

    void OnCutsceneFinished(PlayableDirector finishedDirector)
    {
        // Перевірка про всяк випадок - раптом на сцені кілька Timeline
        if (finishedDirector != director) return;

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.FadeToScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("CutsceneSceneLoader: SceneTransitionManager.Instance не знайдено!");
        }
    }
}
