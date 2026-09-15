using Photon.Pun;
using UnityEngine;


public class TEST : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log(PhotonNetwork.IsConnected);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
