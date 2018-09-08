namespace Leopotam.Ecs.Net
{
    public abstract class BaseNetworkComponentEvent
    {
        public EcsNetComponentFlags ComponentFlags;
        public long NetworkEntityUid;
        public long ComponentTypeUid;
        public byte[] ComponentBytes;
    }
    
    public class ReceivedNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }
    
    public class SendNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }
    
    public class PrepareComponentToSendEvent<TComponent> where TComponent : class, new()
    {
        public int LocalEntityUid;
        public EcsNetComponentFlags ComponentFlags;
    }
    
    public class StartRetranslatorEvent
    {
        
    }
    
    public class StopRetranslatorEvent
    {
        
    }
    
    public class ClientConnectedEvent
    {
        public ClientInfo ConnectedClient;
    }
    
    public class ClientDisconnectedEvent
    {
        public ClientInfo DisconnectedClient;
    }
}