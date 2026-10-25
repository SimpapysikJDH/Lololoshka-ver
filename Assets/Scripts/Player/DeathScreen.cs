using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Екран смерті. Будує весь UI сам кодом, тому нічого не треба ні додавати в сцени,
// ні призначати в інспекторі - просто викликається з PlayerHealth.Die().
// Показує: час у грі, кількість вбитих монстрів, назву сцени, де гравець помер,
// і кнопку "Вийти в меню".
public static class DeathScreen
{
    public static void Show(GameObject player, string menuSceneName, TMP_FontAsset font)
    {
        // Беремо цифри ДО того, як щось зміниться
        string timeText = RunStats.FormatTime(RunStats.ElapsedSeconds);
        int kills = RunStats.Kills;
        string levelName = SceneManager.GetActiveScene().name;

        Time.timeScale = 0f; // гра стає на паузу, кнопка при цьому працює
        EnsureEventSystem();

        // Якщо шрифт не задано в PlayerHealth - беремо шрифт будь-якого TMP-тексту зі сцени
        // (зазвичай там вже стоїть шрифт з українськими літерами)
        if (font == null)
        {
            TMP_Text anyText = Object.FindFirstObjectByType<TMP_Text>();
            if (anyText != null) font = anyText.font;
        }

        // --- Canvas ---
        GameObject canvasObj = new GameObject("DeathScreenCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // поверх усього іншого UI

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // --- Затемнення на весь екран (заодно блокує кліки по грі під ним) ---
        GameObject dim = new GameObject("Dim");
        dim.transform.SetParent(canvasObj.transform, false);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform dimRect = dim.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        // --- Плашка по центру ---
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.13f, 0.97f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 600f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // --- Тексти ---
        CreateText(panel.transform, "ВИ ПОМЕРЛИ", 72, new Color(0.9f, 0.25f, 0.25f), font);
        CreateText(panel.transform, $"Час у грі: {timeText}", 40, Color.white, font);
        CreateText(panel.transform, $"Вбито монстрів: {kills}", 40, Color.white, font);
        CreateText(panel.transform, $"Помер на рівні: {levelName}", 40, Color.white, font);

        // --- Кнопка ---
        GameObject buttonObj = new GameObject("ExitButton");
        buttonObj.transform.SetParent(panel.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.5f, 1f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        button.colors = colors;

        LayoutElement buttonLayout = buttonObj.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 90f;
        buttonLayout.preferredWidth = 420f;

        TMP_Text buttonText = CreateText(buttonObj.transform, "Вийти в меню", 40, Color.white, font);
        RectTransform buttonTextRect = buttonText.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        Object.Destroy(buttonText.GetComponent<LayoutElement>()); // всередині кнопки розкладка не потрібна

        button.onClick.AddListener(() => ExitToMenu(player, menuSceneName));
    }

    private static TMP_Text CreateText(Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = size * 1.4f;

        return tmp;
    }

    // Якщо в сцені немає EventSystem - кнопка не реагувала б на клік
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static void ExitToMenu(GameObject player, string menuSceneName)
    {
        Time.timeScale = 1f;
        RunStats.Reset();

        // Гравець і GameFlowManager живуть між сценами (DontDestroyOnLoad), тож без цього
        // нова гра з меню почалась би зі старим гравцем і старим номером рівня.
        if (GameFlowManager.Instance != null)
            Object.Destroy(GameFlowManager.Instance.gameObject);

        if (player != null)
            Object.Destroy(player);

        SceneManager.LoadScene(menuSceneName);
    }
}
