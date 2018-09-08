using System;
using System.Collections.Generic;

namespace Leopotam.Ecs.Net
{
    [EcsInject]
    public class RetranslatorSystem : IEcsRunSystem, IEcsInitSystem
    {
        private EcsWorld _ecsWorld = null;
        private EcsFilterSingle<EcsNetworkConfig> _config = null;

        private EcsFilter<StartRetranslatorEvent> _startEvents = null;
        private EcsFilter<StopRetranslatorEvent> _stopEvents = null;
        private EcsFilter<SendNetworkComponentEvent> _sendEvents = null;

        private EcsFilter<ClientConnectedEvent> _connectedClients = null;
        private EcsFilter<ClientDisconnectedEvent> _disconnectedClients = null;

        public void Initialize()
        {
            _config.Data.LocalEntitiesToNetworkGuid = new Dictionary<int, Guid>();
            _config.Data.NetworkEntitiesGuidToLocal = new Dictionary<Guid, int>();
        }
        
        public void Run()
        {
            StartStopRetranslator();
            ReceiveConnects();
            SendComponents();
            ReceiveComponents();
        }

        private void StartStopRetranslator()
        {
            if (_startEvents.EntitiesCount > 0)
            {
                if (!_config.Data.Retranslator.IsRunning())
                {
                    _config.Data.Retranslator.Start();
                }
#if DEBUG
                else
                {
                    throw new Exception("Retranslator is already started");
                }
#endif
                
                _startEvents.RemoveAllEntities();
            }
            
            if (_stopEvents.EntitiesCount > 0)
            {
                if (_config.Data.Retranslator.IsRunning())
                {
                    _config.Data.Retranslator.Stop();
                }
#if DEBUG
                else
                {
                    throw new Exception("Retranslator is already stopped");
                }
#endif
                
                _stopEvents.RemoveAllEntities();
            }
        }

        private void ReceiveConnects()
        {
            _connectedClients.RemoveAllEntities();
            foreach (ClientInfo clientInfo in _config.Data.Retranslator.GetConnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientConnectedEvent>().ConnectedClient = clientInfo;
            }

            _disconnectedClients.RemoveAllEntities();
            foreach (ClientInfo clientInfo in _config.Data.Retranslator.GetDisconnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientDisconnectedEvent>().DisconnectedClient = clientInfo;
            }
        }

        private void SendComponents()
        {
            for (int i = 0; i < _sendEvents.EntitiesCount; i++)
            {
                var sendEvent = _sendEvents.Components1[i];
                _config.Data.Retranslator.SendComponent(sendEvent);
                sendEvent.ComponentBytes = null;
            }
            _sendEvents.RemoveAllEntities();
        }

        private void ReceiveComponents()
        {
            foreach (var receivedComponent in _config.Data.Retranslator.GetReceivedComponents())
            {
                _ecsWorld.CreateEntityWith(out ReceivedNetworkComponentEvent receivedNetworkComponentEvent);
                receivedNetworkComponentEvent.ComponentFlags = receivedComponent.ComponentFlags;
                receivedNetworkComponentEvent.NetworkEntityGuid = receivedComponent.NetworkEntityGuid;
                receivedNetworkComponentEvent.ComponentNetworkUid = receivedComponent.ComponentNetworkUid;
                receivedNetworkComponentEvent.ComponentBytes = receivedComponent.ComponentBytes;
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
        public long ComponentUid { get; protected set; }
        
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
                if (receivedComponent.ComponentNetworkUid != ComponentUid) continue;
                
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
        
        protected void AddNetworkToLocalEntity(Guid network, int local)
        {
            NetworkConfig.Data.NetworkEntitiesGuidToLocal.Add(network, local);
            NetworkConfig.Data.LocalEntitiesToNetworkGuid.Add(local, network);
        }

        protected void RemoveNetworkToLocalEntity(Guid network, int local)
        {
            NetworkConfig.Data.NetworkEntitiesGuidToLocal.Remove(network);
            NetworkConfig.Data.LocalEntitiesToNetworkGuid.Remove(local);
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
            Guid networkEntity = received.NetworkEntityGuid;
            bool localEntityExist = NetworkConfig.Data.NetworkEntitiesGuidToLocal.ContainsKey(networkEntity);
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
                localEntity = NetworkConfig.Data.NetworkEntitiesGuidToLocal[received.NetworkEntityGuid];
                oldComponent = EcsWorld.EnsureComponent<TComponent>(localEntity);
            }
            else
            {
                localEntity = EcsWorld.CreateEntityWith(out oldComponent);
                AddNetworkToLocalEntity(received.NetworkEntityGuid, localEntity);
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
            TComponent componentToSend = EcsWorld.GetComponent<TComponent>(prepareComponent.LocalEntityId);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);
            
#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception($"{typeof(TComponent).Name} doesn't exist on this entity");
            }
#endif
            
            EcsWorld.CreateEntityWith(out SendNetworkComponentEvent sendEvent);
            sendEvent.ComponentNetworkUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;

            int localEntity = prepareComponent.LocalEntityId;
            bool localEntityExist = NetworkConfig.Data.LocalEntitiesToNetworkGuid.ContainsKey(localEntity);
            Guid networkEntity;

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
                    .LocalEntitiesToNetworkGuid[localEntity];
                sendEvent.NetworkEntityGuid = networkEntity;
                
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
                        .LocalEntitiesToNetworkGuid[localEntity];
                    sendEvent.NetworkEntityGuid = networkEntity;
                }
                else
                {
                    networkEntity = Guid.NewGuid();
                    AddNetworkToLocalEntity(networkEntity, localEntity);
                    sendEvent.NetworkEntityGuid = networkEntity;
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
            var receivedEvent = NetworkConfig.Data.Serializator.GetComponentFromBytes<TEvent>(received.ComponentBytes);
            EcsWorld.CreateEntityWith(out TEvent newEvent);
            EventUpdateAction(receivedEvent, newEvent);
        }

        protected override void PrepareComponentToNetwork(PrepareComponentToSendEvent<TEvent> prepareComponent)
        {
            TEvent componentToSend = EcsWorld.GetComponent<TEvent>(prepareComponent.LocalEntityId);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);
#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception($"Component {nameof(TEvent)} doesn't exist on this entity");
            }
#endif
            
            EcsWorld.CreateEntityWith(out SendNetworkComponentEvent sendEvent);
            sendEvent.ComponentNetworkUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;
            sendEvent.NetworkEntityGuid = Guid.Empty;
            sendEvent.ComponentBytes = NetworkConfig.Data.Serializator.GetBytesFromComponent(componentToSend);
        }
    }
}