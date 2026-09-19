using System.Collections;
using UnityEngine;

// Причепи на тригер-зону порталу в кімнаті боса (двері/портал "В магазин").
// НЕ треба тиснути E - досить просто ЗАЙТИ в цю зону, і спрацює ефект зникання
// (частинки біля гравця + плавне розчинення), а ПОТІМ - перехід у магазин.
//
// Об'єкт з цим скриптом тримай ВИМКНЕНИМ (SetActive(false)) в редакторі -
// його вмикає BossRoomController.RunWaves через поле shopPortal
// саме в момент, коли зачищена остання хвиля.
[RequireComponent(typeof(Collider2D))]
public class BossExitPortal : MonoBehaviour
{
    [Header("Ефект зникнення гравця")]
    [Tooltip("Префаб частинок (Particle System), який заспавниться біля гравця і піде за ним")]
    public ParticleSystem disappearParticles;
    [Tooltip("Скільки секунд триває плавне розчинення гравця (від звичайного вигляду до прозорого)")]
    public float fadeDuration = 1.5f;

    private bool triggered = false; // щоб не спрацювало двічі, якщо гравець потопчеться в зоні

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        StartCoroutine(DisappearAndGoToShop(other.gameObject));
    }

    private IEnumerator DisappearAndGoToShop(GameObject playerObj)
    {
        SpriteRenderer sr = playerObj.GetComponent<SpriteRenderer>();

        ParticleSystem spawnedParticles = null;
        if (disappearParticles != null)
        {
            spawnedParticles = Instantiate(disappearParticles, playerObj.transform.position, Quaternion.identity);
            spawnedParticles.transform.SetParent(playerObj.transform); // частинки їдуть разом з гравцем
            spawnedParticles.Play();
        }

        if (sr != null && fadeDuration > 0f)
        {
            Color startColor = sr.color;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (spawnedParticles != null)
            Destroy(spawnedParticles.gameObject);

        // Повертаємо гравцю нормальну видимість ДО завантаження сцени -
        // інакше він прийде в магазин вже прозорим
        if (sr != null)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToShop();
        else
            Debug.LogWarning("BossExitPortal: GameFlowManager.Instance == null - " +
                              "переконайся, що він створений у першій сцені (Level1) і ще живий.");
    }
}