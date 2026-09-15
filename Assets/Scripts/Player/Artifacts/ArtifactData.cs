using UnityEngine;

// Щоб створити новий артефакт: клік правою в Project -> Create -> Artifacts -> New Artifact
// Порада: майже всі числові поля нижче можна ставити з мінусом - тоді артефакт
// не додає стат, а віднімає його (наприклад -2 в speedBonus зробить гравця повільнішим).
// Усі стати захищені від виходу в нуль/мінус там, де це зламало б гру (PlayerStats.cs / PlayerHealth.cs).
[CreateAssetMenu(fileName = "New Artifact", menuName = "Artifacts/New Artifact")]
public class ArtifactData : ScriptableObject
{
    [Header("Інформація")]
    public string artifactName = "Артефакт";
    public Sprite icon;
    [TextArea] public string description;

    [Header("Швидкість гравця")]
    public float speedBonus = 0f;

    [Header("Здоров'я (2 = одне повне серце)")]
    public int heartsToAdd = 0;

    [Header("Снаряди - базові стати")]
    public float projectileSpeedBonus = 0f;
    public float damageBonus = 0f;
    public float attackRangeBonus = 0f;
    public float attackCooldownReduction = 0f; // зменшує затримку між пострілами (швидша стрільба)
    public float sizeMultiplierBonus = 0f;     // + до розміру снаряда (0.2 = +20% до поточного розміру)

    [Header("Снаряди - спецефекти")]
    public bool enableBounce = false;
    public int bounceCountBonus = 0;
    public bool enableHoming = false;
    public float homingStrength = 3f;
    public int pierceBonus = 0;
    public bool enableInfinitePierce = false; // снаряд проходить крізь УСІХ монстрів, без обмежень

    [Header("Снаряди - типи польоту (можна комбінувати між собою)")]
    public bool enableZigzag = false;
    public float zigzagFrequencyBonus = 0f;   // + до швидкості "хвилі"
    public float zigzagAmplitudeBonus = 0f;   // + до кута відхилення (градуси)

    public bool enableChaotic = false;
    public float chaosStrengthBonus = 0f;     // + до сили випадкових поворотів (градуси)

    public bool enableOrbit = false;          // снаряд закручується по колу навколо гравця
    // Радіус орбіти = attackRange, швидкість обертання = projectileSpeed - це звичайні
    // стати гравця, окремих бонусів-полів для радіусу/швидкості орбіти більше нема.
    public float orbitDurationBonus = 0f;     // + до тривалості орбіти (секунди)

    [Header("Особливі ресурси")]
    public int sparkAmount = 0; // Іскра
    public int luckAmount = 0;  // Удача

    [Header("Станції (ставлять точки, що з'єднуються лінією-шкодою від 3 штук)")]
    public bool enableStations = false;
    public GameObject stationPrefab;              // префаб станції (StationInstance); можна лишити пустим
    public bool stationsPlaceOnAttack = true;       // true = станція ставиться ЗАМІСТЬ кульки при атаці; false = ставляться самі по таймеру (старий режим)
    public int maxStationsBonus = 0;               // + до макс. кількості одночасних станцій
    public float stationLifetimeBonus = 0f;        // + до часу життя однієї станції (сек)
    public float stationSpawnIntervalReduction = 0f; // - до інтервалу між постановками (діє лише якщо stationsPlaceOnAttack = false)
    public float stationLinkDamageBonus = 0f;       // + до шкоди атаки (лінія станцій = той самий стат damage, не окремий)
    public float stationLinkWidthBonus = 0f;        // + до товщини лінії-шкоди

    [Header("Станції - фіксовані шкода/товщина лінії (ВИСТАВЛЯЄ стат РІВНО на це значення, а не бонусом; 0 = не використовувати)")]
    public float stationLinkFixedDamage = 0f;       // якщо > 0 - PlayerStats.damage буде виставлено РІВНО на це значення (лінія й атака - один стат)
    public float stationLinkFixedWidth = 0f;        // якщо > 0 - лінія завжди матиме РІВНО цю товщину, незалежно від бонусів вище

    [Header("Станції - кастомний вигляд лінії (необов'язково)")]
    public Texture2D stationLinkTexture;            // текстура лінії (напр. електрика/енергія); якщо null - лінія суцільного кольору
    public float stationLinkScrollSpeed = 1.5f;     // швидкість "перетікання" текстури
    public Gradient stationLinkColorGradient;       // якщо задано (є ключі кольору) - переважає над звичайним кольором лінії

    [Header("Станції - шар ворогів (ВАЖЛИВО, якщо шкода не спрацьовує)")]
    // StationController додається В РАНТАЙМІ (AddComponent), тому його enemyLayer НЕ можна
    // заздалегідь виставити в інспекторі гравця. Якщо шкода від станцій/кільця не наноситься -
    // призначте тут правильний шар ворогів вашого проєкту (ScriptableObject це дозволяє налаштувати заздалегідь).
    public LayerMask stationEnemyLayerOverride;

    [Header("Мультишот / рандомна точка вильоту (діє через PlayerAttack, не через PlayerStats)")]
    [Tooltip("+ до кількості кульок за один постріл (PlayerAttack.projectileCount). Результат не може опуститись нижче 1")]
    public int projectileCountBonus = 0;
    [Tooltip("+ до паузи між кульками в черзі (PlayerAttack.multishotInterval), у секундах. За замовчуванням не обов'язково чіпати - базова пауза 0.08с в PlayerAttack вже й так стріляє чергою, як тільки к-сть кульок > 1")]
    public float multishotIntervalBonus = 0f;
    [Tooltip("+ до кута розкиду віяла між кульками в черзі (PlayerAttack.multishotSpreadAngle), у градусах")]
    public float multishotSpreadAngleBonus = 0f;
    [Tooltip("+ до бічного зсуву між кульками, поруч одна з одною (PlayerAttack.multishotSpacing), у світових одиницях")]
    public float multishotSpacingBonus = 0f;
    [Tooltip("+ до радіусу випадкового зсуву точки вильоту (PlayerAttack.randomSpawnRadius). Результат не може опуститись нижче 0")]
    public float randomSpawnRadiusBonus = 0f;

    [Header("Комбо: Орбіта + Станції -> лазерне кільце (стати ВИСТАВЛЯЮТЬСЯ на ці значення ОДНОРАЗОВО, в момент активації комбо)")]
    // Спрацьовує, коли гравець підбирає ДРУГИЙ з двох артефактів (орбіта + станції) - байдуже, який саме.
    // На відміну від бонусів вище - це не "+ чи -" до поточного стату, а КОНКРЕТНЕ значення, на яке
    // стат буде виставлено (незалежно від того, що було до цього). Спрацьовує ОДИН РАЗ, наступні
    // артефакти після активації комбо знову додають бонуси як завжди, а не перезаписують.
    public float comboFixedDamage = 0.1f;
    public float comboFixedAttackRange = 2f;
    public float comboFixedAttackCooldown = 8f;

    // Можна перевизначити (override) в окремих артефактах для унікальної поведінки
    public virtual void ApplyEffect(GameObject player)
    {
        // Реєструємо артефакт в інвентарі гравця (для вікна інвентаря по E) - робимо це
        // одразу на початку, незалежно від того, які саме стати нижче цей артефакт дає.
        ArtifactInventory inventory = player.GetComponent<ArtifactInventory>();
        if (inventory != null)
            inventory.AddArtifact(this);

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.AddMoveSpeed(speedBonus);
            stats.AddProjectileSpeed(projectileSpeedBonus);
            stats.AddDamage(damageBonus);
            stats.AddAttackRange(attackRangeBonus);
            stats.ReduceAttackCooldown(attackCooldownReduction);
            stats.AddProjectileSize(sizeMultiplierBonus);
            stats.AddSpark(sparkAmount);
            stats.AddLuck(luckAmount);

            if (enableBounce)
                stats.EnableBounce(bounceCountBonus);

            if (enableHoming)
                stats.EnableHoming(homingStrength);

            if (pierceBonus != 0)
                stats.AddPierce(pierceBonus);

            if (enableInfinitePierce)
                stats.EnableInfinitePierce();

            if (enableZigzag)
                stats.EnableZigzag(zigzagFrequencyBonus, zigzagAmplitudeBonus);

            if (enableChaotic)
                stats.EnableChaotic(chaosStrengthBonus);

            if (enableOrbit)
                stats.EnableOrbit(orbitDurationBonus);
        }

        if (heartsToAdd != 0)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.AddMaxHearts(heartsToAdd);
        }

        if (enableStations)
        {
            StationController stationController = player.GetComponent<StationController>();
            if (stationController == null)
                stationController = player.AddComponent<StationController>();

            stationController.EnableFeature(stationPrefab);
            stationController.SetPlaceOnAttack(stationsPlaceOnAttack);
            stationController.AddMaxStations(maxStationsBonus);
            stationController.AddStationLifetime(stationLifetimeBonus);
            stationController.ReduceSpawnInterval(stationSpawnIntervalReduction);
            stationController.AddLinkWidth(stationLinkWidthBonus);

            // Шкода лінії станцій - це той самий стат, що й звичайна шкода атаки
            // (PlayerStats.damage), а не окреме число. Тому міняємо тут напряму через
            // stats.AddDamage() - завдяки цьому й лінія стає сильнішою, і UI (PlayerStatsUI)
            // одразу показує зміну звичайним попапом на полі "Шкода", без будь-яких
            // окремих полів чи алісів під станції.
            if (stats != null)
            {
                if (stationLinkDamageBonus != 0f)
                    stats.AddDamage(stationLinkDamageBonus);

                // Фіксоване значення застосовується ПІСЛЯ бонусу і перекриває його -
                // рахуємо потрібну дельту самі (як comboFixedDamage нижче), щоб спрацював
                // і кламп-захист від нуля/мінуса, і подія OnStatChanged (попап у UI).
                if (stationLinkFixedDamage > 0f)
                    stats.AddDamage(stationLinkFixedDamage - stats.damage);
            }

            // Фіксована товщина застосовується ПІСЛЯ бонусу вище і перекриває його -
            // якщо задано, лінія матиме рівно це значення, скільки б бонусів до цього не накопичилось.
            if (stationLinkFixedWidth > 0f)
                stationController.SetLinkWidth(stationLinkFixedWidth);

            if (stationLinkTexture != null)
                stationController.linkTexture = stationLinkTexture;
            if (stationLinkColorGradient != null && stationLinkColorGradient.colorKeys.Length > 0)
                stationController.linkColorGradient = stationLinkColorGradient;
            if (stationLinkScrollSpeed != 0f)
                stationController.linkScrollSpeed = stationLinkScrollSpeed;

            if (stationEnemyLayerOverride.value != 0)
                stationController.enemyLayer = stationEnemyLayerOverride;
        }

        // Мультишот / рандомна точка вильоту - на відміну від стату вище, це не через PlayerStats,
        // а напряму через PlayerAttack (там же й живуть projectileCount/randomSpawnRadius).
        // Клампимо тут же, бо PlayerAttack сам себе не захищає (звичайні поля інспектора).
        if (projectileCountBonus != 0 || multishotIntervalBonus != 0f || multishotSpreadAngleBonus != 0f || multishotSpacingBonus != 0f || randomSpawnRadiusBonus != 0f)
        {
            PlayerAttack attack = player.GetComponent<PlayerAttack>();
            if (attack != null)
            {
                attack.projectileCount = Mathf.Max(1, attack.projectileCount + projectileCountBonus);
                attack.multishotInterval = Mathf.Max(0f, attack.multishotInterval + multishotIntervalBonus);
                attack.multishotSpreadAngle += multishotSpreadAngleBonus;
                attack.multishotSpacing += multishotSpacingBonus;
                attack.randomSpawnRadius = Mathf.Max(0f, attack.randomSpawnRadius + randomSpawnRadiusBonus);
            }
        }

        // Комбо: якщо в гравця вже є ОБИДВА артефакти - орбіта і станції (байдуже, який взяли першим) -
        // атака замість звичайної станції-крапки ставить лазерне кільце навколо гравця з обмеженим часом життя.
        PlayerStats statsForCombo = player.GetComponent<PlayerStats>();
        StationController stationForCombo = player.GetComponent<StationController>();
        if (statsForCombo != null && stationForCombo != null && statsForCombo.projectileOrbit && stationForCombo.isEnabled)
        {
            // comboOrbitLaserEnabled ще false -> це САМЕ ЗАРАЗ вперше спрацювало комбо (взяли другий з двох артефактів).
            // Перевіряємо ДО EnableComboOrbitLaser(), інакше прапорець вже стане true і ми не відрізнимо
            // "щойно активувалось" від "уже давно активне, просто взяли ще один артефакт".
            bool comboJustActivated = !stationForCombo.comboOrbitLaserEnabled;

            stationForCombo.EnableComboOrbitLaser();

            if (comboJustActivated)
            {
                // Треба виставити стати РІВНО на комбо-значення (не додати/відняти довільно), але
                // робимо це через ті самі Add/Reduce методи PlayerStats, порахувавши потрібну дельту
                // самим - так спрацює і кламп-захист від нуля/мінуса, і подія OnStatChanged, тобто
                // PlayerStatsUI сама покаже спливаючу підказку (+/-) про те, що стат змінився.
                statsForCombo.AddDamage(comboFixedDamage - statsForCombo.damage);
                statsForCombo.AddAttackRange(comboFixedAttackRange - statsForCombo.attackRange);
                statsForCombo.ReduceAttackCooldown(statsForCombo.attackCooldown - comboFixedAttackCooldown);
            }
        }
    }
}