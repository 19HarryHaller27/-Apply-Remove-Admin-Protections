namespace NoTouchItems;

public static class NoTouchConstants
{
    /// <summary>Bump when shipping; shown in join message and server log so you can confirm the loaded DLL matches your build.</summary>
    public const string ModVersion = "0.1.31";

    public const string LogPrefix = "[NoTouch]";

    public const string DataKey = "notouchitems-data";

    /// <summary>When true, no-touch diagnostic lines use Notification. When false (default), they use Debug so a normal log stays readable; raise log verbosity to Debug to troubleshoot [NoTouch] issues. Not a const so the log branch is not dead-code-eliminated at compile time.</summary>
    public static bool LogDiagnosticsAsNotification;

    public const string ModId = "notouchitems";

    public const string CodeEntityLantern = "notouch-lantern-entity";
    public const string CodeBlockLantern = "notouch-lantern-block";
    public const string CodeExistentLantern = "notouch-lantern-existent";
    public const string CodeExistentPartnerStick = "notouch-existent-stick";

    public const string NotouchMessageClaimant = "custommessage-notouch-protected";

    /// <summary>Max distance (center to center) from a creative player holding the entity totem; increase if /entity or creative spawns fail when you stand a bit back.</summary>
    public const double StampedSpawnMatchRadius = 20.0;

    /// <summary>
    /// Technical raycast length used when we must resolve aim server-side.
    /// Existent toggling itself has no distance gate.
    /// </summary>
    public const float ExistentRaycastRange = 10000f;

    /// <summary>
    /// When true (default), existent sets <c>HeldPriorityInteract</c> on the item so the engine treats held use like a
    /// priority item. When false, experiment: more vanilla “use block / world” routing may run first; can help or hurt
    /// stamping—flip, rebuild, and test in creative.
    /// </summary>
    public const bool ExistentLanternHeldPriorityInteract = true;

    /// <summary>When line-of-sight samples fail (grass, small obstacles), allow existent entity right-click
    /// if the caller is this close and their crosshair entity matches (same as in-game r-click on totem use).</summary>
    public const double ExistentLooseLineOfSightDistance = 5.0;

    public static readonly double ExistentLooseLineOfSightDistanceSq = ExistentLooseLineOfSightDistance * ExistentLooseLineOfSightDistance;

    /// <summary>Synced to clients: entity has No Touch protection (stamped / entity totem / /nt protect rule).</summary>
    public const string WatchedNotouchTree = "notouchitems";

    public const string WatchedNotouchKeyProtected = "protected";
}
