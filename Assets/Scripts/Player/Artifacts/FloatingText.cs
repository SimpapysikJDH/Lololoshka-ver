using UnityEngine;
using TMPro;

// Причепи на префаб тексту з компонентом TextMeshPro (3D, не UI!)
// GameObject -> 3D Object -> Text - TextMeshPro
[RequireComponent(typeof(TextMeshPro))]
public class FloatingText : MonoBehaviour
{
    [Header("Швидкість руху вгору")]
    public float floatSpeed = 1f;

    [Header("Час життя тексту")]
    public float lifetime = 1f;

    [Header("Сортування (щоб не ховався за картою)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 200;

    private TextMeshPro text;
    private MeshRenderer meshRenderer;
    private float elapsed;
    private Color startColor;

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
        meshRenderer = GetComponent<MeshRenderer>();
        startColor = text.color;

        // Виставляємо сортування, щоб текст малювався поверх карти й персонажа
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }

    public void SetText(string message)
    {
        text.text = message;
    }

    void Update()
    {
        // Рухаємо текст вгору
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // Поступово робимо прозорим
        elapsed += Time.deltaTime;
        float t = elapsed / lifetime;
        Color c = startColor;
        c.a = Mathf.Lerp(1f, 0f, t);
        text.color = c;

        if (elapsed >= lifetime)
            Destroy(gameObject);
    }
}