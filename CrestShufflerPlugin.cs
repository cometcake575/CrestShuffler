using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CrestShuffler;

[BepInPlugin("com.cometcake575.crestshuffler", "Crest Shuffler", "1.0.4")]
public class CrestShufflerPlugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;

    private static int _rerollTime;
    private static float _timeUntilReroll;
    private static ToolMode _toolShuffle;
    
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo("Crest Shuffler has loaded!");
        
        _ = new Hook(typeof(InventoryToolCrest).GetProperty(nameof(InventoryToolCrest.IsHidden))!.GetGetMethod(),
            (Func<InventoryToolCrest, bool> _, InventoryToolCrest _) => true);

        _rerollTime = Config.Bind("Options", "Reroll Time", 60, 
            "How often the crest should be rerolled in seconds (-1 to disable the timer)").Value;
        _timeUntilReroll = _rerollTime;

        if (Config.Bind("Options", "Reroll on transition", false,
                "Whether to reroll the crest when going through a scene transition").Value)
        {
            _ = new Hook(typeof(HeroController).GetMethod(nameof(HeroController.SceneInit)),
                (Action<HeroController> orig, HeroController self) =>
                {
                    orig(self);
                    StartCoroutine(RandomCrest());
                });
        }

        if (Config.Bind("Options", "Reroll on death", false,
                "Whether to reroll the crest when dying").Value)
        {
            _ = new Hook(typeof(HeroController).GetMethod("Awake", 
                    BindingFlags.NonPublic | BindingFlags.Instance),
                (Action<HeroController> orig, HeroController self) =>
                {
                    orig(self);
                    self.OnDeath += () => StartCoroutine(RandomCrest());
                });
        }

        _toolShuffle = Config.Bind("Options", "Shuffle Tools", ToolMode.Off,
            "Whether to shuffle tools as well as the crest.\n" +
            "'Unlocked' will give the player random tools they have unlocked.\n" +
            "'All' will give the player random tools, including ones they do not have.").Value;
    }

    private void Update()
    {
        if (!HeroController.instance) return;
        if (_rerollTime > 0)
        {
            _timeUntilReroll -= Time.deltaTime;
            if (_timeUntilReroll <= 0)
            {
                StartCoroutine(RandomCrest());
                _timeUntilReroll += _rerollTime;
            }
        }
    }

    private static IEnumerator RandomCrest()
    {
        var hc = HeroController.instance;
        yield return new WaitUntil(() => !hc.controlReqlinquished && 
                                         !hc.cState.dashing && 
                                         !hc.cState.downAttacking && 
                                         !hc.cState.downSpikeAntic && 
                                         !hc.cState.airDashing);

        var crests = ToolItemManager.GetAllCrests()
            .Where(crest => crest.name != PlayerData.instance.CurrentCrestID).ToList();
        var crest = crests[Random.Range(0, crests.Count)];
        PlayerData.instance.IsCurrentCrestTemp = true;
        ToolItemManager.AutoEquip(crest, false, false);
        HeroController.instance.UpdateSilkCursed();

        if (_toolShuffle != ToolMode.Off)
        {
            var tools = (_toolShuffle == ToolMode.Unlocked ? 
                ToolItemManager.GetUnlockedTools() : 
                ToolItemManager.GetAllTools()).ToList();
            
            for (var i = 0; i < 30; i++) ToolItemManager.AutoEquip(tools[Random.Range(0, tools.Count)]);
        }

        yield return null;
        hc.RegainControl();
        hc.StartAnimationControl();
    }

    // ReSharper disable once UnusedMember.Local
    private enum ToolMode
    {
        Off,
        Unlocked,
        All
    }
}
