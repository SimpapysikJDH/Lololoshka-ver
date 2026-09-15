using System.Collections;
using UnityEngine;

// Причепи на префаб активного артефакту (Sprite + Collider2D з Is Trigger).
// На відміну від ArtifactPickup (пасивні артефакти), цей НЕ застосовує ефект одразу -
// він кладе артефакт у ActiveArtifactSlot гравця, звідки гравець сам активує його клавішею.
// Якщо в слоті вже щось лежало - воно випадає на землю (worldPickupPrefab цього артефакту).
[RequireComponent(typeof(SpriteRenderer))]
public class ActiveArtifactPickup : MonoBehaviour
{
    [Header("Який активний артефакт це представляє")]
    public ActiveArtifactData artifactData;

    [Header("Анімація гравця при підборі (руки вгору тощо)")]
    public bool triggerPlayerAnimation = true;
    public string playerPickupTriggerName = "Pickup";
    public float playerAnimationDuration = 0.6f;

    [Header("Текст над головою (необов'язково)")]
    public GameObject floatingTextPrefab;
    public float textHeight = 1.2f;

    private bool isPickingUp = false;
    private Collider2D col;

    // Заповнюється через SetCarriedState(), коли цей пікап - НЕ знахідка з рівня,
    // а артефакт, який гравець сам щойно викинув зі слота (витіснивши іншим).
    // Якщо стан "з собою" є - при підборі відновлюємо саме його (заряди й кулдаун),
    // а НЕ видаємо повний набір зарядів заново. Це і закриває абуз "викинув -> підняв -> знову повний".
    private bool hasCarriedState = false;
    private int carriedCharges;
    private int carriedRoomsUntilReady;

    public void SetCarriedState(int chargesLeft, int roomsUntilReady)
    {
        hasCarriedState = true;
        carriedCharges = chargesLeft;
        carriedRoomsUntilReady = roomsUntilReady;
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickingUp) return;

        ActiveArtifactSlot slot = other.GetComponent<ActiveArtifactSlot>();
        if (slot == null) return;

        if (artifactData == null)
        {
            Debug.LogError($"[{name}] ПОМИЛКА: На цьому префабі НЕ призначено ActiveArtifactData в Інспекторі!");
            return;
        }

        isPickingUp = true;
        col.enabled = false;

        StartCoroutine(PickupRoutine(other.gameObject, slot));
    }

    private IEnumerator PickupRoutine(GameObject player, ActiveArtifactSlot slot)
    {
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Animator animator = player.GetComponent<Animator>();

        if (triggerPlayerAnimation && movement != null)
        {
            movement.SetBusy(true);
            if (animator != null)
                animator.SetTrigger(playerPickupTriggerName);
        }

        if (floatingTextPrefab != null)
        {
            Vector3 textPos = player.transform.position + Vector3.up * textHeight;
            GameObject textObj = Instantiate(floatingTextPrefab, textPos, Quaternion.identity);
            FloatingText floatingText = textObj.GetComponent<FloatingText>();
            if (floatingText != null)
                floatingText.SetText(artifactData.artifactName);
        }

        float waitTime = triggerPlayerAnimation ? playerAnimationDuration : 0.2f;
        yield return new WaitForSeconds(waitTime);

        // Стан того, що ЗАРАЗ лежить у слоті (ще до заміни) - знадобиться,
        // щоб при витісненні викинути його на землю з РЕАЛЬНИМИ зарядами/кулдауном,
        // а не даруючи йому "нову молодість" при наступному підборі.
        int previousCharges = slot.chargesLeft;
        int previousRoomsUntilReady = slot.roomsUntilReady;

        // Кладемо артефакт у слот. Якщо цей пікап сам є раніше викинутим артефактом
        // (hasCarriedState) - відновлюємо саме його заряди/кулдаун, інакше (знахідка
        // з рівня) видаємо повний набір зарядів без кулдауну.
        ActiveArtifactData previous = hasCarriedState
            ? slot.Equip(artifactData, carriedCharges, carriedRoomsUntilReady)
            : slot.Equip(artifactData);

        if (previous != null && previous.worldPickupPrefab != null)
        {
            Vector3 dropPos = player.transform.position + (Vector3)(Random.insideUnitCircle.normalized * 0.7f);
            GameObject droppedObj = Instantiate(previous.worldPickupPrefab, dropPos, Quaternion.identity);

            ActiveArtifactPickup droppedPickup = droppedObj.GetComponent<ActiveArtifactPickup>();
            if (droppedPickup != null)
                droppedPickup.SetCarriedState(previousCharges, previousRoomsUntilReady);
        }

        if (triggerPlayerAnimation && movement != null)
            movement.SetBusy(false);

        Destroy(gameObject);
    }
}