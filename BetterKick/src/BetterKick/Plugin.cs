using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

namespace BetterKick
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static ConfigEntry<KeyCode> kickKeybind;
        public const string MOD_KEY = "BetterKick_Installed";

        private void Awake()
        {
            Log = Logger;
            Harmony harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");

            kickKeybind = Config.Bind("General", "Kick Keybind", KeyCode.V, "The keybind for kicking.");
        }

        public static void SetModStatus()
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
            {
                PhotonHashtable props = new PhotonHashtable { { MOD_KEY, true } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                Log.LogInfo("Sent BetterKick status to the room.");
            }
        }

        public static bool AllPlayersHaveMod()
        {
            if (!PhotonNetwork.InRoom) return true;

            foreach (PhotonPlayer p in PhotonNetwork.PlayerList)
            {
                if (p.IsLocal) continue;

                // If the property doesn't exist on a remote player, they don't have the mod
                if (p.CustomProperties == null || !p.CustomProperties.ContainsKey(MOD_KEY))
                {
                    Log.LogWarning($"Player {p.NickName} is missing the mod.");
                    return false;
                }
            }
            return true;
        }

        public static void ShowWarning(string message)
        {
            var guiManager = GameObject.FindFirstObjectByType<GUIManager>();
            if (guiManager == null) return;

            GameObject canvasObj = new GameObject("BetterKick_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject container = new GameObject("BetterKickContainer");
            container.transform.SetParent(canvasObj.transform, false);
            CanvasGroup group = container.AddComponent<CanvasGroup>();

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 70);
            rect.sizeDelta = new Vector2(800, 100);

            GameObject textObj = new GameObject("WarningText");
            textObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();

            tmp.text = message.ToUpper();
            tmp.font = guiManager.heroDayText.font;
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.3f, 0.3f);

            guiManager.StartCoroutine(FadeAlertUI(group, canvasObj));
        }

        private static IEnumerator FadeAlertUI(CanvasGroup group, GameObject fullCanvas)
        {
            float elapsed = 0f;
            while (elapsed < 0.3f) { elapsed += Time.deltaTime; group.alpha = elapsed / 0.3f; yield return null; }
            yield return new WaitForSeconds(2.5f);
            elapsed = 0f;
            while (elapsed < 0.3f) { elapsed += Time.deltaTime; group.alpha = 1f - (elapsed / 0.3f); yield return null; }
            Destroy(fullCanvas);
        }
    }

    [HarmonyPatch]
    public static class BetterKickPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), nameof(MonoBehaviourPunCallbacks.OnJoinedRoom))]
        public static void OnJoinedRoom()
        {
            Plugin.SetModStatus();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), nameof(MonoBehaviourPunCallbacks.OnPlayerEnteredRoom))]
        public static void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
        {
            Plugin.SetModStatus();
            Plugin.Log.LogInfo($"{newPlayer.NickName} joined. Re-syncing mod status.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterGrabbing), nameof(CharacterGrabbing.Update))]
        public static void Character_Update_Postfix(CharacterGrabbing __instance)
        {
            if (!__instance.character.IsLocal || GUIManager.instance.windowBlockingInput) return;

            if (Input.GetKeyDown(Plugin.kickKeybind.Value) && !__instance.character.data.isKicking)
            {
                if (Plugin.AllPlayersHaveMod())
                {
                    if (__instance.character.photonView.IsMine)
                    {
                        var character = __instance.character;
                        if (!character.data.isClimbingAnything && character.data.isGrounded && !character.OutOfRegularStamina())
                        {
                            character.data.isKicking = true;
                            __instance._kickTime = 0f;
                            character.photonView.RPC("RPCA_Kick", RpcTarget.All);
                        }
                    }
                }
                else
                {
                    Plugin.ShowWarning("Cannot kick: Someone is missing the BetterKick mod.");
                }
            }

            if (!__instance.isKickMode && __instance.character.data.isKicking)
            {
                __instance._kickTime += Time.deltaTime;
                if (__instance._kickTime > 1f)
                {
                    __instance.character.data.isKicking = false;
                    __instance._kickTime = 0f;
                }
            }
        }
    }
}