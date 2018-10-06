using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Leopotam.Ecs.Net.Implementations.TcpRetranslator
{
    public class TcpRetranslator : IEcsNetworkListener
    {
        private class Component
        {
            public short ComponentUid;
            public EcsNetComponentFlags Flags;
            public byte[] Bytes;
        }
        
        private class Retranslator
        {
            public string Address;
            public short Port;
            public TcpClient SendClient;
            public TcpClient ReceiveClient;
        }

        private readonly object _locker = new object();
        
        public bool IsRunning { get; private set; }

        private readonly Dictionary<long, List<Component>> _componentsForSend = new Dictionary<long, List<Component>>();
        private readonly List<Component> _eventsForSend = new List<Component>();
        
        private readonly List<ReceivedNetworkComponentEvent> _receivedComponents = new List<ReceivedNetworkComponentEvent>();

        private readonly List<Retranslator> _retranslators = new List<Retranslator>();
        private readonly List<ClientInfo> _connectedClients = new List<ClientInfo>();
        private readonly List<ClientInfo> _disconnectedClients = new List<ClientInfo>();

        private EcsNetworkConfig _config;
        private TcpListener _listener;

        public void Start(EcsNetworkConfig config)
        {
            _config = config;
            _listener = new TcpListener(IPAddress.Parse(config.LocalAddress), config.LocalPort);

            IsRunning = true;
            _listener.Start();
            Task.Run(() => ListenForNewClients());
        }

        public void Stop()
        {
            if(!IsRunning) return;
            
            _listener.Stop();
            IsRunning = false;
        }

        public void Connect(string address, short port)
        {
            TcpClient sendClient = new TcpClient(address, port);
            NetworkStream sendStream = sendClient.GetStream();
            
            sendStream.WriteByte((byte) _config.LocalAddress.Length);
            sendStream.WriteAsciiString(_config.LocalAddress);
            sendStream.WriteShort(_config.LocalPort);
            
            _retranslators.Add(new Retranslator
            {
                Address = address,
                Port = port,
                SendClient = sendClient
            });
        }

        public IEnumerable<ClientInfo> GetConnectedClients()
        {
            var connected = _connectedClients.ToArray();
            _connectedClients.Clear();
            return connected;
        }

        public IEnumerable<ClientInfo> GetDisconnectedClients()
        {
            var disconnected = _disconnectedClients.ToArray();
            _disconnectedClients.Clear();
            return disconnected;
        }

        public void AddComponentsForSend(SendNetworkComponentEvent component)
        {
            lock (_locker)
            {
                if (component.ComponentFlags.HasFlag(EcsNetComponentFlags.IS_EVENT))
                {
                    _eventsForSend.Add(new Component
                    {
                        ComponentUid = component.ComponentTypeUid,
                        Bytes = component.ComponentBytes,
                        Flags = component.ComponentFlags
                    });
                }
                else
                {
                    AddComponent(component);
                }
            }
        }

        private void AddComponent(SendNetworkComponentEvent sendEvent)
        {
            if (_componentsForSend.ContainsKey(sendEvent.NetworkEntityUid))
            {
                var componentsOnNetworkEntity = _componentsForSend[sendEvent.NetworkEntityUid];
                foreach (var component in componentsOnNetworkEntity)
                {
                    if (component.ComponentUid != sendEvent.ComponentTypeUid) continue;
                    
                    component.Bytes = sendEvent.ComponentBytes;
                    return;
                }
                    
                componentsOnNetworkEntity.Add(new Component
                {
                    ComponentUid = sendEvent.ComponentTypeUid,
                    Flags = sendEvent.ComponentFlags,
                    Bytes = sendEvent.ComponentBytes
                });
            }
            else
            {
                var list = new List<Component>();
                _componentsForSend.Add(sendEvent.NetworkEntityUid, list);
                list.Add(new Component
                {
                    ComponentUid = sendEvent.ComponentTypeUid,
                    Flags = sendEvent.ComponentFlags,
                    Bytes = sendEvent.ComponentBytes
                });
            }
        }

        public IEnumerable<ReceivedNetworkComponentEvent> GetReceivedComponents()
        {
            ReceivedNetworkComponentEvent[] received;
            lock (_locker)
            {
                received = _receivedComponents.ToArray();
                _receivedComponents.Clear();
            }
            return received;
        }

        private void ListenForNewClients()
        {
            try
            {
                while (IsRunning)
                {
                    TcpClient newClient = _listener.AcceptTcpClient();
                    AcceptNewClient(newClient);
                }
            }
            finally
            {
                Stop();
            }
        }

        private void AcceptNewClient(TcpClient receiveClient)
        {
            NetworkStream receiveStream = receiveClient.GetStream();
            
            int addressLength = receiveStream.ReadByte();
            string address = receiveStream.ReadAsciiString(addressLength);
            short port = receiveStream.ReadShort();
            Retranslator existingRetranslator = GetRetranslatorIfExistOrNull(address, port);
            
            if (existingRetranslator != null)
            {
                existingRetranslator.ReceiveClient = receiveClient;
                FinalizeConnection(existingRetranslator);
                return;
            }
                    
            TcpClient sendClient = new TcpClient(address, port);
            NetworkStream sendStream = sendClient.GetStream();
            sendStream.WriteByte((byte) _config.LocalAddress.Length);
            sendStream.WriteAsciiString(_config.LocalAddress);
            sendStream.WriteShort(_config.LocalPort);

            var newRetranslator = new Retranslator
            {
                Address = address,
                Port = port,
                ReceiveClient = receiveClient,
                SendClient = sendClient
            };
            _retranslators.Add(newRetranslator);
            FinalizeConnection(newRetranslator);
        }

        private Retranslator GetRetranslatorIfExistOrNull(string address, int port)
        {
            foreach (Retranslator retranslator in _retranslators)
            {
                if (retranslator.Address == address && retranslator.Port == port) return retranslator;
            }

            return null;
        }

        private void FinalizeConnection(Retranslator retranslator)
        {
            _connectedClients.Add(new ClientInfo
            {
                Address = retranslator.Address,
                Port = retranslator.Port
            });
            Task.Run(() => StartListenForNewComponents(retranslator));
        }

        public void Send()
        {
            if(_eventsForSend.Count <= 0 && _componentsForSend.Count <= 0) return;

            Task.Run(() => SendEverything());
        }

        private void SendEverything()
        {
            lock (_locker)
            {
                foreach (Retranslator retranslator in _retranslators)
                {
                    TcpClient sendClient = retranslator.SendClient;

                    try
                    {
                        NetworkStream stream = sendClient.GetStream();
                        SendAllEventsToNetwork(stream);
                        SendAllComponentsToNetwork(stream);
                    }
                    catch (Exception)
                    {
                        CloseRetranslator(retranslator);
                    }
                }
            
                _eventsForSend.Clear();
                _componentsForSend.Clear();
            }
        }

        private void StartListenForNewComponents(Retranslator retranslator)
        {
            TcpClient receiveClient = retranslator.ReceiveClient;
            NetworkStream stream = receiveClient.GetStream();

            try
            {
                while (IsRunning)
                {
                    ReceiveEvents(stream);
                    ReceiveComponents(stream);
                }
            }
            finally
            {
                CloseRetranslator(retranslator);
            }         
        }

        private void ReceiveEvents(NetworkStream stream)
        {
            short eventCount = stream.ReadShort();
            lock (_locker)
            {
                for (int i = 0; i < eventCount; i++)
                {
                    ReceiveEvent(stream);
                }
            }
        }

        private void ReceiveEvent(NetworkStream stream)
        {
            var receivedEvent = new ReceivedNetworkComponentEvent
            {
                ComponentTypeUid = stream.ReadShort(),
                ComponentFlags = (EcsNetComponentFlags) stream.ReadByte()
            };

            short eventSize = stream.ReadShort();
            byte[] eventBytes = new byte[eventSize];
            stream.Read(eventBytes, 0, eventSize);
            receivedEvent.ComponentBytes = eventBytes;
            
            _receivedComponents.Add(receivedEvent);
        }

        private void ReceiveComponents(NetworkStream stream)
        {
            short entityCount = stream.ReadShort();
            lock (_locker)
            {
                for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
                {
                    long networkEntity = stream.ReadLong();
                    short componentCount = stream.ReadShort();
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        ReceiveComponent(stream, networkEntity);
                    }
                }
            }
        }

        private void ReceiveComponent(NetworkStream stream, long networkEntity)
        {
            var receivedNetworkComponent = new ReceivedNetworkComponentEvent
            {
                NetworkEntityUid = networkEntity,
                ComponentTypeUid = stream.ReadShort(),
                ComponentFlags = (EcsNetComponentFlags) stream.ReadByte()
            };

            short componentSize = stream.ReadShort();
            byte[] componentBytes = new byte[componentSize];
            stream.Read(componentBytes, 0, componentSize);
            receivedNetworkComponent.ComponentBytes = componentBytes;
            
            _receivedComponents.Add(receivedNetworkComponent);
        }

        private void SendAllEventsToNetwork(NetworkStream stream)
        {
            stream.WriteShort((short) _eventsForSend.Count);

            foreach (Component eventToSend in _eventsForSend)
            {
                SendComponentToNetwork(stream, eventToSend);
            }
        }

        private void SendAllComponentsToNetwork(NetworkStream stream)
        {
            stream.WriteShort((short) _componentsForSend.Keys.Count);

            foreach (long networkEntity in _componentsForSend.Keys)
            {
                stream.WriteLong(networkEntity);
                
                var components = _componentsForSend[networkEntity];
                stream.WriteShort((short) components.Count);
                foreach (Component componentToSend in components)
                {
                    SendComponentToNetwork(stream, componentToSend);
                }
            }
        }

        private void SendComponentToNetwork(NetworkStream stream, Component component)
        {
            stream.WriteShort(component.ComponentUid);
            stream.WriteByte((byte) component.Flags);
            stream.WriteShort((short) component.Bytes.Length);
            stream.Write(component.Bytes, 0, component.Bytes.Length);
        }

        private void CloseRetranslator(Retranslator retranslator)
        {
            retranslator.ReceiveClient.Close();
            retranslator.SendClient.Close();
            _retranslators.Remove(retranslator);
            _disconnectedClients.Add(new ClientInfo
            {
                Address = retranslator.Address,
                Port = retranslator.Port
            });
        }
    }
}