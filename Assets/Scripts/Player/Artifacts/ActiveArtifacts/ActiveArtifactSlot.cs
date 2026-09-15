using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Причепи на GameObject гравця (той самий, де PlayerStats).
// Тримає ОДИН активний (розхідний) артефакт. Новий підбір (через ActiveArtifactPickup)
// витісняє старий - старий викидається на землю у вигляді того ж префабу підбору.
//
// Кулдаун рахується не в секундах, а в кількості ЗАЧИЩЕНИХ кімнат (RoomController.OnAnyRoomCleared) -
// тобто гравцю треба реально повбивати монстрів у стількох кімнатах, скільки вказано в cooldownRooms,
// перш ніж зможе активувати артефакт знову.
[RequireComponent(typeof(PlayerStats))]
public class ActiveArtifactSlot : MonoBehaviour
{
    [Header("Поточний активний артефакт (заповнюється автоматично при підборі)")]
    public ActiveArtifactData currentArtifact;

    [Header("Клавіша активації")]
    public Key activateKey = Key.Q;

    [Header("Заряди / кулдаун (тільки для перегляду в інспекторі, не редагуй руками)")]
    public int chargesLeft = 0;
    public int roomsUntilReady = 0;

    // Підписуйся в UI-скрипті (ActiveArtifactUI), щоб оновлювати іконку внизу екрану.
    // artifact == null означає, що слот порожній.
    public event Action<ActiveArtifactData, int> OnArtifactChanged; // (артефакт, залишок зарядів)
    public event Action<int> OnCooldownChanged;                     // залишок кімнат до готовності
    public event Action OnArtifactUsed;

    void OnEnable()
    {
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
    }

    void Update()
    {
        if (currentArtifact == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[activateKey].wasPressedThisFrame)
        {
            TryActivate();
        }
    }

    // Викликається з ActiveArtifactPickup при підборі нового артефакту "з нуля" (лежав на рівні).
    // Такий артефакт завжди дається з повними зарядами і без кулдауну.
    // Повертає артефакт, який лежав у слоті ДО цього (щоб пікап міг викинути його на землю),
    // або null, якщо слот був порожній.
    public ActiveArtifactData Equip(ActiveArtifactData newArtifact)
    {
        return Equip(newArtifact, newArtifact != null ? newArtifact.charges : 0, 0);
    }

    // Перевантаження для випадку, коли підбирається артефакт, що вже мав власний стан
    // (наприклад, гравець сам викинув його раніше, замінивши іншим). ВАЖЛИВО:
    // без цього перевантаження гравець міг би нескінченно "абузити" систему,
    // перекидаючи два артефакти туди-сюди і щоразу отримуючи повні заряди й нульовий кулдаун -
    // ось чому ActiveArtifactPickup передає сюди збережений chargesLeft/roomsUntilReady
    // для артефактів, які самі були викинуті зі слота, а не знайдені вперше на рівні.
    public ActiveArtifactData Equip(ActiveArtifactData newArtifact, int startCharges, int startRoomsUntilReady)
    {
        ActiveArtifactData previous = currentArtifact;

        currentArtifact = newArtifact;
        chargesLeft = startCharges;
        roomsUntilReady = startRoomsUntilReady;

        OnArtifactChanged?.Invoke(currentArtifact, chargesLeft);
        OnCooldownChanged?.Invoke(roomsUntilReady);

        return previous;
    }

    public bool CanActivate()
    {
        return currentArtifact != null && chargesLeft > 0 && roomsUntilReady <= 0;
    }

    public void TryActivate()
    {
        if (!CanActivate()) return;

        currentArtifact.Activate(gameObject, this);

        chargesLeft--;
        roomsUntilReady = currentArtifact.cooldownRooms;

        OnArtifactUsed?.Invoke();
        OnCooldownChanged?.Invoke(roomsUntilReady);

        if (chargesLeft <= 0)
        {
            // Заряди скінчились - слот звільняється
            currentArtifact = null;
            OnArtifactChanged?.Invoke(null, 0);
        }
        else
        {
            OnArtifactChanged?.Invoke(currentArtifact, chargesLeft);
        }
    }

    private void HandleRoomCleared()
    {
        if (currentArtifact == null || roomsUntilReady <= 0) return;

        roomsUntilReady--;
        OnCooldownChanged?.Invoke(roomsUntilReady);
    }
}