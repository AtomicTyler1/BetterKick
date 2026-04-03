using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

namespace BetterKick
{
    [BepInAutoPlugin]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; } = null!;
        public static Harmony harmony = null!;

        public static ConfigEntry<KeyCode> kickKeybind;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            harmony.PatchAll();
            Log.LogInfo($"Plugin {Name} is loaded!");

            kickKeybind = Config.Bind("General", "Kick Keybind", KeyCode.V, "The keybind for kicking.");
        }

        public static void ShowWarning(string message, bool ignoreUserConfig = false)
        {
            var guiManager = GameObject.FindFirstObjectByType<GUIManager>();
            if (guiManager == null) return;

            var font = guiManager.heroDayText.font;

            GameObject canvasObj = new GameObject("BetterKick_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject container = new GameObject("BetterKickContainer");
            container.transform.SetParent(canvasObj.transform, false);

            CanvasGroup group = container.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);
            rect.sizeDelta = new Vector2(-40, 60);

            GameObject textObj = new GameObject("WarningText");
            textObj.transform.SetParent(container.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = message.ToUpper();
            tmp.font = font;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            tmp.color = new Color(1f, 0.2f, 0.2f);
            tmp.outlineColor = new Color(0.1f, 0f, 0f);
            tmp.outlineWidth = 0.08f;

            guiManager.StartCoroutine(FadeAlertUI(group, canvasObj, message));
        }

        private static IEnumerator FadeAlertUI(CanvasGroup group, GameObject fullCanvas, string message)
        {
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                group.alpha = elapsed / 0.5f;
                yield return null;
            }

            yield return new WaitForSeconds(4f);

            elapsed = 0f;
            while (elapsed < 1f)
            {
                elapsed += Time.deltaTime;
                group.alpha = 1f - (elapsed / 1f);
                yield return null;
            }

            Destroy(fullCanvas);
        }
    }

    [HarmonyPatch]
    public static class BetterKickPatches
    {
        private static bool AllClientsHaveMod =>
            Netcode.Instance.PlayersWithMod.Count >= PhotonNetwork.CurrentRoom.PlayerCount;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRun))]
        public static void OnRunStart()
        {
            Netcode.Instance.PlayersWithMod.Clear();

            Netcode.Instance.PlayersWithMod.Add(PhotonNetwork.LocalPlayer.ActorNumber);

            if (PhotonNetwork.InRoom)
            {
                Netcode.Instance.photonView.RPC("RPC_RegisterModPresence", RpcTarget.Others, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterGrabbing), nameof(CharacterGrabbing.Update))]
        public static void Character_Update_Postfix(CharacterGrabbing __instance)
        {
            if (!__instance.character.IsLocal || GUIManager.instance.windowBlockingInput) return;

            if (Input.GetKeyDown(Plugin.kickKeybind.Value) && !__instance.character.data.isKicking)
            {
                if (AllClientsHaveMod || PhotonNetwork.CurrentRoom.PlayerCount <= 1)
                {
                    if (__instance.character.photonView.IsMine)
                    {
                        var character = __instance.character;
                        if (!character.data.isClimbingAnything && character.data.isGrounded && !character.OutOfRegularStamina())
                        {
                            Plugin.Log.LogInfo($"Kicking!");

                            character.data.isKicking = true;
                            __instance._kickTime = 0f;

                            character.photonView.RPC("RPCA_Kick", RpcTarget.All);
                        }
                    }
                }
                else
                {
                    Plugin.ShowWarning("Cannot kick: Not all players have the BetterKick mod installed.");
                    Plugin.Log.LogWarning("Cannot kick: Not all players have the BetterKick mod installed.");
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
