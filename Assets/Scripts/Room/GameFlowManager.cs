using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

// Один екземпляр на всю гру. Постав на порожній GameObject у ПЕРШІЙ сцені (Level1).
// Завдяки DontDestroyOnLoad переживає всі наступні переходи сцен
// Рівень -> Магазин -> Рівень -> Магазин -> ... і пам'ятає, на якому ми рівні.
//
// ВАЖЛИВО: назви сцен мають бути додані в File -> Build Settings -> Scenes In Build:
// "Level1", "Level2", "Level3", ... (рівні) і "Shop" (магазин, одна й та сама сцена,
// що перезавантажується заново щоразу - тому асортимент товарів рандомиться наново).
public class GameFlowManager : MonoBehaviourPun
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Назви сцен")]
    public string levelScenePrefix = "Level"; // рівні звуться Level1, Level2, Level3...
    public string shopSceneName = "Shop";

    [Header("Поточний рівень (1 = перший)")]
    public int currentLevelIndex = 1;

    [Header("Спавн у магазині")]
    [Tooltip("Тег об'єкта в сцені Shop, який позначає точку спавну гравця (постав вручну в місці, " +
             "де точно немає стін/колайдерів). Створи порожній GameObject у Shop, признач йому цей тег.")]
    public string shopSpawnPointTag = "ShopSpawnPoint";
    [Tooltip("Резервна позиція, якщо об'єкт з тегом shopSpawnPointTag не знайдено в сцені")]
    public Vector3 fallbackShopSpawnPosition = Vector3.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == shopSceneName)
            PlacePlayerAtShopSpawn();
    }

    // PersistentPlayer переживає перехід між сценами, тому без цього гравець опинявся б
    // у Shop на тих самих координатах, де стояв на попередньому рівні (і міг застрягти в стіні)
    private void PlacePlayerAtShopSpawn()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("GameFlowManager: не знайдено гравця (тег Player) при вході в Shop.");
            return;
        }

        Vector3 spawnPos = fallbackShopSpawnPosition;

        GameObject spawnPointObj = GameObject.FindGameObjectWithTag(shopSpawnPointTag);
        if (spawnPointObj != null)
        {
            spawnPos = spawnPointObj.transform.position;
        }
        else
        {
            Debug.LogWarning($"GameFlowManager: не знайдено об'єкт з тегом '{shopSpawnPointTag}' в сцені Shop - " +
                              $"використано резервну позицію {fallbackShopSpawnPosition}. Постав вручну GameObject-мітку з цим тегом на вільному місці підлоги.");
        }

        playerObj.transform.position = spawnPos;

        // Скидаємо швидкість, інакше гравець за інерцією одразу поїде геть з точки спавну
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    // Викликай, коли бос переможений (з BossRoomController)
    public void GoToShop()
    {
        LoadScene(shopSceneName);
    }

    // Викликай з порталу виходу з магазину (LevelExitPortal)
    public void GoToNextLevel()
    {
        currentLevelIndex++;
        LoadScene(levelScenePrefix + currentLevelIndex);
    }

    private void LoadScene(string sceneName)
    {
        // У мультиплеєрі сцену вантажить ЛИШЕ мастер - решта гравців підхоплюють
        // автоматично, ЯКЩО десь при вході в кімнату виставлено
        // PhotonNetwork.AutomaticallySyncScene = true (зроби це в скрипті підключення
        // до Photon, якщо ще не зроблено - інакше решта гравців не переключаться самі)
        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel(sceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}