using System;
using System.Collections.Generic;

namespace Leopotam.Ecs.Net
{
    [EcsInject]
    public class RetranslatorSystem : IEcsRunSystem, IEcsInitSystem
    {
        private EcsWorld _ecsWorld = null;
        private EcsFilterSingle<EcsNetworkConfig> _config = null;

        private EcsFilter<StartListenerEvent> _startEvents = null;
        private EcsFilter<StopListenerEvent> _stopEvents = null;
        
        private EcsFilter<SendNetworkComponentEvent> _sendEvents = null;
        private EcsFilter<ConnectToEvent> _connectEvents = null;
        private EcsFilter<NewEntityReceivedEvent> _newEntities = null;

        private EcsFilter<ClientConnectedEvent> _connectedClients = null;
        private EcsFilter<ClientDisconnectedEvent> _disconnectedClients = null;

        public void Initialize()
        {
            _config.Data.LocalEntitiesToNetwork = new Dictionary<int, long>();
            _config.Data.NetworkEntitiesToLocal = new Dictionary<long, int>();
        }
        
        public void Run()
        {
            StartStopListener();
            ReceiveConnects();
            SendComponents();
            ReceiveComponents();
        }

        private void StartStopListener()
        {
            if (_startEvents.EntitiesCount > 0)
            {
                if (!_config.Data.EcsNetworkListener.IsRunning)
                {
                    _config.Data.EcsNetworkListener.Start(_config.Data);
                    Console.WriteLine("Listener started");
                }
#if DEBUG
                else
                {
                    throw new Exception("EcsNetworkListener is already started");
                }
#endif
                
                _startEvents.RemoveAllEntities();
            }
            
            if (_stopEvents.EntitiesCount > 0)
            {
                if (_config.Data.EcsNetworkListener.IsRunning)
                {
                    _config.Data.EcsNetworkListener.Stop();
                    Console.WriteLine("Listener stopped");
                }
#if DEBUG
                else
                {
                    throw new Exception("EcsNetworkListener is already stopped");
                }
#endif
                
                _stopEvents.RemoveAllEntities();
            }
        }

        private void ReceiveConnects()
        {
            for (int i = 0; i < _connectedClients.EntitiesCount; i++)
            {
                _connectedClients.Components1[i].ConnectedClient = null;
                _ecsWorld.RemoveEntity(_connectedClients.Entities[i]);
            }
            foreach (ClientInfo clientInfo in _config.Data.EcsNetworkListener.GetConnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientConnectedEvent>().ConnectedClient = clientInfo;
            }

            for (int i = 0; i < _disconnectedClients.EntitiesCount; i++)
            {
                _disconnectedClients.Components1[i].DisconnectedClient = null;
                _ecsWorld.RemoveEntity(_disconnectedClients.Entities[i]);
            }
            foreach (ClientInfo clientInfo in _config.Data.EcsNetworkListener.GetDisconnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientDisconnectedEvent>().DisconnectedClient = clientInfo;
            }

            for (int i = 0; i < _connectEvents.EntitiesCount; i++)
            {
                ConnectToEvent connectEvent = _connectEvents.Components1[i];
                _config.Data.EcsNetworkListener.Connect(connectEvent.Address, connectEvent.Port);
                _ecsWorld.RemoveEntity(_connectEvents.Entities[i]);
            }
        }

        private void SendComponents()
        {
            for (int i = 0; i < _sendEvents.EntitiesCount; i++)
            {
                var sendEvent = _sendEvents.Components1[i];
                _config.Data.EcsNetworkListener.SendComponent(sendEvent);
                sendEvent.ComponentBytes = null;
                Console.WriteLine($"Component {sendEvent.ComponentTypeUid} sended");
            }
            _sendEvents.RemoveAllEntities();
        }

        private void ReceiveComponents()
        {
            _newEntities.RemoveAllEntities();
            foreach (var receivedComponent in _config.Data.EcsNetworkListener.GetReceivedComponents())
            {
                ReceivedNetworkComponentEvent receivedNetworkComponentEvent;
                _ecsWorld.CreateEntityWith(out receivedNetworkComponentEvent);
                receivedNetworkComponentEvent.ComponentFlags = receivedComponent.ComponentFlags;
                receivedNetworkComponentEvent.NetworkEntityUid = receivedComponent.NetworkEntityUid;
                receivedNetworkComponentEvent.ComponentTypeUid = receivedComponent.ComponentTypeUid;
                receivedNetworkComponentEvent.ComponentBytes = receivedComponent.ComponentBytes;
                
                Console.WriteLine($"Component {receivedComponent.ComponentTypeUid} received");
            }
        }

        public void Destroy()
        {
            
        }
    }
    
    [EcsInject]
    public abstract class BaseNetworkProcessSystem<T> : IEcsInitSystem, IEcsRunSystem
        where T : class, new()
    {
        public short ComponentUid { get; protected set; }
        
        protected EcsWorld EcsWorld = null;
        protected EcsFilterSingle<EcsNetworkConfig> NetworkConfig = null;

        protected EcsFilter<ReceivedNetworkComponentEvent> ReceivedComponents = null;
        protected EcsFilter<PrepareComponentToSendEvent<T>> ComponentsToPrepare = null;
        
        public void Initialize()
        {
            var attr = Attribute
                .GetCustomAttribute(typeof(T), typeof(EcsNetComponentUidAttribute)) as EcsNetComponentUidAttribute;
#if DEBUG
            if (attr == null)
            {
                throw new Exception(typeof(T).Name + " doesn't has " + nameof(EcsNetComponentUidAttribute));
            }
#endif
            ComponentUid = attr.Uid;
        }
        
        public void Run()
        {
            for (int i = 0; i < ReceivedComponents.EntitiesCount; i++)
            {
                var receivedComponent = ReceivedComponents.Components1[i];
                if (receivedComponent.ComponentTypeUid != ComponentUid) continue;
                
                ProcessReceivedComponent(receivedComponent);
                receivedComponent.ComponentBytes = null;
                EcsWorld.RemoveEntity(ReceivedComponents.Entities[i]);
            }

            for (int i = 0; i < ComponentsToPrepare.EntitiesCount; i++)
            {
                PrepareComponentToNetwork(ComponentsToPrepare.Components1[i]);
            }
            ComponentsToPrepare.RemoveAllEntities();
        }

        protected abstract void ProcessReceivedComponent(ReceivedNetworkComponentEvent receivedComponent);

        protected abstract void PrepareComponentToNetwork(PrepareComponentToSendEvent<T> componentToNetwork);
        
        protected void AddNetworkToLocalEntity(long network, int local)
        {
            NetworkConfig.Data.NetworkEntitiesToLocal.Add(network, local);
            NetworkConfig.Data.LocalEntitiesToNetwork.Add(local, network);
        }

        protected void RemoveNetworkToLocalEntity(long network, int local)
        {
            NetworkConfig.Data.NetworkEntitiesToLocal.Remove(network);
            NetworkConfig.Data.LocalEntitiesToNetwork.Remove(local);
        }

        public void Destroy()
        {
            
        }
    }
    
    [EcsInject]
    public sealed class NetworkComponentProcessSystem<TComponent> : BaseNetworkProcessSystem<TComponent>
        where TComponent : class, new()
    {
        private Action<TComponent, TComponent> ComponentUpdateAction { get; }

        public NetworkComponentProcessSystem(Action<TComponent, TComponent> componentUpdateAction)
        {
            ComponentUpdateAction = componentUpdateAction;
        }

        protected override void ProcessReceivedComponent(ReceivedNetworkComponentEvent received)
        {
            TComponent oldComponent;
            long networkEntity = received.NetworkEntityUid;
            bool localEntityExist = NetworkConfig.Data.NetworkEntitiesToLocal.ContainsKey(networkEntity);
            int localEntity;
            bool componentWasRemoved = received.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);

            if (componentWasRemoved && !localEntityExist)
            {
#if DEBUG
                throw new Exception($"Attempt to remove {typeof(TComponent).Name} for non exist local entity");
#endif
                return;
            }
            
            if (localEntityExist)
            {
                localEntity = NetworkConfig.Data.NetworkEntitiesToLocal[received.NetworkEntityUid];
                oldComponent = EcsWorld.EnsureComponent<TComponent>(localEntity);
            }
            else
            {
                localEntity = EcsWorld.CreateEntityWith(out oldComponent);
                EcsWorld.CreateEntityWith<NewEntityReceivedEvent>().LocalEntity = localEntity;
                AddNetworkToLocalEntity(received.NetworkEntityUid, localEntity);
            }

            if (componentWasRemoved)
            {
                EcsWorld.RemoveComponent<TComponent>(localEntity);
                if(EcsWorld.IsEntityExists(localEntity)) return;
                
                RemoveNetworkToLocalEntity(networkEntity, localEntity);
            }
            else
            {
                var newComponent = NetworkConfig.Data.Serializator.GetComponentFromBytes<TComponent>(received.ComponentBytes);
                ComponentUpdateAction(newComponent, oldComponent);
            }
        }

        protected override void PrepareComponentToNetwork(PrepareComponentToSendEvent<TComponent> prepareComponent)
        {
            TComponent componentToSend = EcsWorld.GetComponent<TComponent>(prepareComponent.LocalEntityUid);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);
            
#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception($"{typeof(TComponent).Name} doesn't exist on this entity");
            }
#endif
            SendNetworkComponentEvent sendEvent;
            EcsWorld.CreateEntityWith(out sendEvent);
            sendEvent.ComponentTypeUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;

            int localEntity = prepareComponent.LocalEntityUid;
            bool localEntityExist = NetworkConfig.Data.LocalEntitiesToNetwork.ContainsKey(localEntity);
            long networkEntity;

            if (componentWasRemoved)
            {
#if DEBUG
                if (!localEntityExist)
                {
                    throw new Exception($"You tried to send removed {typeof(TComponent).Name} for not network entity");
                }
#endif
                
                networkEntity = NetworkConfig
                    .Data
                    .LocalEntitiesToNetwork[localEntity];
                sendEvent.NetworkEntityUid = networkEntity;
                
                if(EcsWorld.IsEntityExists(localEntity)) return;
                RemoveNetworkToLocalEntity(networkEntity, localEntity);
            }
            else
            {
                sendEvent.ComponentBytes = NetworkConfig.Data.Serializator.GetBytesFromComponent(componentToSend);
                
                if (localEntityExist)
                {
                    networkEntity = NetworkConfig
                        .Data
                        .LocalEntitiesToNetwork[localEntity];
                    sendEvent.NetworkEntityUid = networkEntity;
                }
                else
                {
                    networkEntity = NetworkConfig.Data.Random.NextInt64();
                    AddNetworkToLocalEntity(networkEntity, localEntity);
                    sendEvent.NetworkEntityUid = networkEntity;
                }
            }
        }
    }
    
    [EcsInject]
    public sealed class NetworkEventProcessSystem<TEvent> : BaseNetworkProcessSystem<TEvent>
        where TEvent : class, new()
    {
        private Action<TEvent, TEvent> EventUpdateAction { get; }

        public NetworkEventProcessSystem(Action<TEvent, TEvent> eventUpdateAction)
        {
            EventUpdateAction = eventUpdateAction;
        }
        
        protected override void ProcessReceivedComponent(ReceivedNetworkComponentEvent received)
        {
            TEvent newEvent;
            var receivedEvent = NetworkConfig.Data.Serializator.GetComponentFromBytes<TEvent>(received.ComponentBytes);
            EcsWorld.CreateEntityWith(out newEvent);
            EventUpdateAction(receivedEvent, newEvent);
        }

        protected override void PrepareComponentToNetwork(PrepareComponentToSendEvent<TEvent> prepareComponent)
        {
            TEvent componentToSend = EcsWorld.GetComponent<TEvent>(prepareComponent.LocalEntityUid);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);
#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception($"Component {nameof(TEvent)} doesn't exist on this entity");
            }
#endif
            SendNetworkComponentEvent sendEvent;
            EcsWorld.CreateEntityWith(out sendEvent);
            sendEvent.ComponentTypeUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;
            sendEvent.NetworkEntityUid = 0;
            sendEvent.ComponentBytes = NetworkConfig.Data.Serializator.GetBytesFromComponent(componentToSend);
        }
    }
}