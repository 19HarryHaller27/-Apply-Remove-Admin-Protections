using System;
using System.Collections.Generic;
using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace NoTouchItems;

public sealed class NoTouchServerSystem : ModSystem
{
    public const string VariantExistent = "existent";
    public const string VariantBlock = "block";
    public const string VariantEntity = "entity";

    public static NoTouchServerSystem? Instance { get; private set; }

    public static bool IsEntityProtected(Entity? entity) =>
        entity is not null && IsProtectedEntityId(entity.EntityId);

    public static bool IsProtectedEntityId(long id) =>
        Instance is not null
        && Instance.data.protectedEntityIds is { } l
        && l.Contains(id);

    public static bool VariantIs(CollectibleObject? coll, string variant, out int __)
    {
        __ = 0;
        return coll?.Attributes?["notouchKind"]?.AsString() == variant;
    }

    public static void DiagS(ICoreServerAPI? api, string message)
    {
        if (api is null)
        {
            return;
        }
        if (NoTouchConstants.LogDiagnosticsAsNotification)
        {
            api.Logger.Notification(NoTouchConstants.LogPrefix + " " + message);
        }
        else
        {
            api.Logger.Debug(NoTouchConstants.LogPrefix + " " + message);
        }
    }

    /// <summary>Always at Notification: one high-signal line (e.g. item use) that should appear without raising global log level.</summary>
    public static void DiagSImportant(ICoreServerAPI? api, string message)
    {
        api?.Logger.Notification(NoTouchConstants.LogPrefix + " " + message);
    }

    /// <summary>
    /// Existent is designed around a deliberate <b>right-button interact only</b>. On the server,
    /// <see cref="EntityControls.RightMouseDown"/> can still be set during left-button combat
    /// (so <c>OnPlayerInteractEntity</c> was toggling on attack). Require right without left to
    /// match a clean “use” right-click, not a left-click or both-buttons chord.
    /// </summary>
    public static bool IsExistentRightButtonInteract(Entity? e) =>
        e is EntityPlayer pl
        && pl.Controls.RightMouseDown
        && !pl.Controls.LeftMouseDown;

    /// <summary>Use ShiftKey for click modifiers (supports remapped sneak/shift configs).</summary>
    public static bool IsExistentShiftModifierDown(Entity? e) =>
        e is EntityPlayer pl && pl.Controls.ShiftKey;

    private static bool IsRightInteractMouseDown(Entity? e) => IsExistentRightButtonInteract(e);

    private static readonly JsonSerializerOptions NoTouchJsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = false,
    };

    private void Diag(string message)
    {
        if (sapi is null)
        {
            return;
        }
        if (NoTouchConstants.LogDiagnosticsAsNotification)
        {
            sapi.Logger.Notification(NoTouchConstants.LogPrefix + " " + message);
        }
        else
        {
            sapi.Logger.Debug(NoTouchConstants.LogPrefix + " " + message);
        }
    }

    public enum ExistentAction
    {
        None,
        Stamped,
        Unstamped
    }

    /// <summary>How the server ray fallback picks a target when explicit selections are missing.</summary>
    public enum ExistentRayAimPolicy
    {
        /// <summary>Ray prefers the first entity along the view (then block), matching vanilla aim.</summary>
        Default,
        /// <summary>
        /// For <see cref="Item.OnHeldInteractStart"/>: run a block-only trace (ignore entities) and a combined trace;
        /// if both hit, the surface closer to the eye wins so a nearby insect does not beat the slab behind it.
        /// </summary>
        BlockRayIgnoreEntities,
    }

    public static ExistentAction TryToggleExistent(
        ICoreServerAPI sapi,
        IServerPlayer sp,
        EntityPlayer eplr,
        BlockSelection? blockSel,
        EntitySelection? entitySel)
    {
        if (Instance is null)
        {
            DiagS(sapi, "TryToggle: Instance is null; early exit.");
            return ExistentAction.None;
        }
        if (sp.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            DiagS(sapi, "TryToggle: not creative; early exit.");
            return ExistentAction.None;
        }
        DiagS(
            sapi,
            "TryToggle enter: "
            + $"sp={sp.PlayerName} eplrId={eplr.EntityId} "
            + $"entitySelId={entitySel?.Entity?.EntityId} blockPos={blockSel?.Position}");
        if (entitySel is not null)
        {
            Entity? te = entitySel.Entity;
            if (te is null)
            {
                DiagS(sapi, "TryToggle entity path: null entity; early exit.");
                return ExistentAction.None;
            }
            if (IsProtectedEntityId(te.EntityId))
            {
                DiagS(
                    sapi,
                    "TryToggle entity path: was protected, unprotect attempt.");
                if (Instance.UnprotectEntity(sapi, te))
                {
                    DiagS(
                        sapi,
                        "TryToggle entity path: unprotect ok.");
                    return ExistentAction.Unstamped;
                }
                DiagS(
                    sapi,
                    "TryToggle entity path: unprotect failed.");
                return ExistentAction.None;
            }
            DiagS(
                sapi,
                "TryToggle entity path: protect (totem) attempt.");
            if (Instance.ProtectEntityNow(sapi, te, force: true))
            {
                return ExistentAction.Stamped;
            }
            return ExistentAction.None;
        }
        if (blockSel is not null)
        {
            BlockPos p = blockSel.Position;
            Block? blk = sapi.World.BlockAccessor.GetBlock(p);
            if (blk is null || blk.Id == 0)
            {
                DiagS(
                    sapi,
                    "TryToggle block path: no block; early exit "
                    + p);
                return ExistentAction.None;
            }
            string k = BlockKey(p);
            if (Instance.data.protectedBlockKeys is not null
                && Instance.data.protectedBlockKeys.Contains(k))
            {
                DiagS(
                    sapi,
                    "TryToggle block path: was protected, unprotect " + k);
                if (Instance.UnprotectBlockAt(sapi, p))
                {
                    return ExistentAction.Unstamped;
                }
                return ExistentAction.None;
            }
            DiagS(
                sapi,
                "TryToggle block path: protect (totem) " + k
                + " block="
                + blk?.Code);
            if (Instance.ProtectBlockAtPos(sapi, p, byTotem: true))
            {
                return ExistentAction.Stamped;
            }
        }
        DiagS(
            sapi,
            "TryToggle: no entity and no block selection; no-op.");
        return ExistentAction.None;
    }

    private ICoreServerAPI? sapi;
    private NoTouchData data = new();
    private long reattachListenerId;
    private readonly Dictionary<string, (long AtMs, string TargetKey)> existentUseDebounce = new();

    public override bool ShouldLoad(EnumAppSide forSide) => true;

    public override void StartPre(ICoreAPI api)
    {
        api.RegisterItemClass("ItemProtectionLantern", typeof(ItemProtectionLantern));
        api.RegisterItemClass("ItemExistentPartnerStick", typeof(ItemExistentPartnerStick));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Instance = this;
        sapi = api;
        sapi.RegisterEntityBehaviorClass(EntityBehaviorNoTouch.Code, typeof(EntityBehaviorNoTouch));
        sapi.Logger.Notification($"No Touch Items v{NoTouchConstants.ModVersion}: server hooks loaded.");
        sapi.Event.SaveGameLoaded += OnSaveGameLoaded;
        sapi.Event.GameWorldSave += OnGameWorldSave;
        sapi.Event.DidPlaceBlock += OnDidPlaceBlock;
        sapi.Event.OnEntitySpawn += OnEntitySpawn;
        sapi.Event.OnEntityLoaded += OnEntityLoaded;
        sapi.Event.OnEntityDespawn += OnEntityDespawn;
        sapi.Event.OnTestBlockAccess += OnTestBlockAccess;
        sapi.Event.CanUseBlock += OnCanUseBlockExistent;
        sapi.Event.DidUseBlock += OnDidUseBlockExistent;
        sapi.Event.OnPlayerInteractEntity += OnPlayerInteractEntityExistent;
        sapi.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        sapi.Event.HandInteract += OnServerHandInteract;
        reattachListenerId = sapi.Event.RegisterGameTickListener(OnReattachProtectedBehaviors, 2000, 5000);

        IChatCommandApi c = sapi.ChatCommands;
        c.GetOrCreate("nt")
            .WithAlias("notouch")
            .WithDescription("No Touch: mod diagnostics and setup only. Gameplay: totems + Shift+right-click. Try /nt help")
            .WithExamples("nt help", "nt status", "nt probe", "nt protect")
            .RequiresPlayer()
            .RequiresPrivilege("chat")
            .WithArgs(c.Parsers.OptionalAll("ntArgs"))
            .HandleWith(OnCmdNt);
    }

    public override void Dispose()
    {
        if (sapi is not null)
        {
            sapi.Event.SaveGameLoaded -= OnSaveGameLoaded;
            sapi.Event.GameWorldSave -= OnGameWorldSave;
            sapi.Event.DidPlaceBlock -= OnDidPlaceBlock;
            sapi.Event.OnEntitySpawn -= OnEntitySpawn;
            sapi.Event.OnEntityLoaded -= OnEntityLoaded;
            sapi.Event.OnEntityDespawn -= OnEntityDespawn;
            sapi.Event.OnTestBlockAccess -= OnTestBlockAccess;
            sapi.Event.CanUseBlock -= OnCanUseBlockExistent;
            sapi.Event.DidUseBlock -= OnDidUseBlockExistent;
            sapi.Event.OnPlayerInteractEntity -= OnPlayerInteractEntityExistent;
            sapi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            sapi.Event.HandInteract -= OnServerHandInteract;
            if (reattachListenerId != 0L)
            {
                sapi.Event.UnregisterGameTickListener(reattachListenerId);
            }
        }
        reattachListenerId = 0L;
        if (Instance == this)
        {
            Instance = null;
        }
        sapi = null;
    }

    private void OnSaveGameLoaded()
    {
        if (sapi?.WorldManager.SaveGame is null)
        {
            return;
        }
        data = new NoTouchData();
        try
        {
            if (sapi.WorldManager.SaveGame.GetData<string>(NoTouchConstants.DataKey) is { Length: not 0 } json)
            {
                data = JsonSerializer.Deserialize<NoTouchData>(json, NoTouchJsonOptions) ?? new();
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning(
                NoTouchConstants.LogPrefix
                + " save data load failed (new or legacy); starting empty. "
                + ex.Message);
        }
        data.protectedEntityIds ??= new();
        data.protectedBlockKeys ??= new();
        data.futureRules ??= new();
    }

    private void OnGameWorldSave()
    {
        if (sapi?.WorldManager.SaveGame is null)
        {
            return;
        }
        string json = JsonSerializer.Serialize(
            data,
            NoTouchJsonOptions);
        sapi.WorldManager.SaveGame.StoreData(
            NoTouchConstants.DataKey,
            json);
    }

    public static string BlockKey(BlockPos p) => p.X + "|" + p.Y + "|" + p.Z;

    private void OnDidPlaceBlock(IServerPlayer byPlayer, int _old, BlockSelection blockSel, ItemStack? _with)
    {
        if (sapi is null || blockSel is null)
        {
            return;
        }
        if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative
            && byPlayer.Entity is EntityPlayer eplr
            && HasBlockTotemInEitherHand(eplr))
        {
            Diag(
                "DidPlaceBlock: block-totem protect for "
                + byPlayer.PlayerName
                + " at "
                + BlockKey(blockSel.Position));
            _ = ProtectBlockAtPos(
                sapi,
                blockSel.Position,
                byTotem: true);
        }
        OnPlacedByFutureRuleIfAny(blockSel.Position);
    }

    private void OnPlacedByFutureRuleIfAny(BlockPos pos)
    {
        if (sapi is null)
        {
            return;
        }
        data.futureRules ??= new();
        long t = sapi.World.ElapsedMilliseconds;
        Block? b = sapi.World.BlockAccessor.GetBlock(pos);
        if (b is null || b.Id == 0)
        {
            return;
        }
        if (b.Code is not AssetLocation code)
        {
            return;
        }
        foreach (NoTouchFutureRule rule in data.futureRules)
        {
            if (t < rule.SinceRealMs || !rule.ForBlocks)
            {
                continue;
            }
            if (!NoTouchPathMatch.Matches(code, rule.Pattern))
            {
                continue;
            }
            _ = ProtectBlockAtPos(sapi, pos, byTotem: false);
            return;
        }
    }

    private void OnEntitySpawn(Entity entity)
    {
        if (sapi is null)
        {
            return;
        }
        data.futureRules ??= new();
        long t = sapi.World.ElapsedMilliseconds;
        string? code = entity.Properties?.Code?.ToString();
        if (entity.Properties?.Code is AssetLocation c)
        {
            foreach (NoTouchFutureRule rule in data.futureRules)
            {
                if (t < rule.SinceRealMs)
                {
                    continue;
                }
                if (!rule.ForEntities)
                {
                    continue;
                }
                if (NoTouchPathMatch.Matches(c, rule.Pattern))
                {
                    DiagS(
                        sapi,
                        "OnEntitySpawn: /nt protect rule '"
                        + rule.Pattern
                        + "' id="
                        + entity.EntityId
                        + " code="
                        + code);
                    _ = ProtectEntityNow(sapi, entity, force: false);
                    return;
                }
            }
        }
        if (byPlayerHoldingEntityTotem(sapi, entity) is IServerPlayer pt)
        {
            DiagS(
                sapi,
                "OnEntitySpawn: entity-totem near '"
                + pt.PlayerName
                + "' id="
                + entity.EntityId
                + " code="
                + code);
            _ = ProtectEntityNow(sapi, entity, force: true);
        }
    }

    private static IServerPlayer? byPlayerHoldingEntityTotem(ICoreServerAPI api, Entity around)
    {
        IServerPlayer? best = null;
        double bestSq = double.MaxValue;
        double rs = NoTouchConstants.StampedSpawnMatchRadius * NoTouchConstants.StampedSpawnMatchRadius;
        foreach (IServerPlayer p in api.World.AllOnlinePlayers)
        {
            if (p.WorldData.CurrentGameMode != EnumGameMode.Creative || p.Entity is not EntityPlayer e)
            {
                continue;
            }
            if (!HasEntityTotemInEitherHand(e))
            {
                continue;
            }
            double d = e.Pos.SquareDistanceTo(around.Pos);
            if (d > rs)
            {
                continue;
            }
            if (d < bestSq)
            {
                bestSq = d;
                best = p;
            }
        }
        return best;
    }

    public static bool IsOurLantern(ItemStack stack, string lastPathPart) =>
        stack.Collectible.Code
            == new AssetLocation(NoTouchConstants.ModId, lastPathPart);

    private void OnEntityLoaded(Entity entity)
    {
        if (data.protectedEntityIds?.Contains(entity.EntityId) == true)
        {
            ReattachBehavior(entity);
            SyncNotouchWatched(entity, true);
        }
    }

    // Clear the saved id only for permanent loss; do not on Unload/Disconnect/OutOfRange or the entity
    // can return from disk and never get EntityBehaviorNoTouch in OnEntityLoaded.
    private static bool ShouldClearProtectedIdOnDespawn(EntityDespawnData d) =>
        d.Reason is EnumDespawnReason.Death
            or EnumDespawnReason.Combusted
            or EnumDespawnReason.PickedUp
            or EnumDespawnReason.Expire
            or EnumDespawnReason.Removed;

    private void OnEntityDespawn(Entity entity, EntityDespawnData reason)
    {
        if (!ShouldClearProtectedIdOnDespawn(reason))
        {
            return;
        }
        data.protectedEntityIds?.Remove(entity.EntityId);
    }

    /// <summary>
    /// Stops <see cref="EnumBlockAccessFlags.BuildOrBreak"/> on protected cells (place/mine) so players
    /// cannot break or replace them. <see cref="EnumBlockAccessFlags.Use"/> (open door/chest/vessel UI)
    /// and <see cref="EnumBlockAccessFlags.Traverse"/> are left to the default response: open still works
    /// when the game tests <c>Use</c> alone. Uses <c>HasFlag</c> because the enum is a flags set.
    /// </summary>
    private EnumWorldAccessResponse OnTestBlockAccess(
        IPlayer player,
        BlockSelection? blockSel,
        EnumBlockAccessFlags accessType,
        ref string claimant,
        EnumWorldAccessResponse response)
    {
        if (blockSel is null)
        {
            return response;
        }
        if (!accessType.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            return response;
        }
        if (data.protectedBlockKeys is null
            || !data.protectedBlockKeys.Contains(BlockKey(blockSel.Position)))
        {
            return response;
        }
        sapi?.Logger.Debug(
            NoTouchConstants.LogPrefix
            + " build/break denied pos="
            + BlockKey(blockSel.Position)
            + " player="
            + player?.PlayerName);
        claimant = NoTouchConstants.NotouchMessageClaimant;
        return EnumWorldAccessResponse.DeniedByMod;
    }

    private bool OnCanUseBlockExistent(IServerPlayer byPlayer, BlockSelection blockSel)
    {
        if (sapi is null || byPlayer.Entity is not EntityPlayer eplr)
        {
            return true;
        }
        if (!IsRightInteractMouseDown(eplr))
        {
            return true;
        }
        return !TryHandleExistentHook(
            byPlayer,
            eplr,
            blockSel,
            null,
            "CanUseBlock");
    }

    private void OnDidUseBlockExistent(IServerPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.Entity is not EntityPlayer eplr)
        {
            return;
        }
        if (!IsRightInteractMouseDown(eplr))
        {
            return;
        }
        _ = TryHandleExistentHook(
            byPlayer,
            eplr,
            blockSel,
            null,
            "DidUseBlock");
    }

    private void OnPlayerInteractEntityExistent(
        Entity entity,
        IPlayer byPlayer,
        ItemSlot _slot,
        Vec3d _hitPosition,
        int mode,
        ref EnumHandling handling)
    {
        if (sapi is null
            || byPlayer is not IServerPlayer sp
            || sp.Entity is not EntityPlayer eplr
            || entity.EntityId == eplr.EntityId)
        {
            return;
        }
        if (mode != (int)EnumInteractMode.Interact)
        {
            return;
        }
        if (!IsRightInteractMouseDown(eplr))
        {
            return;
        }

        if (TryHandleExistentHook(
                sp,
                eplr,
                null,
                new EntitySelection { Entity = entity },
                "OnPlayerInteractEntity"))
        {
            handling = EnumHandling.PreventDefault;
        }
    }

    private bool TryHandleExistentHook(
        IServerPlayer _player,
        EntityPlayer eplr,
        BlockSelection? eventBlock,
        EntitySelection? eventEntity,
        string source)
    {
        if (sapi is null)
        {
            return false;
        }
        if (!TryGetExistentStampPlayer(
                eplr,
                out IServerPlayer? stampSp,
                out bool strictPose)
            || stampSp is null)
        {
            return false;
        }
        return TryHandleExistentStampInteract(
            sapi,
            stampSp,
            eplr,
            eventBlock,
            eventEntity,
            source,
            strictPose,
            ExistentRayAimPolicy.Default);
    }

    /// <summary>
    /// Primary path for existent stamping: totem in off-hand, empty main/active slot (one hand totem, other empty).
    /// Runs before normal hand routing; uses <see cref="TryAppendExistentAimFromServerRaycast"/> when
    /// <see cref="IPlayer.CurrentBlockSelection"/> / <see cref="IPlayer.CurrentEntitySelection"/> are not set yet,
    /// since this event fires earlier than aim sync (no reliance on moving the totem to the active hotbar slot).
    /// </summary>
    private void OnServerHandInteract(
        IServerPlayer player,
        EnumHandInteractNw kind,
        float _secondsPassed,
        ref EnumHandling handling)
    {
        if (sapi is null)
        {
            return;
        }

        if (!kind.ToString().StartsWith("Start", StringComparison.Ordinal))
        {
            return;
        }

        if (player.Entity is not EntityPlayer eplr)
        {
            return;
        }
        if (!IsRightInteractMouseDown(eplr))
        {
            return;
        }

        if (TryHandleExistentHook(
                player,
                eplr,
                player.CurrentBlockSelection,
                player.CurrentEntitySelection,
                "HandInteract:" + kind))
        {
            handling = EnumHandling.PreventDefault;
        }
    }

    private void OnReattachProtectedBehaviors(float _dt)
    {
        ICoreServerAPI? api = sapi;
        if (api is null || data.protectedEntityIds is null)
        {
            return;
        }
        List<long> copy = new(data.protectedEntityIds);
        foreach (long id in copy)
        {
            Entity? e = api.World.GetEntityById(id);
            if (e is null)
            {
                continue;
            }
            if (!e.HasBehavior(EntityBehaviorNoTouch.Code))
            {
                ReattachBehavior(e);
            }
            else if (e.GetBehavior(EntityBehaviorNoTouch.Code) is EntityBehavior b)
            {
                TryPromoteNoTouchBehaviorFirst(
                    e,
                    b);
            }
            if (e.HasBehavior(EntityBehaviorNoTouch.Code))
            {
                SyncNotouchWatched(e, true);
            }
        }
    }

    private static void SyncNotouchWatched(Entity? entity, bool isProtected)
    {
        if (entity is null)
        {
            return;
        }
        ITreeAttribute t = entity.WatchedAttributes.GetOrAddTreeAttribute(NoTouchConstants.WatchedNotouchTree);
        t.SetInt(NoTouchConstants.WatchedNotouchKeyProtected, isProtected ? 1 : 0);
        entity.WatchedAttributes.MarkPathDirty(NoTouchConstants.WatchedNotouchTree);
    }

    public static void SendExistentResultMessage(IServerPlayer sp, ExistentAction a)
    {
        if (a == ExistentAction.Stamped)
        {
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "No Touch: protected (existent).",
                EnumChatType.Notification);
        }
        else if (a == ExistentAction.Unstamped)
        {
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "No Touch: removed protection (existent).",
                EnumChatType.Notification);
        }
    }

    private void ExistentNotify(IServerPlayer sp, ExistentAction a) =>
        SendExistentResultMessage(
            sp,
            a);

    public static bool IsExistentTotemItemStack(ItemStack? s) =>
        s is { Item: not null, Collectible: not null }
        && IsOurLantern(s, NoTouchConstants.CodeExistentLantern)
        && VariantIs(s.Collectible, VariantExistent, out _);

    public static bool IsExistentPartnerStickItemStack(ItemStack? s) =>
        s is { Item: not null, Collectible: not null }
        && string.Equals(
            s.Collectible.Code?.Path,
            NoTouchConstants.CodeExistentPartnerStick,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Fallback from <see cref="IPlayer.CurrentEntitySelection"/> when explicit event args are absent.</summary>
    private static EntitySelection? TryGetAimedExistentEntitySelection(IPlayer byPlayer, EntityPlayer eplr)
    {
        Entity? te = byPlayer.CurrentEntitySelection?.Entity;
        if (te is null || te.EntityId == eplr.EntityId)
        {
            return null;
        }
        return new EntitySelection { Entity = te };
    }

    private static Vec3d ExistentEyePos(EntityPlayer eplr) =>
        eplr.Pos.XYZ.Add(
            eplr.LocalEyePos.X,
            eplr.LocalEyePos.Y,
            eplr.LocalEyePos.Z);

    private static bool IsRealBlockSelection(ICoreServerAPI api, BlockSelection bs)
    {
        Block? b = api.World.BlockAccessor.GetBlock(bs.Position);
        return b is not null && b.Id != 0;
    }

    /// <summary>
    /// Resolves stamp target: explicit entity/block from the interaction, then synced selections, then ray.
    /// When both entity and block are supplied, the closer-to-eye hit wins so a foreground mob does not override
    /// the slab you are actually clicking. <paramref name="rayPolicy"/> controls whether the ray ignores entities
    /// first (held existent) or prefers the first entity hit (HandInteract / vanilla aim).
    /// </summary>
    public static void ResolveExistentToggleSelections(
        ICoreServerAPI api,
        IServerPlayer byPlayer,
        EntityPlayer eplr,
        BlockSelection? eventBlock,
        EntitySelection? eventEntity,
        ExistentRayAimPolicy rayPolicy,
        out EntitySelection? entityOut,
        out BlockSelection? blockOut)
    {
        entityOut = null;
        blockOut = null;
        BlockSelection? eb = eventBlock is not null && IsRealBlockSelection(
                api,
                eventBlock)
            ? eventBlock
            : null;
        Entity? ev = eventEntity?.Entity;
        if (ev is not null && ev.EntityId == eplr.EntityId)
        {
            ev = null;
        }

        if (ev is not null && eb is not null)
        {
            Vec3d eye = ExistentEyePos(eplr);
            Vec3d vBlk = eb.HitPosition;
            double dBlk = eye.SquareDistanceTo(vBlk);
            double dEnt = eye.SquareDistanceTo(ev.Pos.XYZ);
            if (dBlk <= dEnt)
            {
                blockOut = eb;
                return;
            }

            entityOut = new EntitySelection { Entity = ev };
            return;
        }

        if (ev is not null)
        {
            entityOut = new EntitySelection { Entity = ev };
            return;
        }

        if (eb is not null)
        {
            blockOut = eb;
            return;
        }

        // Synced aim: must disambiguate together. Previously we read CurrentEntitySelection *before*
        // CurrentBlockSelection, so a stale mob highlight could win and we never saw the slab in
        // CurrentBlockSelection — the main "blocks don't work" failure with the existent in hand.
        BlockSelection? curBlk =
            byPlayer.CurrentBlockSelection is { } cb
            && IsRealBlockSelection(
                api,
                cb)
                ? cb
                : null;
        EntitySelection? curEnt = TryGetAimedExistentEntitySelection(
            byPlayer,
            eplr);
        if (curBlk is not null && curEnt is not null)
        {
            Vec3d eye = ExistentEyePos(eplr);
            double dBlk = eye.SquareDistanceTo(curBlk.HitPosition);
            Entity? te = curEnt.Entity;
            if (te is not null && te.EntityId != eplr.EntityId)
            {
                double dEnt = eye.SquareDistanceTo(te.Pos.XYZ);
                if (dBlk <= dEnt)
                {
                    blockOut = curBlk;
                    return;
                }

                entityOut = curEnt;
                return;
            }

            blockOut = curBlk;
            return;
        }

        if (curBlk is not null)
        {
            blockOut = curBlk;
            return;
        }

        if (curEnt is not null)
        {
            entityOut = curEnt;
            return;
        }

        TryAppendExistentAimFromServerRaycast(
            api,
            eplr,
            ref entityOut,
            ref blockOut,
            rayPolicy);
    }

    /// <summary>
    /// Fills block/entity aim when UI selections are not populated yet (common for <see cref="IServerEventAPI.HandInteract"/>).
    /// </summary>
    private static void TryAppendExistentAimFromServerRaycast(
        ICoreServerAPI api,
        EntityPlayer eplr,
        ref EntitySelection? entityOut,
        ref BlockSelection? blockOut,
        ExistentRayAimPolicy rayPolicy)
    {
        if (entityOut is not null || blockOut is not null)
        {
            return;
        }

        Vec3d fromPos = eplr.Pos.XYZ.Add(
            0,
            eplr.LocalEyePos.Y,
            0);
        float range = NoTouchConstants.ExistentRaycastRange;
        long selfId = eplr.EntityId;
        if (rayPolicy == ExistentRayAimPolicy.BlockRayIgnoreEntities)
        {
            TryExistentRayDualEntityOrBlockThrough(
                api,
                eplr,
                fromPos,
                range,
                selfId,
                ref entityOut,
                ref blockOut);
            return;
        }

        BlockSelection? bRay = null;
        EntitySelection? eRay = null;
        api.World.RayTraceForSelection(
            fromPos,
            (float)eplr.Pos.Pitch,
            (float)eplr.Pos.Yaw,
            range,
            ref bRay,
            ref eRay,
            null,
            ent => ent.EntityId != selfId);
        if (eRay?.Entity is { } hitEnt
            && hitEnt.EntityId != selfId)
        {
            entityOut = eRay;
            return;
        }

        if (bRay is not null && IsRealBlockSelection(
                api,
                bRay))
        {
            blockOut = bRay;
        }
    }

    /// <summary>
    /// Two traces: (1) combined vanilla-style, (2) entities ignored so the first solid block is known.
    /// If both an entity and that “through” block exist, pick whichever hit is closer to the eye (reorders priority vs
    /// vanilla “entity always wins if it is first along the ray”).
    /// </summary>
    private static void TryExistentRayDualEntityOrBlockThrough(
        ICoreServerAPI api,
        EntityPlayer eplr,
        Vec3d fromPos,
        float range,
        long selfId,
        ref EntitySelection? entityOut,
        ref BlockSelection? blockOut)
    {
        BlockSelection? bThrough = null;
        EntitySelection? eIgnore = null;
        api.World.RayTraceForSelection(
            fromPos,
            (float)eplr.Pos.Pitch,
            (float)eplr.Pos.Yaw,
            range,
            ref bThrough,
            ref eIgnore,
            null,
            _ => false);

        BlockSelection? bComb = null;
        EntitySelection? eComb = null;
        api.World.RayTraceForSelection(
            fromPos,
            (float)eplr.Pos.Pitch,
            (float)eplr.Pos.Yaw,
            range,
            ref bComb,
            ref eComb,
            null,
            ent => ent.EntityId != selfId);

        bool haveThroughBlock = bThrough is not null
            && IsRealBlockSelection(
                api,
                bThrough);
        Entity? entComb = eComb?.Entity;
        bool haveCombEntity = entComb is not null && entComb.EntityId != selfId;
        bool haveCombBlock = bComb is not null
            && IsRealBlockSelection(
                api,
                bComb);

        Vec3d eye = ExistentEyePos(eplr);

        if (haveCombEntity && haveThroughBlock)
        {
            double dEnt = eye.SquareDistanceTo(entComb!.Pos.XYZ);
            double dBlk = eye.SquareDistanceTo(bThrough!.HitPosition);
            if (dBlk < dEnt)
            {
                blockOut = bThrough;
                return;
            }

            entityOut = eComb;
            return;
        }

        if (haveCombEntity)
        {
            entityOut = eComb;
            return;
        }

        if (haveThroughBlock)
        {
            blockOut = bThrough;
            return;
        }

        if (haveCombBlock)
        {
            blockOut = bComb;
        }
    }

    /// <summary>
    /// Creative existent-lantern stamping: resolve aim and run toggle. Returns whether the interaction should
    /// consume the hand use (caller sets <see cref="EnumHandHandling"/> or <see cref="EnumHandling"/>).
    /// </summary>
    public static bool TryHandleExistentStampInteract(
        ICoreServerAPI api,
        IServerPlayer stampPlayer,
        EntityPlayer eplr,
        BlockSelection? eventBlock,
        EntitySelection? eventEntity,
        string source = "unknown",
        bool strictPose = false,
        ExistentRayAimPolicy rayPolicy = ExistentRayAimPolicy.Default,
        bool preferExplicitBlock = false)
    {
        if (stampPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return false;
        }

        // Global safety: existent stamping is now a deliberate Shift+RMB action.
        if (!IsExistentShiftModifierDown(eplr))
        {
            return false;
        }

        EntitySelection? eUse;
        BlockSelection? bUse;
        if (preferExplicitBlock
            && eventBlock is not null
            && IsRealBlockSelection(
                api,
                eventBlock))
        {
            // Strong block-first mode for held-item routing: if engine gave us a real block on this click,
            // stamp that block directly and do not let entity/stale selections steal the interaction.
            eUse = null;
            bUse = eventBlock;
        }
        else
        {
            ResolveExistentToggleSelections(
                api,
                stampPlayer,
                eplr,
                eventBlock,
                eventEntity,
                rayPolicy,
                out eUse,
                out bUse);
        }
        if (eUse is null && bUse is null)
        {
            DiagS(
                api,
                "Existent hook no target: source="
                + source
                + " strictPose="
                + strictPose);
            return false;
        }

        string targetKey = eUse is not null
            ? "e:" + eUse.Entity?.EntityId
            : "b:" + bUse?.Position;
        if (Instance is { } inst
            && inst.ShouldDebounceExistent(stampPlayer.PlayerUID, targetKey))
        {
            DiagS(
                api,
                "Existent hook debounced: source="
                + source
                + " target="
                + targetKey);
            return true;
        }

        DiagSImportant(
            api,
            "Existent stamp use: source="
            + source
            + " strictPose="
            + strictPose
            + " ent="
            + (eUse?.Entity?.EntityId)
            + " block="
            + (bUse?.Position));
        ExistentAction a;
        if (eUse is not null)
        {
            a = TryToggleExistent(
                api,
                stampPlayer,
                eplr,
                null,
                eUse);
        }
        else
        {
            a = TryToggleExistent(
                api,
                stampPlayer,
                eplr,
                bUse!,
                null);
        }

        if (a == ExistentAction.None)
        {
            DiagS(
                api,
                "Existent hook no-op: source="
                + source
                + " target="
                + targetKey);
            return false;
        }
        SendExistentResultMessage(
            stampPlayer,
            a);
        return true;
    }

    public static bool IsExistentStampingPose(
        EntityPlayer eplr,
        out IServerPlayer? sp)
    {
        sp = eplr.Player as IServerPlayer;
        if (sp is null)
        {
            return false;
        }
        bool l = IsExistentTotemItemStack(eplr.LeftHandItemSlot?.Itemstack);
        bool r = IsExistentTotemItemStack(eplr.RightHandItemSlot?.Itemstack);
        bool rStick = IsExistentPartnerStickItemStack(eplr.RightHandItemSlot?.Itemstack);
        bool le = eplr.LeftHandItemSlot?.Itemstack is null;
        bool re = eplr.RightHandItemSlot?.Itemstack is null;
        bool strictPose = (l && re) || (r && le) || (l && rStick);
        return strictPose && IsExistentShiftModifierDown(eplr);
    }

    private static bool TryGetExistentStampPlayer(
        EntityPlayer eplr,
        out IServerPlayer? sp,
        out bool strictPose)
    {
        strictPose = false;
        sp = eplr.Player as IServerPlayer;
        if (sp is null || sp.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return false;
        }

        bool l = IsExistentTotemItemStack(eplr.LeftHandItemSlot?.Itemstack);
        bool r = IsExistentTotemItemStack(eplr.RightHandItemSlot?.Itemstack);
        bool rStick = IsExistentPartnerStickItemStack(eplr.RightHandItemSlot?.Itemstack);
        bool le = eplr.LeftHandItemSlot?.Itemstack is null;
        bool re = eplr.RightHandItemSlot?.Itemstack is null;
        strictPose = ((l && re) || (r && le) || (l && rStick)) && IsExistentShiftModifierDown(eplr);
        return strictPose;
    }

    private bool ShouldDebounceExistent(string playerUid, string targetKey)
    {
        if (sapi is null)
        {
            return false;
        }

        long now = sapi.World.ElapsedMilliseconds;
        if (existentUseDebounce.TryGetValue(playerUid, out (long AtMs, string TargetKey) prev)
            && prev.TargetKey == targetKey
            && now - prev.AtMs <= 180L)
        {
            return true;
        }

        existentUseDebounce[playerUid] = (now, targetKey);
        return false;
    }

    private static bool HasBlockTotemInEitherHand(EntityPlayer e)
    {
        return HandHasBlockTotem(e.LeftHandItemSlot) || HandHasBlockTotem(e.RightHandItemSlot);
    }

    private static bool HandHasBlockTotem(ItemSlot? slot) =>
        slot?.Itemstack is { } s
        && s.Item is not null
        && IsOurLantern(s, NoTouchConstants.CodeBlockLantern)
        && VariantIs(s.Collectible, VariantBlock, out _);

    private static bool HasEntityTotemInEitherHand(EntityPlayer e)
    {
        return HandHasEntityTotem(e.LeftHandItemSlot) || HandHasEntityTotem(e.RightHandItemSlot);
    }

    private static bool HandHasEntityTotem(ItemSlot? slot) =>
        slot?.Itemstack is { } s
        && s.Item is not null
        && IsOurLantern(s, NoTouchConstants.CodeEntityLantern)
        && VariantIs(s.Collectible, VariantEntity, out _);

    private void OnPlayerNowPlaying(IServerPlayer p)
    {
        if (p is null || sapi is null)
        {
            return;
        }
        p.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            $"No Touch Items v{NoTouchConstants.ModVersion}: place blocks, spawn mobs, stamp existent: totem in one hand (or existent left + Stick of Existent Protection right), Shift+right-click/place. Chat /nt* is diagnostics only. Alt+P: paths.",
            EnumChatType.Notification);
    }

    private TextCommandResult OnCmdNt(TextCommandCallingArgs targs)
    {
        ICoreServerAPI? api = sapi;
        if (api is null
            || targs.Caller.Player is not IServerPlayer sp
            || sp.Entity is not EntityPlayer eplr)
        {
            return TextCommandResult.Error(
                "No Touch: server or player not ready.",
                "notouch-nt-na");
        }
        string line = (targs[0] as string ?? string.Empty).Trim();
        string first = "help";
        string rest = string.Empty;
        if (line.Length is not 0)
        {
            int spc0 = line.IndexOf(
                ' ',
                StringComparison.Ordinal);
            first = (spc0 < 0
                ? line
                : line[.. spc0])
                .ToLowerInvariant();
            if (spc0 >= 0)
            {
                rest = line[(spc0 + 1) ..].Trim();
            }
        }
        if (line.Length is 0
            || first
                is "help"
                or "h"
                or "?"
            )
        {
            return TextCommandResult.Success(
                "No Touch /nt: diagnostics (status, probe, look) and optional /nt protect|noprotect for future spawn rules (creative). In-game: totems, Shift+right-clicks, placement; no /nt* required.");
        }
        if (first is "status" or "s" or "stat")
        {
            return OnCmdNtStatus(sp);
        }
        if (first is "probe" or "p" or "info" or "i")
        {
            return OnCmdNtProbe(
                sp,
                eplr);
        }
        if (first is "look" or "l")
        {
            return OnCmdNtLookReadonly(
                sp,
                eplr);
        }
        if (first is "protect" or "protection")
        {
            return OnCmdNtProtectLine(
                sp,
                rest);
        }
        if (first is "noprotect" or "noprotection" or "unprotect")
        {
            return OnCmdNtNoprotectLine(
                sp,
                rest);
        }
        if (first is "version" or "v" or "ver")
        {
            return TextCommandResult.Success(
                NoTouchConstants.LogPrefix
                + " v"
                + NoTouchConstants.ModVersion);
        }
        return TextCommandResult.Error("Unknown. Try: /nt help", "notouch-nt-unk");
    }

    private TextCommandResult OnCmdNtStatus(IServerPlayer sp)
    {
        ICoreServerAPI? api = sapi;
        int ne = data.protectedEntityIds?.Count ?? 0;
        int nb = data.protectedBlockKeys?.Count ?? 0;
        int nr = data.futureRules?.Count ?? 0;
        Diag(
            "nt status: entities="
            + ne
            + " blockCells="
            + nb
            + " futureRules="
            + nr
            + " for "
            + sp.PlayerName);
        sp.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            NoTouchConstants.LogPrefix
            + " v"
            + NoTouchConstants.ModVersion
            + ": protected entities: "
            + ne
            + ", block keys: "
            + nb
            + ", future /nt protect rules: "
            + nr,
            EnumChatType.Notification);
        if (ne is not 0
            && data.protectedEntityIds is not null)
        {
            var parts = new List<string>();
            int n = 0;
            foreach (long id in data.protectedEntityIds)
            {
                if (n++ > 6)
                {
                    parts.Add("…");
                    break;
                }
                Entity? e = api?.World.GetEntityById(id);
                parts.Add(
                    e is null
                        ? id + "?"
                        : e.EntityId.ToString());
            }
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Ids (up to 7): "
                + string.Join(
                    ", ",
                    parts),
                EnumChatType.Notification);
        }
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCmdNtLookReadonly(
        IServerPlayer sp,
        EntityPlayer eplr)
    {
        TextCommandResult r = OnCmdNtProbe(
            sp,
            eplr);
        sp.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            "Existent: Shift+right-click the target in-game (totem in one hand, other empty; or existent left + Stick of Existent Protection right; not /nt look). /nt look is read-only diagnostics only.",
            EnumChatType.Notification);
        return r;
    }

    private TextCommandResult OnCmdNtProbe(IServerPlayer sp, EntityPlayer eplr)
    {
        ICoreServerAPI? api = sapi;
        if (api is null)
        {
            return TextCommandResult.Error("No server.", "notouch-nt-na");
        }
        bool okPose = IsExistentStampingPose(
            eplr,
            out _);
        BlockSelection? blockAim = sp.CurrentBlockSelection;
        sp.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            "Mode: "
            + sp.WorldData.CurrentGameMode
            + " existentPose: "
            + okPose
            + " (one hand totem+other empty).",
            EnumChatType.Notification);
        Entity? aimEnt = sp.CurrentEntitySelection?.Entity;
        if (aimEnt is not null)
        {
            double d = System.Math.Sqrt(
                eplr.Pos.SquareDistanceTo(aimEnt.Pos));
            bool losE = LineOfSightToEntity(
                api,
                eplr,
                aimEnt);
            bool inListE = IsProtectedEntityId(aimEnt.EntityId);
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Entity aim id="
                + aimEnt.EntityId
                + " d="
                + d.ToString("F1")
                + " los="
                + losE
                + " protectedId="
                + inListE,
                EnumChatType.Notification);
        }
        else
        {
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "No entity in CurrentEntitySelection.",
                EnumChatType.Notification);
        }
        if (blockAim is { } bs)
        {
            BlockPos p = bs.Position;
            double dB = System.Math.Sqrt(
                eplr.Pos.SquareDistanceTo(
                    p.X
                    + 0.5,
                    p.Y
                    + 0.5,
                    p.Z
                    + 0.5));
            bool rOk = true;
            bool losB = LineOfSightToBlock(
                api,
                eplr,
                p);
            string k = BlockKey(p);
            bool inListB = data.protectedBlockKeys?.Contains(k) == true;
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "Block aim "
                + p
                + " d="
                + dB.ToString("F1")
                + " inRange6="
                + rOk
                + " los="
                + losB
                + " key="
                + k
                + " protected="
                + inListB,
                EnumChatType.Notification);
        }
        else
        {
            sp.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "No block in CurrentBlockSelection.",
                EnumChatType.Notification);
        }
        Diag(
            "nt probe: "
            + sp.PlayerName
            + " aimEnt="
            + (aimEnt?.EntityId)
            + " aimBlock="
            + (blockAim?.Position));
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCmdNtProtectLine(
        IServerPlayer p,
        string line)
    {
        ICoreServerAPI? api = sapi;
        if (api is null
            || p.Entity is null)
        {
            return TextCommandResult.Error("Server or player unavailable.", "notouch-na");
        }
        if (p.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return TextCommandResult.Error("No Touch: /nt protect is creative only.", "notouch-gamemode");
        }
        line = (line ?? string.Empty).Trim();
        if (line.Length is 0)
        {
            return TextCommandResult.Success(
                "Usage: /nt protect <pattern> [b|e|a] — b=blocks e=entities a=both (default: a). Affects only future spawns/places.");
        }
        int spcPat = line.IndexOf(
            ' ',
            StringComparison.Ordinal);
        string pat;
        string mode;
        if (spcPat < 0)
        {
            pat = line;
            mode = "a";
        }
        else
        {
            pat = line.AsSpan(0, spcPat)
                .Trim()
                .ToString();
            mode = line[(spcPat + 1)..].Trim()
                .ToLowerInvariant();
            if (mode.Length is 0)
            {
                mode = "a";
            }
        }
        if (string.IsNullOrEmpty(pat))
        {
            return TextCommandResult.Error("No pattern after /nt protect.", "notouch-pattern");
        }
        bool fEnt = true;
        bool fBlk = true;
        if (mode is "b" or "block" or "blocks")
        {
            fEnt = false;
            fBlk = true;
        }
        else if (mode is "e" or "entity" or "entities")
        {
            fEnt = true;
            fBlk = false;
        }
        else if (mode is "a" or "all" or "both" or "be")
        {
            fEnt = fBlk = true;
        }
        data.futureRules.Add(
            new NoTouchFutureRule
            {
                Pattern = pat,
                ForEntities = fEnt,
                ForBlocks = fBlk,
                SinceRealMs = api.World.ElapsedMilliseconds,
            });
        return TextCommandResult.Success(
            $"No Touch: future rule for '{pat}' (entities:{fEnt} blocks:{fBlk}).");
    }

    private TextCommandResult OnCmdNtNoprotectLine(
        IServerPlayer p,
        string pat)
    {
        if (p.Entity is null)
        {
            return TextCommandResult.Error("Server or player unavailable.", "notouch-na");
        }
        if (p.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return TextCommandResult.Error("No Touch: /nt noprotect is creative only.", "notouch-gamemode");
        }
        if (string.IsNullOrWhiteSpace(pat))
        {
            return TextCommandResult.Error(
                "Usage: /nt noprotect <pattern> — exact string match to stored rules.",
                "notouch-usage");
        }
        int c = data.futureRules.RemoveAll(x => x.Pattern == pat.Trim());
        return TextCommandResult.Success(
            $"No Touch: removed {c} future rule(s) with that pattern.");
    }

    public bool UnprotectBlockAt(ICoreServerAPI _api, BlockPos pos)
    {
        string k = BlockKey(pos);
        DiagS(
            _api,
            "UnprotectBlockAt " + k);
        _ = _api;
        if (data.protectedBlockKeys is null)
        {
            return false;
        }
        return data.protectedBlockKeys.Remove(k);
    }

    public bool UnprotectEntity(ICoreServerAPI _api, Entity? ent)
    {
        _ = _api;
        if (ent is null)
        {
            return false;
        }
        DiagS(
            _api,
            "UnprotectEntity id=" + ent.EntityId);
        data.protectedEntityIds?.Remove(ent.EntityId);
        if (ent.GetBehavior(EntityBehaviorNoTouch.Code) is EntityBehavior b1)
        {
            ent.RemoveBehavior(b1);
        }
        else if (ent.GetBehavior(EntityBehaviorNoTouch.LegacyCode) is EntityBehavior b0)
        {
            ent.RemoveBehavior(b0);
        }
        SyncNotouchWatched(ent, false);
        return true;
    }

    public bool ProtectBlockAtPos(ICoreServerAPI _api, BlockPos pos, bool byTotem)
    {
        _ = byTotem;
        data.protectedBlockKeys ??= new();
        string k = BlockKey(pos);
        if (!data.protectedBlockKeys.Contains(k))
        {
            data.protectedBlockKeys.Add(k);
            DiagS(
                _api,
                "ProtectBlockAt (new key) " + k);
        }
        else
        {
            DiagS(
                _api,
                "ProtectBlockAt (already) " + k);
        }
        return true;
    }

    public bool ProtectEntityNow(ICoreServerAPI _api, Entity ent, bool force)
    {
        if (!force
            && data.protectedEntityIds is not null
            && data.protectedEntityIds.Contains(ent.EntityId))
        {
            DiagS(
                _api,
                "ProtectEntityNow: already in list, force off id=" + ent.EntityId);
            return true;
        }
        data.protectedEntityIds ??= new();
        if (!data.protectedEntityIds.Contains(ent.EntityId))
        {
            data.protectedEntityIds.Add(ent.EntityId);
        }
        ReattachBehavior(ent);
        SyncNotouchWatched(ent, true);
        DiagS(
            _api,
            "ProtectEntityNow: id="
            + ent.EntityId
            + " force="
            + force
            + " hasBehavior="
            + ent.HasBehavior(EntityBehaviorNoTouch.Code));
        return true;
    }

    private void ReattachBehavior(Entity? entity)
    {
        if (entity is null)
        {
            return;
        }
        if (entity.GetBehavior(EntityBehaviorNoTouch.LegacyCode) is EntityBehavior leg)
        {
            entity.RemoveBehavior(leg);
        }
        if (entity.HasBehavior(EntityBehaviorNoTouch.Code))
        {
            return;
        }
        try
        {
            var nb = new EntityBehaviorNoTouch(entity);
            entity.AddBehavior(nb);
            TryPromoteNoTouchBehaviorFirst(
                entity,
                nb);
        }
        catch (Exception ex)
        {
            sapi?.Logger.Warning("No Touch: could not add EntityBehaviorNoTouch for entity {0}: {1}",
                entity?.EntityId,
                ex);
        }
    }

    /// <summary>
    /// <see cref="Entity.ReceiveDamage"/> iterates <see cref="EntitySidedProperties.Behaviors"/> in list order (see game
    /// <c>Entity.ReceiveDamage</c>); <see cref="EntityBehaviorHealth"/> must run after our handler so damage can be zeroed
    /// before HP is subtracted. <see cref="Entity.CacheServerBehaviors"/> rebuilds tick arrays from that list.
    /// </summary>
    private static void TryPromoteNoTouchBehaviorFirst(
        Entity entity,
        EntityBehavior noTouch)
    {
        if (entity.SidedProperties?.Behaviors is not List<EntityBehavior> list)
        {
            return;
        }

        int i = list.IndexOf(noTouch);
        if (i <= 0)
        {
            return;
        }

        _ = list.Remove(noTouch);
        list.Insert(
            0,
            noTouch);
        if (entity.Api is ICoreServerAPI)
        {
            entity.CacheServerBehaviors();
        }
    }

    /// <summary>
    /// See <see cref="LineOfSightRay"/> and <see cref="GetEntityAimYSamples"/>. If every ray test fails
    /// but the player is within <see cref="NoTouchConstants.ExistentLooseLineOfSightDistance"/> and
    /// <see cref="IPlayer.CurrentEntitySelection"/> is this entity, allow the existent right-click.
    /// </summary>
    private static bool LineOfSightToEntity(ICoreServerAPI sapi, EntityPlayer e, Entity target)
    {
        foreach (double y in GetEntityAimYSamples(target))
        {
            if (LineOfSightRay(
                    sapi,
                    e,
                    target.Pos.X,
                    y,
                    target.Pos.Z,
                    null,
                    target,
                    ignoreNonCollidingBlocks: true))
            {
                return true;
            }
        }
        double d = e.Pos.SquareDistanceTo(target.Pos);
        if (d <= NoTouchConstants.ExistentLooseLineOfSightDistanceSq
            && e.Player is IPlayer pl
            && pl.CurrentEntitySelection?.Entity?.EntityId == target.EntityId)
        {
            return true;
        }
        return false;
    }

    private static bool LineOfSightToBlock(ICoreServerAPI sapi, EntityPlayer e, BlockPos p) =>
        LineOfSightRay(
            sapi,
            e,
            p.X + 0.5,
            p.Y + 0.5,
            p.Z + 0.5,
            p,
            null,
            ignoreNonCollidingBlocks: false);

    /// <summary>Several Y offsets along the target body; ray to feet is unreliable.</summary>
    private static IEnumerable<double> GetEntityAimYSamples(Entity target)
    {
        double y0 = target.Pos.Y;
        if (target.CollisionBox is { } cb)
        {
            double h = System.Math.Max(0.12, cb.Y2 - cb.Y1);
            double b = y0 + cb.Y1;
            yield return b + h * 0.2;
            yield return b + h * 0.5;
            yield return b + h * 0.8;
        }
        else
        {
            yield return y0 + 0.2;
            yield return y0 + 0.55;
            yield return y0 + 0.9;
        }
    }

    /// <summary>
    /// API documentation (<c>VintagestoryAPI.xml</c>, IWorldAccessor.RayTraceForSelection):
    /// ray stops at the first block or entity selection box; <c>bfilter</c> "Return false to ignore" that block and continue.
    /// Use <paramref name="ignoreNonCollidingBlocks"/> for entity tests only: blocks with no collision boxes
    /// should not block LoS to a mob. Block-totem checks pass <c>false</c> so the aimed block is still a valid
    /// hit even if it has no physics boxes (avoids false negatives on that path).
    /// </summary>
    private static bool LineOfSightRay(
        ICoreServerAPI sapi,
        EntityPlayer eplr,
        double tx,
        double ty,
        double tz,
        BlockPos? blockWanted,
        Entity? preferEntity,
        bool ignoreNonCollidingBlocks)
    {
        IWorldAccessor w = sapi.World;
        IBlockAccessor acc = w.BlockAccessor;
        var start = new Vec3d(eplr.Pos.X, eplr.Pos.Y, eplr.Pos.Z);
        start = start.Add(
            eplr.LocalEyePos.X,
            eplr.LocalEyePos.Y,
            eplr.LocalEyePos.Z);
        var end = new Vec3d(tx, ty, tz);
        BlockSelection? bs = null;
        EntitySelection? es = null;
        BlockFilter? bfilter = null;
        if (ignoreNonCollidingBlocks)
        {
            bfilter = (BlockPos pos, Block block) =>
            {
                Cuboidf[]? boxes = block.GetCollisionBoxes(
                    acc,
                    pos);
                if (boxes is null || boxes.Length == 0)
                {
                    return false;
                }
                return true;
            };
        }
        w.RayTraceForSelection(
            start,
            end,
            ref bs,
            ref es,
            bfilter,
            null);
        if (preferEntity is not null)
        {
            return es?.Entity?.EntityId == preferEntity.EntityId;
        }
        if (es is not null)
        {
            return false;
        }
        if (blockWanted is null)
        {
            return false;
        }
        return bs is not null
            && bs.Position.X == blockWanted.X
            && bs.Position.Y == blockWanted.Y
            && bs.Position.Z == blockWanted.Z;
    }
}