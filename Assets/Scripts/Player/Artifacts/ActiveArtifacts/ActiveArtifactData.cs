using System.Collections;
using UnityEngine;

// Тип ефекту активного артефакту. Легко додати новий - просто додай значення сюди
// і обробку в switch всередині Activate().
public enum ActiveArtifactEffectType
{
    FreezeEnemies,   // заморожує всіх монстрів на сцені на duration секунд
    SlowEnemies,     // сповільнює всіх монстрів на сцені (value = множник швидкості 0..1, напр. 0.5) на duration секунд
    SpeedBoost,      // тимчасово додає moveSpeed на duration секунд
    DamageBoost,     // тимчасово додає damage на duration секунд
    Invincibility,   // тимчасова недоторканність (ігнорує будь-яку шкоду) на duration секунд
    Heal,            // миттєво лікує на value одиниць здоров'я (duration не використовується)
    ExtraLife,       // миттєво дає value "золотих сердець" (PlayerHealth.AddGoldenHeart) -
                     // кожне повністю блокує один удар, як зайве життя (duration не використовується)
    DashForward,     // ривок гравця вперед (value = швидкість ривка, duration = тривалість),
                     // штовхає (Knockback) ворогів на шляху
    RevealMap        // миттєво розкриває ВСІ кімнати рівня на мінімапі (value/duration не використовуються).
                     // Відкриває лише самі кімнати (форму рівня), а не вміст - невідвідані кімнати
                     // лишаються сірими (neighborHintColor), як і при звичайному розкритті сусідів
}

// Активний (розхідний) артефакт - на відміну від ArtifactData, ефект НЕ застосовується одразу
// при підборі, а чекає, поки гравець сам не натисне клавішу активації (ActiveArtifactSlot).
// Гравець може тримати лише ОДИН активний артефакт одночасно - новий підбір замінює старий.
//
// Щоб створити новий: клік правою в Project -> Create -> Artifacts -> New Active Artifact
[CreateAssetMenu(fileName = "New Active Artifact", menuName = "Artifacts/New Active Artifact")]
public class ActiveArtifactData : ScriptableObject
{
    [Header("Інформація")]
    public string artifactName = "Активний артефакт";
    public Sprite icon;
    [TextArea] public string description;

    [Header("Тип ефекту")]
    public ActiveArtifactEffectType effectType;

    [Header("Тривалість ефекту (секунди). Для Heal не використовується")]
    public float duration = 6f;

    [Header("Сила ефекту (сенс залежить від типу: бонус швидкості/урону, к-сть ХП для Heal)")]
    public float value = 1f;

    [Header("Використання")]
    [Tooltip("Скільки разів можна активувати артефакт, перш ніж він зникне зі слота. 1 = одноразовий.")]
    public int charges = 1;

    [Header("Кулдаун між активаціями - в кількості ЗАЧИЩЕНИХ кімнат (не просто пройдених)")]
    [Tooltip("0 = без кулдауну, можна активувати знову одразу, якщо лишились заряди")]
    public int cooldownRooms = 1;

    [Header("Префаб цього ж артефакту у світі (щоб викинути на землю, якщо його витіснив інший)")]
    public GameObject worldPickupPrefab;

    // Викликається з ActiveArtifactSlot.TryActivate(). runner потрібен, щоб ScriptableObject
    // міг запустити корутину (самі ScriptableObject-и корутини запускати не можуть).
    public virtual void Activate(GameObject player, MonoBehaviour runner)
    {
        switch (effectType)
        {
            case ActiveArtifactEffectType.FreezeEnemies:
                FreezeAllEnemies(duration);
                break;

            case ActiveArtifactEffectType.SlowEnemies:
                SlowAllEnemies(value, duration);
                break;

            case ActiveArtifactEffectType.SpeedBoost:
                runner.StartCoroutine(TemporarySpeedBoost(player, value, duration));
                break;

            case ActiveArtifactEffectType.DamageBoost:
                runner.StartCoroutine(TemporaryDamageBoost(player, value, duration));
                break;

            case ActiveArtifactEffectType.Invincibility:
                ApplyInvincibility(player, duration);
                break;

            case ActiveArtifactEffectType.Heal:
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                if (health != null)
                    health.Heal(Mathf.RoundToInt(value));
                break;

            case ActiveArtifactEffectType.ExtraLife:
                PlayerHealth healthForLives = player.GetComponent<PlayerHealth>();
                if (healthForLives != null)
                {
                    int lives = Mathf.RoundToInt(value);
                    for (int i = 0; i < lives; i++)
                        healthForLives.AddGoldenHeart();
                }
                break;

            case ActiveArtifactEffectType.DashForward:
                runner.StartCoroutine(DashForwardRoutine(player, value, duration));
                break;

            case ActiveArtifactEffectType.RevealMap:
                if (MinimapUI.Instance != null)
                    MinimapUI.Instance.RevealAllRooms();
                break;
        }
    }

    // Заморожує всіх монстрів на сцені, які мають на собі компонент з IFreezable.
    // Використовуємо MonsterHealth для пошуку (як і Projectile.cs), бо він є на будь-якому монстрі.
    private void FreezeAllEnemies(float freezeDuration)
    {
        MonsterHealth[] monsters = Object.FindObjectsByType<MonsterHealth>(FindObjectsSortMode.None);

        foreach (MonsterHealth monster in monsters)
        {
            IFreezable freezable = monster.GetComponent<IFreezable>();
            if (freezable != null)
                freezable.Freeze(freezeDuration);
        }
    }

    // Сповільнює всіх монстрів на сцені, які мають на собі компонент з ISlowable.
    // На відміну від FreezeAllEnemies - монстри не зупиняються повністю, а лише рухаються повільніше.
    private void SlowAllEnemies(float slowMultiplier, float slowDuration)
    {
        MonsterHealth[] monsters = Object.FindObjectsByType<MonsterHealth>(FindObjectsSortMode.None);

        foreach (MonsterHealth monster in monsters)
        {
            ISlowable slowable = monster.GetComponent<ISlowable>();
            if (slowable != null)
                slowable.Slow(slowMultiplier, slowDuration);
        }
    }

    private IEnumerator TemporarySpeedBoost(GameObject player, float bonus, float dur)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null) yield break;

        stats.AddMoveSpeed(bonus);
        yield return new WaitForSeconds(dur);
        stats.AddMoveSpeed(-bonus); // знімаємо рівно той самий бонус назад
    }

    private IEnumerator TemporaryDamageBoost(GameObject player, float bonus, float dur)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null) yield break;

        stats.AddDamage(bonus);
        yield return new WaitForSeconds(dur);
        stats.AddDamage(-bonus);
    }

    // Потребує методу PlayerHealth.GrantTemporaryInvincibility(float) - додано в оновленому PlayerHealth.cs
    private void ApplyInvincibility(GameObject player, float dur)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
            health.GrantTemporaryInvincibility(dur);
    }

    // Ривок гравця вперед: dashSpeed (value) - швидкість руху під час ривка,
    // dashDuration (duration) - скільки секунд він триває. Штовхає (Knockback) будь-якого
    // ворога з компонентом IKnockbackable, що трапиться на шляху.
    //
    // Потребує двох методів у PlayerMovement (додай їх за аналогією з уже наявним SetBusy):
    //   - Vector2 GetFacingDirection() - повертає напрямок, куди зараз "дивиться" гравець
    //   - SetBusy(bool)                - вже використовується в ActiveArtifactPickup,
    //                                    блокує звичайне керування під час ривка
    private IEnumerator DashForwardRoutine(GameObject player, float dashSpeed, float dashDuration)
    {
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (movement == null || rb == null) yield break;

        Vector2 direction = movement.GetFacingDirection();
        if (direction == Vector2.zero) direction = Vector2.down;

        movement.SetBusy(true);

        // Радіус навколо гравця, у якому ривок "зачіпає" і штовхає ворогів
        const float pushRadius = 0.6f;
        const float knockbackForce = 8f;
        const float staggerDuration = 0.3f;

        float timer = dashDuration;
        while (timer > 0f)
        {
            rb.linearVelocity = direction * dashSpeed;

            Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, pushRadius);
            foreach (Collider2D hit in hits)
            {
                // GetComponentInParent, а не GetComponent - фізичний колайдер монстра часто
                // висить на дочірньому об'єкті, а IKnockbackable реалізовано на батьківському
                // MonsterController (там же, де MonsterHealth). GetComponent тут шукав би
                // лише на самому колайдері й ніколи б нічого не знаходив.
                IKnockbackable knockbackable = hit.GetComponentInParent<IKnockbackable>();
                if (knockbackable != null)
                    knockbackable.Knockback(direction, knockbackForce, staggerDuration);
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        movement.SetBusy(false);
    }
}