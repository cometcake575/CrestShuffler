using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;

namespace NoWingsDip;

[BepInPlugin("com.cometcake575.nowingsdip", "No Wings Dip", "1.0.0")]
public class NoWingsDipPlugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;
    
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo("No Wings Dip has loaded!");
        
        var doubleJumpSteps = typeof(HeroController).GetField("doubleJump_steps",
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        _ = new Hook(typeof(HeroController).GetMethod("DoubleJump",
                BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<HeroController> orig, HeroController self)
                =>
            {
                if ((int) doubleJumpSteps!.GetValue(self) <= self.DOUBLE_JUMP_FALL_STEPS) 
                    doubleJumpSteps.SetValue(self, self.DOUBLE_JUMP_FALL_STEPS + 1);
                orig(self);
            });
    }
}
