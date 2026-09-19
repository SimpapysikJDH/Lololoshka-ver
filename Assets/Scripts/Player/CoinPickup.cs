using Photon.Pun;
using UnityEngine;

// Причепи на префаб монетки (Sprite + Collider2D з Is Trigger)
// Монетка лежить на місці, поки гравець не зайде в радіус "магніту" - тоді летить до нього
public class CoinPickup : MonoBehaviourPun
{
    [Header("Скільки монет дає ця монетка")]
    public int value = 1;

    [Header("Магніт (притягування до гравця)")]
    public float magnetRange = 2.5f;   // на якій відстані монетка починає летіти до гравця
    public float magnetSpeed = 8f;     // швидкість польоту до гравця
    public float magnetAcceleration = 12f; // наскільки швидко розганяється (щоб не летіла різко з місця)

    [Header("Підбір впритул (навіть без магніту)")]
    public float pickupRange = 0.3f;

    [Header("Невеликий 'підскок' при спавні (необов'язково)")]
    public bool popOnSpawn = true;
    public float popForce = 2.5f;
    public float popDuration = 0.25f;

    private Transform player;
    private PlayerStats playerStats;
    private Rigidbody2D rb;
    private float currentSpeed = 0f;
    private bool isPopping = false;
    private float popTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerStats = playerObj.GetComponent<PlayerStats>();
        }

        if (popOnSpawn && rb != null)
        {
            isPopping = true;
            popTimer = popDuration;
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            rb.linearVelocity = randomDir * popForce;
        }
    }

    void Update()
    {
        //if (!photonView.IsMine)
        //{
        //    return;
        //}
        if (isPopping)
        {
            popTimer -= Time.deltaTime;
            if (popTimer <= 0f)
            {
                isPopping = false;
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            return; // поки триває "підскок" - магніт не працює
        }

        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= pickupRange)
        {
            Collect();
            return;
        }

        if (distance <= magnetRange)
        {
            // Плавно розганяємось у бік гравця, замість миттєвого ривка
            currentSpeed = Mathf.MoveTowards(currentSpeed, magnetSpeed, magnetAcceleration * Time.deltaTime);
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(direction * currentSpeed * Time.deltaTime);
        }
        else
        {
            currentSpeed = 0f;
        }
    }

    void Collect()
    {
        if (playerStats != null)
        {
            playerStats.AddCoins(value);
        }

        Destroy(gameObject);
    }
}
