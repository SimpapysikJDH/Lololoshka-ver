using UnityEngine;
using Photon.Pun;

// Причепи на БУДЬ-ЯКИЙ порожній GameObject в сцені (наприклад назви "OfflineBootstrap").
// Це виправляє помилку "Cannot send messages when not connected", коли ти тестуєш
// сцену Shop напряму, без проходження через сцену підключення до Photon.
//
// Unity гарантує, що Awake() у ВСІХ скриптах сцени викликається РАНІШЕ,
// ніж Start() у БУДЬ-ЯКОМУ скрипті - тому цей код встигає ввімкнути офлайн-режим
// до того, як ShopManager.Start() спробує слати RPC.
//
// ВАЖЛИВО: коли будеш робити нормальний мультиплеєр через лобі - просто вимкни
// або видали цей об'єкт зі сцени, він потрібен лише для тестування наодинці.
public class OfflineTestBootstrap : MonoBehaviour
{
    void Awake()
    {
        if (!PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("TestRoom");
        }
    }
}
