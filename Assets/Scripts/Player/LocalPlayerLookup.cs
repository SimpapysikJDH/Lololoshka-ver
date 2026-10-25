using Photon.Pun;
using UnityEngine;

public static class LocalPlayerLookup
{
    public static PlayerStats FindStats()
    {
        foreach (PlayerStats stats in Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            PhotonView view = stats.GetComponent<PhotonView>();
            if (view != null && view.IsMine) return stats;
        }
        return null;
    }
}
