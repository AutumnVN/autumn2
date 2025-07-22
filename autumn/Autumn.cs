using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace autumn;

public class Autumn : ModSystem
{
    static Config Config;
    static ICoreClientAPI capi;
    public override void StartClientSide(ICoreClientAPI capi)
    {
        Autumn.capi = capi;

        Config = capi.LoadModConfig<Config>("autumn.json") ?? new Config();
        capi.StoreModConfig(Config, "autumn.json");

        var harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();

        if (Config.HealthNameTag)
        {
            capi.Event.RegisterRenderer(new Utils.NameTagRenderer(capi), EnumRenderStage.Ortho);
        }
    }

    // ExtendInfoHudRange
    [HarmonyPatch(typeof(HudElementBlockAndEntityInfo), "Every15ms")]
    class HudElementBlockAndEntityInfo_Every15ms
    {
        static bool Prefix(HudElementBlockAndEntityInfo __instance)
        {
            if (!Config.ExtendInfoHudRange) return true;

            if (!__instance.IsOpened()) return false;

            var player = capi.World.Player;
            var eyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);
            float pitch = player.Entity.Pos.Pitch;
            float yaw = player.Entity.Pos.Yaw;
            float range = 128f;
            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            bool renderMeta = ClientSettings.RenderMetaBlocks;
            bool bfilter(BlockPos pos, Block block) => block == null || renderMeta || block.RenderPass != EnumChunkRenderPass.Meta || (block.GetInterface<IMetaBlock>(capi.World, pos)?.IsSelectable(pos) ?? false);
            bool efilter(Entity e) => e.IsInteractable && e.EntityId != capi.World.Player.Entity.EntityId;

            capi.World.RayTraceForSelection(eyePos, pitch, yaw, range, ref blockSel, ref entitySel, bfilter, efilter);

            var trv = Traverse.Create(__instance);

            if (entitySel != null && entitySel.Entity != null)
            {
                trv.Field("currentEntity").SetValue(entitySel.Entity);
                trv.Field("currentBlock").SetValue(null);
                trv.Method("ComposeBlockInfoHud").GetValue();
            }
            else if (blockSel != null)
            {
                Block block = capi.World.BlockAccessor.GetBlock(blockSel.Position);
                trv.Field("currentBlock").SetValue(block);
                trv.Field("currentPos").SetValue(blockSel.Position);
                trv.Field("currentSelectionIndex").SetValue(blockSel.SelectionBoxIndex);
                trv.Field("currentEntity").SetValue(null);
                trv.Method("ComposeBlockInfoHud").GetValue();
            }
            else
            {
                trv.Field("currentBlock").SetValue(null);
                trv.Field("currentEntity").SetValue(null);
                trv.Method("ComposeBlockInfoHud").GetValue();
            }

            return false;
        }
    }

    // HealthInfo
    [HarmonyPatch(typeof(Entity), "GetInfoText")]
    class Entity_GetInfoText
    {
        static void Postfix(Entity __instance, ref string __result)
        {
            if (!Config.HealthInfo) return;

            var healthTree = __instance.WatchedAttributes.GetTreeAttribute("health");
            if (healthTree != null)
            {
                float health = healthTree.GetFloat("currenthealth", 0);
                float maxHealth = healthTree.GetFloat("maxhealth", 0);

                if (health == 0 || maxHealth == 0) return;

                var sb = new StringBuilder(__result);
                sb.AppendLine($"Health: {health:0}/{maxHealth:0}");
                __result = sb.ToString();
            }
        }
    }

    // HealthNameTag
    [HarmonyPatch(typeof(EntityBehaviorNameTag), "OnRenderFrame")]
    class EntityBehaviorNameTag_OnRenderFrame
    {
        static bool Prefix(EntityBehaviorNameTag __instance)
        {
            if (!Config.HealthNameTag) return true;

            Entity entity = __instance.entity;

            if (entity != Utils.GetTargetedEntity(capi) && entity is not EntityPlayer) return true;

            var healthTree = entity.WatchedAttributes.GetTreeAttribute("health");

            if (healthTree == null) return true;

            float health = healthTree.GetFloat("currenthealth", 0);

            if (health <= 0) return true;

            string text = $"{entity.GetName()} {health:0}♥";

            Utils.RenderNameTag(capi, entity, text);

            return false;
        }
    }

    // ThirdPersonNoClip
    [HarmonyPatch(typeof(Camera), "LimitThirdPersonCameraToWalls")]
    class Camera_LimitThirdPersonCameraToWalls
    {
        static void Postfix(Camera __instance, ref bool __result)
        {
            if (!Config.ThirdPersonNoClip) return;

            __result = true;
        }
    }
}
