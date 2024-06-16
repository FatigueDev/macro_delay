﻿using System;
using System.Linq.Expressions;
using System.Numerics;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace thirdperson_crosshair;


public class ThirdpersonCrosshair : ModSystem
{
    private ICoreAPI capi;
    private readonly ThirdpersonCrosshairPatch thirdpersonCrosshairPatch = new ThirdpersonCrosshairPatch();

    public override bool ShouldLoad(EnumAppSide side)
    {
        return side == EnumAppSide.Client;
    }

    public override void Start(ICoreAPI api)
    {
        capi = api;
        base.Start(api);
        thirdpersonCrosshairPatch.OverwriteNativeFunctions(api);
    }

    public override void Dispose()
    {
        thirdpersonCrosshairPatch.overwriter.UnpatchAll();
    }
}

[HarmonyPatchCategory("thirdperson_crosshair")]
internal class ThirdpersonCrosshairPatch
{

    private static ICoreClientAPI clapi;
    public Harmony overwriter;

    public void OverwriteNativeFunctions(ICoreAPI coreAPI)
    {
        clapi = (ICoreClientAPI)coreAPI;
        if (!Harmony.HasAnyPatches("thirdperson_crosshair"))
        {
            overwriter = new Harmony("thirdperson_crosshair");
            overwriter.PatchAll();
            clapi.Logger.Event("Thirdperson Crosshair patched in");
        }
		else
		{
			clapi.Logger.Error("Failed to patch in this god forsaken thirdperson crosshair.");
		}
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SystemRenderAim), "DrawAim")]
    public static bool DrawAim(SystemRenderAim __instance, ref int ___aimTextureId, ref int ___aimHostileTextureId, ref ClientMain game)
    {
        var gameTraverse = Traverse.Create(game);
		PlayerCamera mainCamera = gameTraverse.Field("MainCamera").GetValue<PlayerCamera>();
		EnumCameraMode cameraMode = gameTraverse.Field("MainCamera").Field("CameraMode").GetValue<EnumCameraMode>();

        if (cameraMode != EnumCameraMode.ThirdPerson)
		{
			return true;
		}

		int aimwidth = 32;
		int aimheight = 32;

        if(game.EntityPlayer == null)
        {
            return false;
        }

        BlockSelection blockSelection = game.EntityPlayer.BlockSelection;
        EntitySelection entitySelection = game.EntityPlayer.EntitySelection;

		ItemStack heldStack = game.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
		float attackRange = heldStack?.Collectible.GetAttackRange(heldStack) ?? GlobalConstants.DefaultAttackRange;
		int texId = ___aimTextureId;

        if(blockSelection != null && game.EntityPlayer != null)
        {
            Vec3d hitScreenPos = PosToScreen(clapi, Vec3d.Add(blockSelection.Position.ToVec3d(), blockSelection.HitPosition));
            game.Render2DTexture(texId, (float)hitScreenPos.X - (aimwidth / 2), (float)hitScreenPos.Y - (aimheight / 2), aimwidth, aimheight, 10000f);
        }
		else if (entitySelection != null && game.EntityPlayer != null)
		{
			Cuboidd cuboidd = entitySelection.Entity.SelectionBox.ToDouble().Translate(entitySelection.Position.X, entitySelection.Position.Y, entitySelection.Position.Z);
			EntityPos pos = game.EntityPlayer.SidedPos;
			if (cuboidd.ShortestDistanceFrom(pos.X + game.EntityPlayer.LocalEyePos.X, pos.Y + game.EntityPlayer.LocalEyePos.Y, pos.Z + game.EntityPlayer.LocalEyePos.Z) <= (double)attackRange - 0.08)
			{
				texId = ___aimHostileTextureId;
			}

            Vec3d hitScreenPos = PosToScreen(clapi, Vec3d.Add(entitySelection.Position, entitySelection.HitPosition));
            game.Render2DTexture(texId, (float)hitScreenPos.X - (aimwidth / 2), (float)hitScreenPos.Y - (aimheight / 2), aimwidth, aimheight, 10000f);
		}
        
        return false;
    }

    public static Vec3d PosToScreen(ICoreClientAPI capi, Vec3d pos)
    {
        IRenderAPI rpi = capi.Render;
        pos = MatrixToolsd.Project(pos, rpi.PerspectiveProjectionMat, rpi.PerspectiveViewMat, rpi.FrameWidth, rpi.FrameHeight);
        pos.Y = rpi.FrameHeight - pos.Y;
        return pos;
    }
}