// Інтерфейс для монстра, якого можна миттєво "відштовхнути" в певному напрямку -
// наприклад, коли гравець використовує активний артефакт "Ривок уперед" (DashForward)
// і на шляху трапляється ворог.
//
// На час staggerDuration монстр не керує власним рухом сам - його рухає лише поштовх,
// а звичайна логіка (переслідування, атака, блукання) призупиняється.
//
// Додай цей інтерфейс до свого MonsterController:
//
//   public class MonsterController : MonoBehaviour, IKnockbackable
//   {
//       private bool isStaggered = false;
//
//       public void Knockback(Vector2 direction, float force, float staggerDuration)
//       {
//           StartCoroutine(KnockbackRoutine(direction, force, staggerDuration));
//       }
//
//       private IEnumerator KnockbackRoutine(Vector2 direction, float force, float staggerDuration)
//       {
//           isStaggered = true;
//           rb.linearVelocity = direction.normalized * force;
//           yield return new WaitForSeconds(staggerDuration);
//           isStaggered = false;
//       }
//
//       // ...і на початку Update() зробити `if (isStaggered) return;` (після оновлення аніматора),
//       // щоб звичайна логіка руху не перебивала поштовх у той самий кадр.
//   }
//
// Без цього інтерфейсу артефакт "Ривок уперед" просто пройде крізь ворогів, не штовхаючи їх.
public interface IKnockbackable
{
    void Knockback(UnityEngine.Vector2 direction, float force, float staggerDuration);
}
