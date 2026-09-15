using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

// Причепи на GameObject NPC
// Потрібен Collider2D з Is Trigger (зона, в якій гравець може почати діалог)
[RequireComponent(typeof(Collider2D))]
public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("Репліки діалогу (по порядку)")]
    public DialogueLine[] dialogueLines;

    [Header("Підказка \"Натисни E\" (необов'язково) - показується, поки гравець поруч")]
    public GameObject interactPrompt;

    [Header("Клавіша взаємодії")]
    public Key interactKey = Key.E;

    [Header("Видати гравцю можливість атакувати одразу після цього діалогу")]
    public bool unlockPlayerAttack = true;

    [Header("Діалог спрацьовує лише один раз (типово для туторіального NPC)")]
    public bool triggerOnce = true;

    [Header("Запускати діалог одразу при вході в зону (без натискання E)")]
    public bool autoStartOnEnter = false;

    [Header("Що зробити одразу після завершення діалогу (перетягни сюди об'єкти в Inspector)")]
    public UnityEvent onDialogueComplete;

    private bool playerInRange = false;
    private bool hasTriggered = false;
    private PlayerAttack playerAttack;

    void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (!playerInRange) return;
        if (triggerOnce && hasTriggered) return;
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueActive) return;

        if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("NPCDialogueTrigger: DialogueUI не знайдено в сцені!");
            return;
        }

        if (triggerOnce)
            hasTriggered = true;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        DialogueUI.Instance.StartDialogue(dialogueLines, OnDialogueFinished);
    }

    // Викликається DialogueUI одразу після останньої репліки
    void OnDialogueFinished()
    {
        if (unlockPlayerAttack && playerAttack != null)
            playerAttack.UnlockAttack();

        // Тут спрацьовує все, що прив'язано в Inspector (наприклад, Play() другого Timeline
        // або SetActive на другій CinemachineCamera) - без жодного додаткового коду
        onDialogueComplete?.Invoke();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;

        if (triggerOnce && hasTriggered) return;

        if (playerAttack == null)
            playerAttack = other.GetComponent<PlayerAttack>();

        if (interactPrompt != null && !autoStartOnEnter)
            interactPrompt.SetActive(true);

        // Якщо увімкнено - стартуємо діалог миттєво, без очікування на клавішу E
        if (autoStartOnEnter && (DialogueUI.Instance == null || !DialogueUI.Instance.IsDialogueActive))
        {
            StartDialogue();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }
}