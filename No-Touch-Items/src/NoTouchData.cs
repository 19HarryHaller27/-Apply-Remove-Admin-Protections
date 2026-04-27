using System;
using System.Collections.Generic;

namespace NoTouchItems;

[Serializable]
public sealed class NoTouchData
{
    public List<long> protectedEntityIds = new();

    public List<string> protectedBlockKeys = new();

    /// <summary>Future-apply pattern rules. Only entities/blocks that appear <em>after</em> <see cref="NoTouchFutureRule.SinceRealMs"/> (server ms).</summary>
    public List<NoTouchFutureRule> futureRules = new();
}

[Serializable]
public sealed class NoTouchFutureRule
{
    /// <summary>Path-style match, e.g. "drifter-*" or "game:lantern-large-up". * is wildcard for one segment.</summary>
    public string Pattern = "";

    public bool ForEntities = true;

    public bool ForBlocks = true;

    /// <summary>Server <see cref="IWorldAccessor.Realm"/> time - we use <see cref="ICoreServerAPI.ElapsedMilliseconds"/> for simplicity (future-only).</summary>
    public long SinceRealMs;
}
