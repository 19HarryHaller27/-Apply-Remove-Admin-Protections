using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace NoTouchItems;

public class ItemProtectionLantern : Item
{
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (NoTouchServerSystem.VariantIs(
                this,
                NoTouchServerSystem.VariantExistent,
                out _))
        {
            HeldPriorityInteract = NoTouchConstants.ExistentLanternHeldPriorityInteract;
        }
    }

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        // Optional path when the game routes a use to THIS item's slot (e.g. existent is on the selected hotbar slot).
        // Primary workflow: existent in the off-hand + empty active slot so world interaction uses the "main" hand;
        // that does not call this method for the lantern — stamping is handled in
        // <see cref="NoTouchServerSystem.OnServerHandInteract"/> plus server raycast when Current* aim is not set yet.
        if (api.Side == EnumAppSide.Server
            && api is ICoreServerAPI sapi
            && byEntity is EntityPlayer eplr
            && eplr.Player is IServerPlayer sp
            && NoTouchServerSystem.IsExistentRightButtonInteract(eplr)
            && NoTouchServerSystem.TryHandleExistentStampInteract(
                sapi,
                sp,
                eplr,
                blockSel,
                entitySel,
                "OnHeldInteractStart",
                strictPose: false,
                NoTouchServerSystem.ExistentRayAimPolicy.BlockRayIgnoreEntities,
                preferExplicitBlock: true))
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        base.OnHeldInteractStart(
            slot,
            byEntity,
            blockSel,
            entitySel,
            firstEvent,
            ref handling);
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection? blockSel,
        EntitySelection? entitySel)
    {
        if (this.api.Side == EnumAppSide.Server
            && byEntity is EntityPlayer eplr
            && eplr.Player is IServerPlayer sp0
            && sp0.WorldData.CurrentGameMode == EnumGameMode.Creative
            && NoTouchServerSystem.IsExistentStampingPose(
                eplr,
                out _))
        {
            return;
        }
        if (this.api.Side == EnumAppSide.Server
            && byEntity is EntityPlayer p
            && p.Player is IServerPlayer sp1
            && sp1.WorldData.CurrentGameMode != EnumGameMode.Creative
            && IsNoTouchKindLantern(this))
        {
            return;
        }
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
    }

    private static bool IsNoTouchKindLantern(CollectibleObject c) =>
        c.Attributes?["notouchKind"] is not null;
}
