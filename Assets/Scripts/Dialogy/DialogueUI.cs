using System;
using System.Collections.Generic;
using TMPro; // Якщо в проєкті ще немає TextMeshPro - Unity запропонує імпортувати при першому використанні
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Причепи на GameObject-менеджер діалогу (один на сцену)
// dialoguePanel - корневий UI-об'єкт панелі знизу екрана (Canvas -> Panel), на старті вимкнений
public class DialogueUI : MonoBehaviour
{
    // Простий singleton, щоб будь-який NPC міг викликати DialogueUI.Instance.StartDialogue(...)
    public static DialogueUI Instance { get; private set; }

    [Header("UI елементи (перетягни з Canvas)")]
    public GameObject dialoguePanel;   // корневий об'єкт панелі - вимкни на старті в інспекторі
    public TMP_Text speakerText;       // ім'я персонажа (необов'язково, можна не призначати)
    public TMP_Text dialogueText;      // основний текст поточної репліки
    public GameObject continueHint;    // напр. іконка/текст "Натисни E", необов'язково
    public Image speakerPortrait;      // іконка персонажа (розмісти Image справа знизу на панелі), необов'язково

    [Header("Клавіша для гортання (окрім кліку мишкою - працює завжди)")]
    public Key advanceKey = Key.E;

    // Чи зараз йде діалог - інші скрипти (NPCDialogueTrigger) перевіряють це, щоб не почати новий діалог поверх старого
    public bool IsDialogueActive { get; private set; } = false;

    private Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
    private Action onComplete;
    private PlayerMovement playerMovement;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerMovement = playerObj.GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        bool keyPressed = Keyboard.current != null && Keyboard.current[advanceKey].wasPressedThisFrame;
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (keyPressed || mouseClicked)
        {
            ShowNextLine();
        }
    }

    // Запускає діалог. onDialogueComplete викликається одразу після останньої репліки
    // (наприклад, щоб видати гравцю пляшечку і розблокувати атаку)
    public void StartDialogue(DialogueLine[] lines, Action onDialogueComplete = null)
    {
        if (lines == null || lines.Length == 0) return;

        lineQueue.Clear();
        foreach (DialogueLine line in lines)
            lineQueue.Enqueue(line);

        onComplete = onDialogueComplete;
        IsDialogueActive = true;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // Блокуємо рух гравця на час діалогу (той самий isBusy, що і під час атаки)
        if (playerMovement != null)
            playerMovement.SetBusy(true);

        ShowNextLine();
    }

    void ShowNextLine()
    {
        if (lineQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = lineQueue.Dequeue();

        if (speakerText != null)
            speakerText.text = line.speakerName;

        if (dialogueText != null)
            dialogueText.text = line.text;

        // Показуємо портрет персонажа, якщо він призначений в цій репліці; інакше ховаємо іконку
        if (speakerPortrait != null)
        {
            if (line.portrait != null)
            {
                speakerPortrait.sprite = line.portrait;
                speakerPortrait.enabled = true;
            }
            else
            {
                speakerPortrait.enabled = false;
            }
        }

        // Якщо це була остання репліка - ховаємо підказку "продовжити", бо наступний клік/E закриє діалог
        if (continueHint != null)
            continueHint.SetActive(true);
    }

    void EndDialogue()
    {
        IsDialogueActive = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (playerMovement != null)
            playerMovement.SetBusy(false);

        // Копіюємо колбек і одразу чистимо поле, щоб уникнути повторного виклику
        Action callback = onComplete;
        onComplete = null;
        callback?.Invoke();
    }
}