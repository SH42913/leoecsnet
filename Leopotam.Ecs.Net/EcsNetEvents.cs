namespace Leopotam.Ecs.Net
{
    public abstract class BaseNetworkComponentEvent
    {
        public EcsNetComponentFlags ComponentFlags;
        public long NetworkEntityUid;
        public short ComponentTypeUid;
        public byte[] ComponentBytes;
    }
    
    public class ReceivedNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }
    
    public class SendNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }

#if DEBUG
    [EcsIgnoreInFilter]
    public class PrepareToSendCountEvent
    {
        
    }
#endif
    
    public class PrepareComponentToSendEvent<TComponent> where TComponent : class, new()
    {
        public int LocalEntityUid;
        public EcsNetComponentFlags ComponentFlags;
    }
    
    public class StartListenerEvent
    {
        
    }
    
    public class StopListenerEvent
    {
        
    }

    public class ConnectToEvent
    {
        public string Address;
        public short Port;
    }
    
    public class ClientConnectedEvent
    {
        public ClientInfo ConnectedClient;
    }
    
    public class ClientDisconnectedEvent
    {
        public ClientInfo DisconnectedClient;
    }

    public class NewEntityReceivedEvent
    {
        public int LocalEntity;
    }

    public class RemoveNetworkEntityEvent
    {
        public int LocalEntity;
    }
}