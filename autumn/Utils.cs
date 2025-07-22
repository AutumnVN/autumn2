using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace autumn;

public class Utils
{
    class TimedEntity
    {
        public long time;
        public Entity entity;

        public TimedEntity(Entity entity)
        {
            this.entity = entity;
            time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    static TimedEntity recentTargetedEntity;

    public static Entity GetTargetedEntity(ICoreClientAPI capi)
    {
        var player = capi.World.Player;
        var eyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);
        float pitch = player.Entity.Pos.Pitch;
        float yaw = player.Entity.Pos.Yaw;
        float range = 128f;
        BlockSelection blockSel = null;
        EntitySelection entitySel = null;

        capi.World.RayTraceForSelection(eyePos, pitch, yaw, range, ref blockSel, ref entitySel);

        if (entitySel != null && entitySel.Entity != null)
        {
            recentTargetedEntity = new TimedEntity(entitySel.Entity);
        }

        if (recentTargetedEntity != null && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - recentTargetedEntity.time > 3000)
        {
            recentTargetedEntity = null;
        }

        return recentTargetedEntity?.entity;
    }

    public class NameTagRenderer : IRenderer
    {
        public double RenderOrder => 1.0;
        public int RenderRange => 999;
        readonly ICoreClientAPI capi;

        public NameTagRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            Entity entity = GetTargetedEntity(capi);

            if (entity == null) return;

            var healthTree = entity.WatchedAttributes.GetTreeAttribute("health");

            if (healthTree == null) return;

            float health = healthTree.GetFloat("currenthealth", 0);

            if (health <= 0) return;

            string text = $"{entity.GetName()} {health:0}♥";

            RenderNameTag(capi, entity, text);
        }
        public void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }
    }

    public static void RenderNameTag(ICoreClientAPI capi, Entity entity, string text)
    {
        if (entity.EntityId == capi.World.Player.Entity.EntityId && capi.Render.CameraType == EnumCameraMode.FirstPerson) return;

        ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("nametag");
        if (treeAttribute == null)
        {
            entity.WatchedAttributes.SetAttribute("nametag", treeAttribute = new TreeAttribute());
            treeAttribute.SetString("name", text);
            treeAttribute.SetInt("showtagonlywhentargeted", 0);
            treeAttribute.SetInt("renderRange", 999);
            entity.WatchedAttributes.MarkPathDirty("nametag");
        }

        LoadedTexture nameTagTexture = capi.Gui.TextTexture.GenUnscaledTextTexture(Lang.GetIfExists("nametag-" + text.ToLowerInvariant()) ?? text, CairoFont.WhiteMediumText().WithColor(ColorUtil.WhiteArgbDouble), new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 3,
            Radius = GuiStyle.ElementBGRadius,
            Shade = true,
            BorderColor = GuiStyle.DialogBorderColor,
            BorderWidth = 3.0
        });

        IRenderAPI render = capi.Render;
        EntityPlayer entityPlayer = capi.World.Player.Entity;

        if (entity.Properties.Client.Renderer is not EntityShapeRenderer entityShapeRenderer) return;

        Vec3d vec3d = MatrixToolsd.Project(entityShapeRenderer.getAboveHeadPosition(entityPlayer), render.PerspectiveProjectionMat, render.PerspectiveViewMat, render.FrameWidth, render.FrameHeight);

        if (vec3d.Z < 0.0) return;

        float val = 4f / Math.Max(1f, (float)vec3d.Z);
        float num = Math.Min(1f, val);

        num = Math.Max(num, 0.5f);

        if (num > 0.75f)
        {
            num = 0.75f + (num - 0.75f) / 2f;
        }

        int renderRange = 999;
        double num3 = entityPlayer.Pos.SquareDistanceTo(entity.Pos);

        if (nameTagTexture == null || renderRange * renderRange <= num3) return;

        float posX = (float)vec3d.X - num * nameTagTexture.Width / 2f;
        float posY = render.FrameHeight - (float)vec3d.Y - nameTagTexture.Height * Math.Max(0f, num);
        render.Render2DTexture(nameTagTexture.TextureId, posX, posY, num * nameTagTexture.Width, num * nameTagTexture.Height, 20f);
    }
}
