using System.Collections;
using UnityEngine;

// Причепи на корінь кожної кімнати туторіалу, поруч з RoomController.
[RequireComponent(typeof(RoomController))]
public class TutorialHint : MonoBehaviour
{
    [Tooltip("Об'єкт підказки (World Space текст/картинка). Став його ДОЧІРНІМ у roomContent - " +
             "тоді він сам зникне, коли камера піде з кімнати")]
    public GameObject hintRoot;

    [Tooltip("Тривалість анімації 'вильоту'")]
    public float popDuration = 0.4f;

    [Tooltip("Звідки підказка вилітає (зсув вниз від кінцевої позиції)")]
    public float slideFromBelow = 1.5f;

    [Tooltip("Для кімнати зі стрільбою: розблокувати атаку при вході")]
    public bool unlockAttackOnShow = false;

    private RoomController room;
    private Vector3 finalLocalPos;
    private Vector3 finalScale;
    private bool shown = false;

    void Awake()
    {
        room = GetComponent<RoomController>();

        if (hintRoot != null)
        {
            finalLocalPos = hintRoot.transform.localPosition;
            finalScale = hintRoot.transform.localScale;
            hintRoot.SetActive(false);
        }
    }

    void OnEnable()  { room.OnPlayerEntered += Show; }
    void OnDisable() { room.OnPlayerEntered -= Show; }

    void Show()
    {
        if (shown) return;
        shown = true;

        if (unlockAttackOnShow)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            PlayerAttack pa = p != null ? p.GetComponent<PlayerAttack>() : null;
            if (pa != null) pa.UnlockAttack();
        }

        if (hintRoot != null)
            StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        Transform t = hintRoot.transform;
        hintRoot.SetActive(true);

        Vector3 startPos = finalLocalPos + Vector3.down * slideFromBelow;
        float time = 0f;

        while (time < popDuration)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / popDuration);

            // easeOutBack - невеликий "перельот", виглядає як підстрибування
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3) + c1 * Mathf.Pow(k - 1f, 2);

            t.localPosition = Vector3.LerpUnclamped(startPos, finalLocalPos, e);
            t.localScale = Vector3.LerpUnclamped(Vector3.zero, finalScale, e);
            yield return null;
        }

        t.localPosition = finalLocalPos;
        t.localScale = finalScale;
    }
}
