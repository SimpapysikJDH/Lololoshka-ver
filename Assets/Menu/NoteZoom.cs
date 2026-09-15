using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Скрипт для механіки "дошка з нотатками": клік по блоку -> наближення (zoom)
// -> поява тексту. Клік "назад" -> повернення на місце.
//
// НАЛАШТУВАННЯ В UNITY:
// 1. Повісьте цей скрипт на кожен блок (Image + Button) з нотаткою.
// 2. noteRect - RectTransform самого блоку (зазвичай той самий об'єкт).
// 3. targetPos - порожній RectTransform у центрі екрана з потрібним
//    розміром/масштабом для "наближеного" вигляду. Створіть Empty GameObject
//    в Canvas, розтягніть/розмістіть як треба, це і буде ціль анімації.
// 4. slidePanel - ПОВНИЙ підготовлений "слайд" (окремий GameObject під
//    Canvas, на весь екран), який може містити будь-що: текст, картинки,
//    СВОЇ кнопки з власними OnClick(). Це вже готовий UI-екран, а не просто
//    текст. Вимкніть його (SetActive(false)) заздалегідь.
//    ВАЖЛИВО: розмістіть slidePanel в Hierarchy НИЖЧЕ за AllNotes і
//    zoomParent (тобто останнім серед дітей Canvas), щоб він малювався і
//    приймав кліки ПОВЕРХ усього іншого.
//    Якщо у слайда є власна кнопка "Назад" - призначте на неї
//    OnClick() -> цей самий NoteZoom.OnBackClick().
// 5. otherNotesGroup - CanvasGroup, що обгортає ВСІ нотатки (наприклад,
//    об'єкт "AllNotes"), щоб сховати їх і заблокувати клік, поки одна відкрита.
//    ВАЖЛИВО: клікнута нотатка на час зуму сама тимчасово переноситься
//    (reparent) ПОЗА цю групу - інакше alpha=0 сховає і її саму.
// 6. zoomParent - об'єкт, куди клікнута нотатка тимчасово переноситься на
//    час перегляду (наприклад, сам Canvas, або окремий "ActiveNoteLayer",
//    розміщений НАД AllNotes, але ПІД slidePanel в ієрархії).
// 7. noteCanvasGroup (необов'язково) - CanvasGroup на самій нотатці. Коли
//    з'являється slidePanel, саму "картинку" нотатки ховаємо (вона вже
//    виконала свою роль анімації), щоб не заважала клікам по кнопках слайду.
// 8. На кнопці блоку: OnClick() -> NoteZoom.OnNoteClick()
// 9. На кнопці "Назад" (або фон, або кнопка всередині слайду):
//    OnClick() -> NoteZoom.OnBackClick()

public class NoteZoom : MonoBehaviour
{
    [Header("Основні посилання")]
    public RectTransform noteRect;      // сам блок нотатки
    public RectTransform targetPos;     // куди і як він має "наблизитись"
    public GameObject slidePanel;       // готовий слайд (текст/картинки/кнопки), що з'являється після зуму
    public CanvasGroup otherNotesGroup; // група ВСІХ нотаток (наприклад AllNotes)
    public Transform zoomParent;        // куди тимчасово переносити нотатку під час зуму
    public CanvasGroup noteCanvasGroup; // (необов'язково) для приховування самої нотатки під слайдом

    [Header("Налаштування анімації")]
    public float duration = 0.5f;

    private Vector3 startScale;
    private Vector2 startPos;
    private Transform originalParent;
    private int originalSiblingIndex;
    private bool isZoomed = false;

    void Start()
    {
        // Запам'ятовуємо початкові позицію та масштаб, щоб потім повернути назад
        startScale = noteRect.localScale;
        startPos = noteRect.anchoredPosition;
        originalParent = noteRect.parent;

        if (slidePanel != null)
            slidePanel.SetActive(false);
    }

    public void OnNoteClick()
    {
        if (isZoomed) return;
        isZoomed = true;

        // Запам'ятовуємо, де саме нотатка стояла в ієрархії (щоб повернути точно туди ж)
        originalSiblingIndex = noteRect.GetSiblingIndex();

        // Переносимо нотатку ПОЗА AllNotes, щоб alpha=0 групи її не сховала
        if (zoomParent != null)
            noteRect.SetParent(zoomParent, true); // true = зберегти world position при переносі

        StopAllCoroutines();
        StartCoroutine(ZoomRoutine(true));
    }

    public void OnBackClick()
    {
        if (!isZoomed) return;
        isZoomed = false;

        // Спочатку ховаємо слайд і повертаємо видимість самій нотатці
        if (slidePanel != null)
            slidePanel.SetActive(false);

        if (noteCanvasGroup != null)
        {
            noteCanvasGroup.alpha = 1f;
            noteCanvasGroup.blocksRaycasts = true;
        }

        StopAllCoroutines();
        StartCoroutine(ZoomRoutine(false));
    }

    private IEnumerator ZoomRoutine(bool zoomingIn)
    {
        // Ховаємо/показуємо решту нотаток та блокуємо їхні кліки
        if (otherNotesGroup != null)
        {
            otherNotesGroup.alpha = zoomingIn ? 0f : 1f;
            otherNotesGroup.blocksRaycasts = !zoomingIn;
            otherNotesGroup.interactable = !zoomingIn;
        }

        Vector2 fromPos = noteRect.anchoredPosition;
        Vector3 fromScale = noteRect.localScale;

        Vector2 toPos = zoomingIn ? targetPos.anchoredPosition : startPos;
        Vector3 toScale = zoomingIn ? targetPos.localScale : startScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            noteRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, p);
            noteRect.localScale = Vector3.Lerp(fromScale, toScale, p);
            yield return null;
        }

        noteRect.anchoredPosition = toPos;
        noteRect.localScale = toScale;

        if (zoomingIn)
        {
            // Наближення завершене - ховаємо саму нотатку (вона свою роль
            // виконала) і показуємо повний підготовлений слайд
            if (noteCanvasGroup != null)
            {
                noteCanvasGroup.alpha = 0f;
                noteCanvasGroup.blocksRaycasts = false;
            }

            if (slidePanel != null)
                slidePanel.SetActive(true);
        }

        // Після повернення на місце - повертаємо нотатку назад у AllNotes,
        // на ту саму позицію в ієрархії, де вона й була
        if (!zoomingIn && originalParent != null)
        {
            noteRect.SetParent(originalParent, true);
            noteRect.SetSiblingIndex(originalSiblingIndex);
        }
    }
}