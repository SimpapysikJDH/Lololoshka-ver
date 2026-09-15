using UnityEngine;

// Дані конкретно для слайма (заряд + ривок).
// Успадковано від MonsterData - тому maxHP, moveSpeed, detectionRange, attackRange,
// damage, attackCooldown, hitInvincibilityDuration, wander* - все звідти вже є.
// Щоб створити: клік правою в Project -> Create -> Monsters -> New Slime
[CreateAssetMenu(fileName = "New Slime", menuName = "Monsters/New Slime")]
public class SlimeData : MonsterData
{
    [Header("Заряд перед ривком")]
    public float chargeDuration = 0.6f;      // скільки секунд слайм "трясеться" на місці перед ривком
    public bool trackPlayerDuringCharge = true; // чи довзволяти слайму підправляти напрямок, поки він заряджається

    [Header("Ривок")]
    public float dashSpeed = 8f;             // швидкість під час ривка
    public float dashDuration = 0.4f;        // скільки секунд триває сам ривок (якщо не впреться в стіну раніше)

    [Header("Перезарядка після ривка")]
    public float postDashCooldown = 0.8f;    // пауза після ривка, перед тим як знову можна заряджатись
}