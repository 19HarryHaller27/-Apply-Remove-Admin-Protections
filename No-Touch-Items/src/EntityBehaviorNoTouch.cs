using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace NoTouchItems;

public sealed class EntityBehaviorNoTouch : EntityBehavior
{
    /// <summary>Must be earlier than vanilla <c>health</c> in <see cref="EntitySidedProperties.Behaviors"/> so
    /// <see cref="OnEntityReceiveDamage"/> runs before <c>EntityBehaviorHealth</c>; see
    /// <see cref="NoTouchServerSystem.TryPromoteNoTouchBehaviorFirst"/>.</summary>
    public const string Code = "!notouch:protected";

    public const string LegacyCode = "notouch:protected";

    public EntityBehaviorNoTouch(Entity entity)
        : base(entity)
    {
    }

    public override string PropertyName() => Code;

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (entity?.Api is not ICoreServerAPI sapi)
        {
            return;
        }

        if (sapi.Side != EnumAppSide.Server)
        {
            return;
        }

        if (!NoTouchServerSystem.IsEntityProtected(entity))
        {
            return;
        }

        // All damage types (melee, bow, fire, world, etc.): no HP change on protected list.
        damage = 0f;
    }

    public override void OnGameTick(float deltaTime)
    {
        if (entity?.Api is not ICoreServerAPI sapi || sapi.Side != EnumAppSide.Server)
        {
            return;
        }
        if (!NoTouchServerSystem.IsEntityProtected(entity) || !entity.Alive)
        {
            return;
        }
        // Blunt: kills velocity every tick so a player (or other agents) cannot push, knock back, or
        // shove; still allows separate systems (teleport, worldgen) to move the entity in principle.
        entity.Pos.Motion.Set(0, 0, 0);
        // Creatures store HP in watched "health" (see Take2Mod/HealthEffects/HealthUtil.cs, Mutations in this repo).
        // Together with OnEntityReceiveDamage (API: EntityBehavior.OnEntityReceiveDamage), keep currenthealth at max.
        if (entity.WatchedAttributes.GetTreeAttribute("health") is ITreeAttribute ht)
        {
            float max = ht.GetFloat("maxhealth", 0f);
            if (max > 0f)
            {
                float cur = ht.GetFloat("currenthealth", max);
                if (cur < max)
                {
                    ht.SetFloat("currenthealth", max);
                    entity.WatchedAttributes.MarkPathDirty("health");
                }
            }
        }
    }
}