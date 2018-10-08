namespace Leopotam.Ecs.Net
{
    public static class EcsExtensions
    {
        public static void SendComponentToNetwork<TComponent>(this EcsWorld ecsWorld, int entity) 
            where TComponent : class, new()
        {
            PrepareComponentToSendEvent<TComponent> prepare;
            ecsWorld.CreateEntityWith(out prepare);
            prepare.LocalEntityUid = entity;
            prepare.ComponentFlags = 0;

#if DEBUG
            ecsWorld.CreateEntityWith<PrepareToSendCountEvent>();
#endif
        }
        
        public static void SendRemovedComponentToNetwork<TComponent>(this EcsWorld ecsWorld, int entity) 
            where TComponent : class, new()
        {
            PrepareComponentToSendEvent<TComponent> prepare;
            ecsWorld.CreateEntityWith(out prepare);
            prepare.LocalEntityUid = entity;
            prepare.ComponentFlags = EcsNetComponentFlags.WAS_REMOVED;

#if DEBUG
            ecsWorld.CreateEntityWith<PrepareToSendCountEvent>();
#endif
        }
        
        public static TEvent SendEventToNetwork<TEvent>(this EcsWorld ecsWorld) 
            where TEvent : class, new()
        {
            TEvent newEvent;
            PrepareComponentToSendEvent<TEvent> prepare;
            int entity = ecsWorld.CreateEntityWith(out newEvent);
            ecsWorld.CreateEntityWith(out prepare);
            prepare.LocalEntityUid = entity;
            prepare.ComponentFlags = EcsNetComponentFlags.IS_EVENT;

#if DEBUG
            ecsWorld.CreateEntityWith<PrepareToSendCountEvent>();
#endif

            return newEvent;
        }

        public static void RemoveAllEntities(this EcsFilter filter)
        {
            var world = filter.GetWorld();
            for (var i = 0; i < filter.EntitiesCount; i++) {
                world.RemoveEntity (filter.Entities[i]);
            }
        }
    }
}