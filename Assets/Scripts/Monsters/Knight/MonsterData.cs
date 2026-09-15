using UnityEngine;

// Щоб створити нового монстра: клік правою в Project -> Create -> Monsters -> New Monster
[CreateAssetMenu(fileName = "New Monster", menuName = "Monsters/New Monster")]
public class MonsterData : ScriptableObject
{
    [Header("Інформація")]
    public string monsterName = "Монстр";

    [Header("Здоров'я")]
    public float maxHP = 5;

    [Header("Рух")]
    public float moveSpeed = 2f;

    [Header("Виявлення гравця (радіус, з якого монстр починає переслідувати)")]
    public float detectionRange = 6f;

    [Header("Ближній бій")]
    public float attackRange = 1f;      // на якій відстані монстр атакує
    public int damage = 1;              // скільки шкоди завдає за удар
    public float attackCooldown = 1f;   // затримка між ударами (секунди)

    [Header("Недоторканність монстра після удару (щоб не бити по кілька разів за кадр)")]
    public float hitInvincibilityDuration = 0.2f;

    [Header("Блукання (коли гравець не в радіусі виявлення)")]
    public float wanderSpeed = 1f;              // швидкість руху під час блукання
    public float wanderMoveDuration = 1f;       // скільки секунд монстр рухається
    public float wanderPauseDuration = 1.5f;    // скільки секунд монстр стоїть між рухами

    [Header("Дроп монет")]
    public int minCoinDrop = 1;   // мінімум монет, що випадає зі смертю
    public int maxCoinDrop = 3;   // максимум монет (включно)
}