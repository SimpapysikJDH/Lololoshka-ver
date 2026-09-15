// Інтерфейс для будь-якого монстра, якого можна "сповільнити" на час.
// На відміну від IFreezable (повна зупинка), тут монстр і далі рухається й атакує,
// просто повільніше - slowMultiplier застосовується до його moveSpeed/wanderSpeed/dashSpeed.
//
// Додай цей інтерфейс до свого MonsterController (чи будь-якого скрипта, що керує рухом монстра):
//
//   public class MonsterController : MonoBehaviour, ISlowable
//   {
//       private float speedMultiplier = 1f;
//       private Coroutine slowRoutine;
//
//       public void Slow(float slowMultiplier, float duration)
//       {
//           if (slowRoutine != null)
//               StopCoroutine(slowRoutine);
//           slowRoutine = StartCoroutine(SlowRoutine(slowMultiplier, duration));
//       }
//
//       private IEnumerator SlowRoutine(float multiplier, float duration)
//       {
//           speedMultiplier = Mathf.Clamp01(multiplier);
//           yield return new WaitForSeconds(duration);
//           speedMultiplier = 1f;
//           slowRoutine = null;
//       }
//
//       // ...і множити на speedMultiplier там, де рахується rb.linearVelocity
//   }
//
// Без цього інтерфейсу на монстрах артефакт "Сповільнення" нічого не робитиме -
// ActiveArtifactData шукає саме ISlowable через GetComponent.
public interface ISlowable
{
    // slowMultiplier - множник швидкості на час дії (0.5 = вдвічі повільніше, 0.25 = майже стоп).
    // Очікується діапазон (0..1]. Якщо викликати повторно, поки попередній ефект ще діє -
    // найпростіша (і використана в наших контролерах) реалізація просто замінює його новим:
    // старіший ефект перериваться і почнеться відлік заново з новими параметрами.
    void Slow(float slowMultiplier, float duration);
}
