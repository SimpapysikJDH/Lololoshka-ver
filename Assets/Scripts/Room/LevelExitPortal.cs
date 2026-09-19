using UnityEngine;
using UnityEngine.InputSystem;

// Причепи на тригер-зону виходу з магазину (двері/портал "Далі"). Коли гравець
// заходить у зону і тисне E - переходимо на наступний рівень через GameFlowManager.
[RequireComponent(typeof(Collider2D))]
public class LevelExitPortal : MonoBehaviour
{
    public GameObject promptHint; // підказка "Натисни E, щоб іти далі" - вимикається за замовчуванням

    private bool playerInside = false;

    void Update()
    {
        if (!playerInside) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.GoToNextLevel();
            else
                Debug.LogWarning("LevelExitPortal: GameFlowManager.Instance == null - переконайся, що він створений у першій сцені (Level1) і ще живий.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        if (promptHint != null) promptHint.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        if (promptHint != null) promptHint.SetActive(false);
    }
}
