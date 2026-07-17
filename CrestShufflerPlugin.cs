using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using GlobalEnums;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CrestShuffler;

[BepInPlugin("com.cometcake575.crestshuffler", "Crest Shuffler", "1.2.1")]
public class CrestShufflerPlugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;

    private static int _rerollTime;
    private static float _timeUntilReroll;
    private static ToolMode _toolShuffle;
    private static string[] _crestBlacklist;
    private static CrestMode _crestMode;

    private static bool CrestAllowed(ToolCrest crest)
    {
        return _crestMode switch
        {
            CrestMode.BaseOnly => crest.IsBaseVersion,
            CrestMode.HighestUnlocked => !crest.IsUpgradedVersionUnlocked &&
                                         (crest.IsUnlocked || crest.IsBaseVersion),
            _ => true
        };
    }

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo("Crest Shuffler has loaded!");
        
        _ = new Hook(typeof(InventoryToolCrest).GetProperty(nameof(InventoryToolCrest.IsHidden))!.GetGetMethod(),
            (Func<InventoryToolCrest, bool> _, InventoryToolCrest _) => true);

        _rerollTime = Config.Bind("Options", "Reroll Time", 60, 
            "How often the crest should be rerolled in seconds (-1 to disable the timer)").Value;
        _timeUntilReroll = _rerollTime;

        _crestMode = Config.Bind("Options", "Hunters Crest Mode", CrestMode.All,
            "How to handle hunters crest.\n" +
            "'All' shuffles every tier (original behavior).\n" +
            "'BaseOnly' only rolls base versions.\n" +
            "'HighestUnlocked' only rolls the highest tier you have unlocked.").Value;

        _crestBlacklist = Config.Bind("Options", "Crest Blacklist", "",
                "Comma-separated crest IDs to exclude from the shuffle.\n" +
                "Valid IDs: Hunter, Hunter_v2, Hunter_v3, Reaper, Wanderer, Warrior, Witch, Toolmaster, Spell, Cursed, Cloakless").Value
            .Split(',').Select(id => id.Trim()).Where(id => id.Length > 0).ToArray();

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

        if (Config.Bind("Options", "Reroll on damage", false,
                "Whether to reroll the crest when taking damage").Value)
        {
            _ = new Hook(typeof(HeroController).GetMethod(nameof(HeroController.TakeDamage)),
                (Action<HeroController, GameObject, CollisionSide, int, HazardType, DamagePropertyFlags> orig, 
                    HeroController self, 
                    GameObject go, 
                    CollisionSide damageSide, 
                    int damageAmount, 
                    HazardType hazardType, 
                    DamagePropertyFlags damagePropertyFlags) =>
                {
                    if (self.CanTakeDamage()) StartCoroutine(RandomCrest());
                    orig(self, go, damageSide, damageAmount, hazardType, damagePropertyFlags);
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
        yield return new WaitForSeconds(0.05f);
        var hc = HeroController.instance;
        yield return new WaitUntil(() => !hc.controlReqlinquished && 
                                         !hc.cState.dashing && 
                                         !hc.cState.downAttacking && 
                                         !hc.cState.downSpikeAntic && 
                                         !hc.cState.airDashing);

        var crests = ToolItemManager.GetAllCrests()
            .Where(crest => crest.name != PlayerData.instance.CurrentCrestID &&
                            !_crestBlacklist.Contains(crest.name, StringComparer.OrdinalIgnoreCase) &&
                            CrestAllowed(crest))
            .ToList();

        if (crests.Count == 0)
        {
            Logger.LogWarning("No crests available after blacklist and crest mode filtering, skipping reroll.");
            yield break;
        }

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

    private enum CrestMode
    {
        All,
        BaseOnly,
        HighestUnlocked
    }
}