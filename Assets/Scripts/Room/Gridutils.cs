using UnityEngine;

// Допоміжні функції для роботи з сіткою кімнат
public static class GridUtils
{
    // Обертає вектор на 90 градусів (за годинниковою стрілкою) потрібну кількість разів (0-3)
    public static Vector2Int Rotate(Vector2Int v, int steps)
    {
        steps = ((steps % 4) + 4) % 4;
        for (int i = 0; i < steps; i++)
            v = new Vector2Int(v.y, -v.x);
        return v;
    }
}