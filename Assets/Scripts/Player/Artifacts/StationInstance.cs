using UnityEngine;

// Причепи на префаб станції (той, що вказується в ArtifactData.stationPrefab).
// Якщо на префабі немає цього компонента, StationController додасть його сам.
public class StationInstance : MonoBehaviour
{
    [Header("Візуальне згасання перед зникненням")]
    public bool fadeBeforeDespawn = true;
    public float fadeDuration = 1f; // за скільки секунд ДО кінця життя станція почне зникати

    [Header("Пульсація (необов'язково)")]
    public bool pulseScale = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.08f;

    private StationController owner;
    private float lifetime;
    private float elapsed;
    private SpriteRenderer[] renderers;
    private Color[] startColors;
    private Vector3 baseScale;
    private bool isExpiring = false;

    public Vector3 Position => transform.position;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i].color;

        baseScale = transform.localScale;
    }

    // Викликається контролером одразу після Instantiate
    public void Setup(StationController controllerOwner, float lifetimeSeconds)
    {
        owner = controllerOwner;
        lifetime = Mathf.Max(0.1f, lifetimeSeconds);
        elapsed = 0f;
        isExpiring = false;
    }

    void Update()
    {
        if (isExpiring) return;

        elapsed += Time.deltaTime;

        if (pulseScale)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * pulse;
        }

        float remaining = lifetime - elapsed;

        if (fadeBeforeDespawn && remaining <= fadeDuration)
        {
            float t = Mathf.Clamp01(remaining / fadeDuration);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color c = startColors[i];
                c.a = startColors[i].a * t;
                renderers[i].color = c;
            }
        }

        if (elapsed >= lifetime)
            Expire();
    }

    private void Expire()
    {
        if (isExpiring) return;
        isExpiring = true;

        if (owner != null)
            owner.OnStationExpired(this);

        Destroy(gameObject);
    }

    // Дозволяє контролеру примусово прибрати станцію раніше часу
    // (наприклад, коли досягнуто ліміту станцій і треба звільнити місце під нову)
    public void ForceExpire()
    {
        Expire();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
