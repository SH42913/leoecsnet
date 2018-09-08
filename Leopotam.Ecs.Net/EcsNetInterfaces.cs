namespace Leopotam.Ecs.Net
{
    public interface ISerializator
    {
        byte[] GetBytesFromComponent<T>(T component) where T : class, new();
        T GetComponentFromBytes<T>(byte[] bytes) where T : class, new();
    }
    
    public interface IEcsNetworkListener
    {
        bool IsRunning { get; }
        
        void Start(EcsNetworkConfig config);
        void Stop();

        void Connect(string address, short port);
        
        ClientInfo[] GetConnectedClients();
        ClientInfo[] GetDisconnectedClients();
        
        void SendComponent(SendNetworkComponentEvent component);
        ReceivedNetworkComponentEvent[] GetReceivedComponents();
    }
}