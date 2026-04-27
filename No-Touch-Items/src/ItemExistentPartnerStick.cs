using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace NoTouchItems;

public sealed class ItemExistentPartnerStick : Item
{
    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        // Never allow default stick placement/consumption behavior.
        handling = EnumHandHandling.PreventDefault;

        if (api.Side != EnumAppSide.Server
            || byEntity is not EntityPlayer eplr
            || eplr.Player is not IServerPlayer sp)
        {
            return;
        }

        if (!NoTouchServerSystem.IsExistentShiftModifierDown(eplr)
            || !NoTouchServerSystem.IsExistentRightButtonInteract(eplr))
        {
            return;
        }

        // Pair mode: existent in left hand + partner stick in right hand.
        if (!NoTouchServerSystem.IsExistentTotemItemStack(eplr.LeftHandItemSlot?.Itemstack)
            || !NoTouchServerSystem.IsExistentPartnerStickItemStack(eplr.RightHandItemSlot?.Itemstack))
        {
            return;
        }

        _ = NoTouchServerSystem.TryHandleExistentStampInteract(
            api: (ICoreServerAPI)api,
            stampPlayer: sp,
            eplr: eplr,
            eventBlock: blockSel,
            eventEntity: entitySel,
            source: "OnHeldInteractStart:PartnerStick",
            strictPose: true,
            rayPolicy: NoTouchServerSystem.ExistentRayAimPolicy.BlockRayIgnoreEntities,
            preferExplicitBlock: true);
    }
}
