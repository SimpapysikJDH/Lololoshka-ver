// Реалізуй цей інтерфейс на будь-якому скрипті, що має вміти отримувати шкоду
// (MonsterController, FlowerMonsterController, і всі майбутні типи монстрів)
public interface IDamageable
{
    void TakeDamage(float amount);
}
