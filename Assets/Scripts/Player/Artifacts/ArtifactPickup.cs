using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ArtifactPickup : MonoBehaviour
{
    [Header("Який артефакт це представляє")]
    public ArtifactData artifactData;

    [Header("Анімація персонажа (руки вгору тощо)")]
    public bool triggerPlayerAnimation = true;
    public string playerPickupTriggerName = "Pickup";
    public float playerAnimationDuration = 0.6f;

    [Header("Іконка та текст над головою (необов'язково)")]
    public bool showFloatingIcon = true;
    public float headHeight = 0.9f;
    public GameObject floatingTextPrefab;
    public float textExtraHeight = 0.5f;

    [Header("Сортування іконки")]
    public string sortingLayerName = "Default";
    public int sortingOrderDuringPickup = 100;

    private bool isPickingUp = false;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private ArtifactRoomSpawner spawner;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    public void Setup(ArtifactRoomSpawner owner)
    {
        spawner = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickingUp) return;

        PlayerStats stats = other.GetComponent<PlayerStats>();
        if (stats == null) return;

        // Перевірка: якщо ArtifactData забули перетягнути в префаб
        if (artifactData == null)
        {
            Debug.LogError($"[{name}] ПОМИЛКА: На цьому префабі НЕ призначено ArtifactData в Інспекторі!");
            return;
        }

        isPickingUp = true;
        col.enabled = false;

        if (spawner != null)
        {
            spawner.OnItemPicked(gameObject);
        }

        StartCoroutine(PickupRoutine(other.gameObject));
    }

    private IEnumerator PickupRoutine(GameObject player)
    {
        PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
        Animator playerAnimator = player.GetComponent<Animator>();

        if (triggerPlayerAnimation && playerMovement != null)
        {
            playerMovement.SetBusy(true);

            if (playerAnimator != null)
                playerAnimator.SetTrigger(playerPickupTriggerName);
        }

        if (showFloatingIcon)
        {
            spriteRenderer.sortingLayerName = sortingLayerName;
            spriteRenderer.sortingOrder = sortingOrderDuringPickup;

            Vector3 headPos = player.transform.position + Vector3.up * headHeight;
            transform.position = headPos;

            if (floatingTextPrefab != null)
            {
                Vector3 textPos = headPos + Vector3.up * textExtraHeight;
                GameObject textObj = Instantiate(floatingTextPrefab, textPos, Quaternion.identity);
                FloatingText floatingText = textObj.GetComponent<FloatingText>();
                if (floatingText != null)
                    floatingText.SetText(artifactData.artifactName);
            }
        }
        else
        {
            spriteRenderer.enabled = false;
        }

        float waitTime = triggerPlayerAnimation ? playerAnimationDuration : 0.3f;
        yield return new WaitForSeconds(waitTime);

        // Застосування ефекту
        artifactData.ApplyEffect(player);
        Debug.Log($"Підібрано артефакт: {artifactData.artifactName}");

        // Розблокування руху гравця
        if (triggerPlayerAnimation && playerMovement != null)
        {
            playerMovement.SetBusy(false);
        }

        Destroy(gameObject);
    }
}