namespace Leopotam.Ecs.Net
{
    public static class EcsWorldExtensions
    {
        public static void SendComponentToNetwork<TComponent>(this EcsWorld ecsWorld, int entity) 
            where TComponent : class, new()
        {
            ecsWorld.CreateEntityWith(out PrepareComponentToSendEvent<TComponent> prepare);
            prepare.LocalEntityId = entity;
            prepare.WasRemoved = false;
            prepare.Type = EcsNetTypes.COMPONENT;
        }
        
        public static void SendRemovedComponentToNetwork<TComponent>(this EcsWorld ecsWorld, int entity) 
            where TComponent : class, new()
        {
            ecsWorld.CreateEntityWith(out PrepareComponentToSendEvent<TComponent> prepare);
            prepare.LocalEntityId = entity;
            prepare.WasRemoved = true;
            prepare.Type = EcsNetTypes.COMPONENT;
        }
        
        public static TEvent SendEventToNetwork<TEvent>(this EcsWorld ecsWorld) 
            where TEvent : class, new()
        {
            int entity = ecsWorld.CreateEntityWith(out TEvent newEvent);
            ecsWorld.CreateEntityWith(out PrepareComponentToSendEvent<TEvent> prepare);
            prepare.LocalEntityId = entity;
            prepare.WasRemoved = false;
            prepare.Type = EcsNetTypes.EVENT;

            return newEvent;
        }
    }
}