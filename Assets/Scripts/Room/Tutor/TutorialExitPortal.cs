using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Портал у кімнаті з монстром. Спочатку вимкнений (візуал і колайдер),
// з'являється, коли ВСІ монстри цієї кімнати мертві.
// Не залежить від RoomController - сам знаходить монстрів і слідкує за їх смертю.
[RequireComponent(typeof(Collider2D))]
public class TutorialExitPortal : MonoBehaviour
{
    [Header("Куди йти після туторіалу")]
    public string nextSceneName = "Level1";

    [Tooltip("Візуал порталу (дочірній об'єкт), вмикається після смерті всіх монстрів")]
    public GameObject portalVisual;

    [Tooltip("Монстри, яких треба вбити. Якщо порожньо - портал сам знайде всіх MonsterHealth " +
             "у кімнаті (батьківський RoomController), включно з вимкненими")]
    public List<MonsterHealth> monsters = new List<MonsterHealth>();

    private Collider2D col;
    private bool triggered = false;
    private int aliveCount = 0;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false;
        if (portalVisual != null) portalVisual.SetActive(false);
    }

    void Start()
    {
        if (monsters.Count == 0)
        {
            RoomController room = GetComponentInParent<RoomController>();
            if (room != null)
                monsters.AddRange(room.GetComponentsInChildren<MonsterHealth>(true));
        }

        foreach (MonsterHealth m in monsters)
        {
            if (m == null) continue;
            aliveCount++;
            m.OnDeath += HandleMonsterDied;
        }

        if (aliveCount == 0)
            Debug.LogWarning("TutorialExitPortal: не знайдено жодного монстра - портал не з'явиться. " +
                             "Постав монстра в кімнату або додай його у список Monsters.");
    }

    void OnDestroy()
    {
        foreach (MonsterHealth m in monsters)
            if (m != null) m.OnDeath -= HandleMonsterDied;
    }

    void HandleMonsterDied()
    {
        aliveCount--;
        if (aliveCount > 0) return;

        col.enabled = true;
        if (portalVisual != null) portalVisual.SetActive(true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.FadeToScene(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }
}