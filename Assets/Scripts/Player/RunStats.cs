using UnityEngine;

// Статистика одного забігу (від першої появи гравця до смерті).
// Це статичний клас - його НЕ треба нікуди чіпляти, він сам живе між сценами.
//   - час стартує в PlayerHealth.Start() (один раз за забіг)
//   - вбивства додає MonsterHealth.Die()
//   - скидається при виході в меню (DeathScreen) і при кожному запуску Play
public static class RunStats
{
    public static int Kills { get; private set; }

    private static bool started;
    private static float startTime;

    // Скільки секунд пройшло з початку забігу (час на паузі, timeScale = 0, не рахується)
    public static float ElapsedSeconds => started ? Time.time - startTime : 0f;

    // Захист на випадок вимкненого Domain Reload в редакторі - static-поля інакше
    // переживали б зупинку Play і наступний запуск почався б зі старими цифрами.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Reset();
    }

    public static void StartRun()
    {
        if (started) return;
        started = true;
        startTime = Time.time;
    }

    public static void RegisterKill()
    {
        Kills++;
    }

    public static void Reset()
    {
        started = false;
        startTime = 0f;
        Kills = 0;
    }

    // "05:07" або "1:05:07", якщо грали більше години
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}
