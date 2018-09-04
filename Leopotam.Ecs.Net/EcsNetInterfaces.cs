namespace Leopotam.Ecs.Net
{
    public interface ISerializator
    {
        byte[] GetBytesFromComponent<T>(T component) where T : class, new();
        T GetComponentFromBytes<T>(byte[] bytes) where T : class, new();
    }
    
    public interface IRetranslator
    {
        void Start();
        void Stop();
        bool IsRunning();
        
        ClientInfo[] GetConnectedClients();
        ClientInfo[] GetDisconnectedClients();
        
        void SendComponent(SendNetworkComponentEvent component);
        ReceivedNetworkComponentEvent[] GetReceivedComponents();
    }
}