using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

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
    }

    [HarmonyPatch]
    public static class BetterKickPatches 
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterGrabbing), nameof(CharacterGrabbing.Update))]
        public static void Character_Update_Postfix(CharacterGrabbing __instance)
        {
            if (Input.GetKeyDown(Plugin.kickKeybind.Value) && !__instance.character.data.isKicking)
            {
                if (__instance.character.photonView.IsMine)
                {
                    __instance.character.photonView.RPC("RPCA_Kick", RpcTarget.All);
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
