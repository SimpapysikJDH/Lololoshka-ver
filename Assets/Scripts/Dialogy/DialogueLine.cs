using UnityEngine;

// Одна репліка діалогу. Масив таких реплік = один діалог NPC
[System.Serializable]
public class DialogueLine
{
    public string speakerName; // ім'я персонажа (можна лишити пустим, якщо не потрібно)

    public Sprite portrait; // іконка/портрет персонажа для цієї репліки (можна лишити пустим - тоді іконка сховається)

    [TextArea(2, 4)]
    public string text; // сам текст репліки
}