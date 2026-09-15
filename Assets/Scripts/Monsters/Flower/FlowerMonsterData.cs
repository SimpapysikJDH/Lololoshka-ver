using UnityEngine;

// Монстр, що сидить у землі і розквітає, коли гравець підходить близько
// Щоб створити: клік правою в Project -> Create -> Monsters -> New Flower Monster
[CreateAssetMenu(fileName = "New Flower Monster", menuName = "Monsters/New Flower Monster")]
public class FlowerMonsterData : MonsterData
{
    [Header("Квітка - виявлення гравця")]
    public float revealRange = 1.2f;   // на якій відстані квітка починає розкриватись
    public float hideRange = 1.8f;     // на якій відстані закривається назад (має бути >= revealRange, щоб не тремтіла на межі)

    [Header("Квітка - тривалість анімацій (секунди)")]
    public float openAnimDuration = 0.4f;   // скільки триває анімація "відкривання"
    public float closeAnimDuration = 0.4f;  // скільки триває анімація "закривання"

    [Header("Квітка - кулдаун")]
    public float reopenCooldown = 0.5f; // пауза після закриття, перш ніж квітка може знову відкритись
}