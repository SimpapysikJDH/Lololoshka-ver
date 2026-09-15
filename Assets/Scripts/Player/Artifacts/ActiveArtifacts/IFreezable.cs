// Інтерфейс для будь-якого монстра, якого можна "заморозити" (зупинити рух/атаки) на час.
// Додай цей інтерфейс до свого MonsterController (чи будь-якого скрипта, що керує рухом/атакою монстра):
//
//   public class MonsterController : MonoBehaviour, IFreezable
//   {
//       private bool isFrozen = false;
//
//       public void Freeze(float duration)
//       {
//           StartCoroutine(FreezeRoutine(duration));
//       }
//
//       private IEnumerator FreezeRoutine(float duration)
//       {
//           isFrozen = true;
//           // тут вимкни рух/атаку монстра (наприклад rb.linearVelocity = Vector2.zero; або прапорець isFrozen,
//           // який перевіряється на початку Update()/FixedUpdate())
//           yield return new WaitForSeconds(duration);
//           isFrozen = false;
//       }
//   }
//
// Без цього інтерфейсу на монстрах артефакт "Заморозка" нічого не робитиме -
// ActiveArtifactData шукає саме IFreezable через GetComponent.
public interface IFreezable
{
    void Freeze(float duration);
}
