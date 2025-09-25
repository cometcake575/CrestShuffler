using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;

namespace HeartlessBellhome;

[BepInPlugin("com.cometcake575.heartlessbellhome", "Heartless Bellhome", "1.0.0")]
public class HeartlessBellhomePlugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;
    
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo("Heartless Bellhome has loaded!");

        _ = new Hook(typeof(PlayRandomAudioEvent).GetMethod("Awake",
                BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<PlayRandomAudioEvent> orig, PlayRandomAudioEvent self) =>
            {
                orig(self);

                if (self.gameObject is { name: "heartbeat_audio", scene.name: "Belltown_Room_Spare" })
                {
                    DestroyImmediate(self);
                }
            });
    }
}
