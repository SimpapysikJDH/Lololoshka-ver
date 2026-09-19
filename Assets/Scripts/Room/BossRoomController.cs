using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Один запис "точка спавну -> монстр" всередині хвилі
[Serializable]
public class BossSpawnEntry
{
    [Tooltip("Точка (Transform), де з'явиться монстр цього запису")]
    public Transform spawnPoint;

    [Tooltip("Якщо задано - на цій точці заспавниться САМЕ цей монстр, " +
             "ігноруючи дефолтний префаб хвилі (defaultMonsterPrefab)")]
    public GameObject specificMonsterPrefab;
}

// Одна хвиля бою з босом
[Serializable]
public class BossWave
{
    [Tooltip("Просто для зручності в інспекторі, на гру не впливає")]
    public string waveName = "Хвиля";

    [Tooltip("Пауза (сек) перед стартом ЦІЄЇ хвилі, рахується від моменту зачистки попередньої")]
    public float delayBeforeStart = 1f;

    [Tooltip("Дефолтний префаб монстра для точок цієї хвилі, де НЕ вказано specificMonsterPrefab")]
    public GameObject defaultMonsterPrefab;

    [Tooltip("Точки спавну цієї хвилі (кожна може мати свого конкретного монстра або брати дефолтний)")]
    public List<BossSpawnEntry> spawns = new List<BossSpawnEntry>();
}

// Причепи на той самий GameObject, що й RoomController, на префабі великої кімнати боса.
// Керує чергою хвиль: замикає кімнату при вході гравця, спавнить хвилі по черзі
// (наступна - лише коли попередня повністю зачищена), а після останньої - відмикає двері.
[RequireComponent(typeof(RoomController))]
public class BossRoomController : MonoBehaviour
{
    [Header("Хвилі ворогів (по порядку)")]
    public List<BossWave> waves = new List<BossWave>();

    [Header("Зона переходу в магазин (вмикається після останньої хвилі)")]
    [Tooltip("Об'єкт з BossExitPortal - тримай його ВИМКНЕНИМ в редакторі, тут він вмикається сам після перемоги")]
    public GameObject shopPortal;

    [Header("Ефекти на старті бою (необов'язково)")]
    public ParticleSystem fightStartParticles;
    public AudioClip fightStartSfx;

    [Header("Ефекти між хвилями (необов'язково)")]
    public AudioClip waveClearedSfx;

    [Header("Ефекти після перемоги над усіма хвилями (необов'язково)")]
    public ParticleSystem victoryParticles;
    public AudioClip victorySfx;

    [Header("Джерело звуку (необов'язково, якщо звуки не задані - не потрібне)")]
    public AudioSource audioSource;

    private RoomController room;
    private bool fightStarted = false;
    private readonly List<MonsterHealth> currentWaveAlive = new List<MonsterHealth>();

    void Awake()
    {
        room = GetComponent<RoomController>();
    }

    void OnEnable()
    {
        room.OnPlayerEntered += HandlePlayerEntered;
    }

    void OnDisable()
    {
        room.OnPlayerEntered -= HandlePlayerEntered;
    }

    void HandlePlayerEntered()
    {
        if (fightStarted) return;
        if (waves == null || waves.Count == 0) return;

        fightStarted = true;

        room.LockRoomExternally();
        PlaySfx(fightStartSfx);
        if (fightStartParticles != null)
            fightStartParticles.Play();

        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        for (int i = 0; i < waves.Count; i++)
        {
            BossWave wave = waves[i];

            if (wave.delayBeforeStart > 0f)
                yield return new WaitForSeconds(wave.delayBeforeStart);

            SpawnWave(wave);

            // Чекаємо, поки всі монстри ЦІЄЇ хвилі не помруть, перш ніж переходити до наступної
            yield return new WaitUntil(() => currentWaveAlive.Count == 0);

            if (i < waves.Count - 1)
                PlaySfx(waveClearedSfx);
        }

        // Усі хвилі зачищено - відмикаємо кімнату (двері повернуться до задуму генератора)
        room.UnlockRoomExternally();
        PlaySfx(victorySfx);
        if (victoryParticles != null)
            victoryParticles.Play();

        // НЕ телепортуємо гравця автоматично - вмикаємо зону-портал,
        // ефект зникання і перехід у магазин станеться, коли гравець САМ зайде в неї
        if (shopPortal != null)
            shopPortal.SetActive(true);
        else
            Debug.LogWarning($"[{name}] BossRoomController: поле Shop Portal не призначене в інспекторі - " +
                              "гравець не зможе потрапити в магазин після перемоги.");
    }

    void SpawnWave(BossWave wave)
    {
        currentWaveAlive.Clear();

        foreach (BossSpawnEntry entry in wave.spawns)
        {
            if (entry.spawnPoint == null)
            {
                Debug.LogWarning($"[{name}] BossRoomController: у хвилі '{wave.waveName}' є запис без точки спавну - пропущено.");
                continue;
            }

            GameObject prefab = entry.specificMonsterPrefab != null ? entry.specificMonsterPrefab : wave.defaultMonsterPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[{name}] BossRoomController: точці '{entry.spawnPoint.name}' у хвилі '{wave.waveName}' " +
                                  "не задано монстра (ні specificMonsterPrefab, ні defaultMonsterPrefab хвилі) - пропущено.");
                continue;
            }

            GameObject monsterObj = Instantiate(prefab, entry.spawnPoint.position, Quaternion.identity, transform);

            MonsterHealth health = monsterObj.GetComponent<MonsterHealth>();
            if (health == null)
            {
                Debug.LogWarning($"[{name}] BossRoomController: заспавнений '{monsterObj.name}' не має MonsterHealth - " +
                                  "хвиля не зможе дочекатись його смерті.");
                continue;
            }

            currentWaveAlive.Add(health);
            health.OnDeath += () => currentWaveAlive.Remove(health);
        }

        if (currentWaveAlive.Count == 0)
        {
            Debug.LogWarning($"[{name}] BossRoomController: хвиля '{wave.waveName}' не заспавнила жодного живого монстра - " +
                              "перевір точки спавну і префаби, інакше бій застрягне на цій хвилі назавжди.");
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}