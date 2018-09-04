using System;

namespace Leopotam.Ecs.Net
{
    public abstract class BaseNetworkComponentEvent
    {
        public EcsNetTypes Type;
        public Guid NetworkEntityGuid;
        public long ComponentNetworkUid;
        public byte[] ComponentBytes;
        public bool WasRemoved;
    }
    
    public class ReceivedNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }
    
    public class SendNetworkComponentEvent : BaseNetworkComponentEvent
    {
        
    }
    
    public class PrepareComponentToSendEvent<TComponent> where TComponent : class, new()
    {
        public int LocalEntityId;
        public EcsNetTypes Type;
        public bool WasRemoved;
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