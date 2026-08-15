using HarmonyLib;
using RoR2;

namespace LovenseRoR2;

[HarmonyPatch(typeof(Stage), "Start")]
class PatchStageStart
{
    static void Postfix() => LovensePlugin.ResetSources();
}

[HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamage))]
class PatchTakeDamage
{
    static void Postfix(HealthComponent __instance, DamageInfo damageInfo)
    {
        var localUser = LocalUserManager.GetFirstLocalUser();
        if (localUser?.cachedBody != __instance.body) return;

        bool isDot = damageInfo.dotIndex != DotController.DotIndex.None;
        LovensePlugin.OnDamage(damageInfo.damage, __instance.fullCombinedHealth, isDot);
    }
}

[HarmonyPatch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterDeath))]
class PatchOnKill
{
    static void Postfix(DamageReport damageReport)
    {
        if (damageReport == null) return;
        if (damageReport.attackerTeamIndex != TeamIndex.Player) return;
        if (damageReport.victimTeamIndex   == TeamIndex.Player) return;

        LovensePlugin.OnKill();
    }
}

[HarmonyPatch(typeof(GenericPickupController), "AttemptGrant")]
class PatchOnPickup
{
    static void Postfix(GenericPickupController __instance, CharacterBody body)
    {
        var localUser = LocalUserManager.GetFirstLocalUser();
        if (localUser?.cachedBody != body) return;

        var def = PickupCatalog.GetPickupDef(__instance.pickup.pickupIndex);
        if (def == null) return;

        LovensePlugin.OnItemPickup(def.itemTier);
    }
}
