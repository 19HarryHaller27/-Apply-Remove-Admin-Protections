using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace NoTouchItems;

public sealed class NoTouchClientSystem : ModSystem
{
    private const string HotkeyCode = "notouch-cursor-ids";
    private ICoreClientAPI? capi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        api.Input.RegisterHotKey(
            HotkeyCode,
            "No Touch: cursor id (creative) — for /nt protect patterns and b|e|a",
            GlKeys.P,
            HotkeyType.CreativeOrSpectatorTool,
            altPressed: true,
            ctrlPressed: false,
            shiftPressed: false);
        api.Input.SetHotKeyHandler(HotkeyCode, OnInspectKey);
    }

    public override void Dispose()
    {
        if (capi is not null)
        {
            capi.Input.SetHotKeyHandler(HotkeyCode, null!);
        }
        capi = null;
    }

    private bool OnInspectKey(KeyCombination _)
    {
        ICoreClientAPI? c = capi;
        if (c is null
            || c.World?.Player is not IPlayer pl
            || pl.Entity is null
            || pl.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return true;
        }
        BlockSelection? bs = pl.CurrentBlockSelection;
        EntitySelection? es = pl.CurrentEntitySelection;
        if (es?.Entity is { } tEnt && tEnt.EntityId == pl.Entity.EntityId)
        {
            c.ShowChatMessage("No Touch: aim at a block or another entity (not yourself). Default hotkey: Alt+P.");
            return true;
        }
        var line = new StringBuilder(200);
        line.Append("No Touch: ");
        if (es?.Entity is not null
            && es.Entity.Properties?.Code is { } ecode)
        {
            line.Append("Entity path for /nt protect (pattern): \"")
                .Append(ecode.Domain)
                .Append(":")
                .Append(ecode.Path)
                .Append("\". Use [e] or [a] for /nt protect. ");
        }
        else if (bs is not null
            && c.World.BlockAccessor.GetBlock(bs.Position) is { } block
            && block.Id != 0
            && block.Code is { } bcode)
        {
            line.Append("Block path for /nt protect (pattern): \"")
                .Append(bcode.Domain)
                .Append(":")
                .Append(bcode.Path)
                .Append("\". Use [b] or [a].");
        }
        else
        {
            c.ShowChatMessage("No Touch: aim at a block or entity, then press Alt+P (default). Creative only.");
            return true;
        }
        c.ShowChatMessage(line.ToString());
        return true;
    }
}
