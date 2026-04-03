using Photon.Pun;
using UnityEngine;
using System.Collections.Generic;

namespace BetterKick
{
    public class Netcode : MonoBehaviourPun
    {
        private static Netcode _instance = null!;
        public PhotonView photonView = null!;

        public HashSet<int> PlayersWithMod = new HashSet<int>();

        public static Netcode Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject singleton = new GameObject("BetterKickNetcode");
                    _instance = singleton.AddComponent<Netcode>();
                    DontDestroyOnLoad(singleton);
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            photonView = GetComponent<PhotonView>();
            if (photonView == null)
            {
                photonView = gameObject.AddComponent<PhotonView>();
                photonView.ViewID = 5726;
            }
        }

        [PunRPC]
        public void RPC_RegisterModPresence(int playerID)
        {
            if (!PlayersWithMod.Contains(playerID))
            {
                PlayersWithMod.Add(playerID);
                Plugin.Log.LogInfo($"Player {playerID} verified with BetterKick.");
            }

            if (PhotonNetwork.IsMasterClient && playerID != PhotonNetwork.LocalPlayer.ActorNumber)
            {
                photonView.RPC("RPC_RegisterModPresence", RpcTarget.Others, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
    }
}