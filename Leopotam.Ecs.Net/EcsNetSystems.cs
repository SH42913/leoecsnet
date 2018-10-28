using System;
using System.Collections.Generic;

namespace Leopotam.Ecs.Net
{
    [EcsInject]
    public class RetranslatorSystem : IEcsRunSystem, IEcsInitSystem
    {
        private EcsWorld _ecsWorld = null;
        private EcsNetworkConfig _networkConfig = null;

        private EcsFilter<StartListenerEvent> _startEvents = null;
        private EcsFilter<StopListenerEvent> _stopEvents = null;

        private EcsFilter<SendNetworkComponentEvent> _sendEvents = null;
        private EcsFilter<ConnectToEvent> _connectEvents = null;
        private EcsFilter<RemoveNetworkEntityEvent> _removeEntityEvents = null;
        
        private EcsFilter<NewEntityReceivedEvent> _newEntities = null;
        private EcsFilter<ReceivedNetworkComponentEvent> _receivedComponents = null;

        private EcsFilter<ClientConnectedEvent> _connectedClients = null;
        private EcsFilter<ClientDisconnectedEvent> _disconnectedClients = null;

#if DEBUG
        private EcsFilter<PrepareToSendCountEvent> _prepareEvents = null;
#endif

        public void Initialize()
        {
            _networkConfig.LocalEntitiesToNetwork = new Dictionary<int, long>();
            _networkConfig.NetworkEntitiesToLocal = new Dictionary<long, int>();
            _networkConfig.NetworkUidToType = new Dictionary<short, Type>();
        }

        public void Run()
        {
            StartStopListener();
            ReceiveConnects();
            SendComponents();
            ReceiveComponents();
            RemoveNetworkEntities();
        }

        private void StartStopListener()
        {
            if (_startEvents.EntitiesCount > 0)
            {
                _startEvents.RemoveAllEntities();
                
                if (!_networkConfig.EcsNetworkListener.IsRunning)
                {
                    _networkConfig.EcsNetworkListener.Start(_networkConfig);
                }
#if DEBUG
                else
                {
                    throw new Exception("EcsNetworkListener is already started");
                }
#endif
            }

            if (_stopEvents.EntitiesCount > 0)
            {
                _stopEvents.RemoveAllEntities();
                
                if (_networkConfig.EcsNetworkListener.IsRunning)
                {
                    _networkConfig.EcsNetworkListener.Stop();
                }
#if DEBUG
                else
                {
                    throw new Exception("EcsNetworkListener is already stopped");
                }
#endif
            }
        }

        private void ReceiveConnects()
        {
            UpdateConnectedClients();
            UpdateDisconnectedClients();

            foreach (int i in _connectEvents)
            {
                ConnectToEvent connectEvent = _connectEvents.Components1[i];
                _networkConfig.EcsNetworkListener.Connect(connectEvent.Address, connectEvent.Port);
                _ecsWorld.RemoveEntity(_connectEvents.Entities[i]);
            }
        }

        private void UpdateConnectedClients()
        {
            foreach (int i in _connectedClients)
            {
                _connectedClients.Components1[i].ConnectedClient = null;
                _ecsWorld.RemoveEntity(_connectedClients.Entities[i]);
            }

            foreach (ClientInfo clientInfo in _networkConfig.EcsNetworkListener.GetConnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientConnectedEvent>().ConnectedClient = clientInfo;
            }
        }

        private void UpdateDisconnectedClients()
        {
            foreach (int i in _disconnectedClients)
            {
                _disconnectedClients.Components1[i].DisconnectedClient = null;
                _ecsWorld.RemoveEntity(_disconnectedClients.Entities[i]);
            }

            foreach (ClientInfo clientInfo in _networkConfig.EcsNetworkListener.GetDisconnectedClients())
            {
                _ecsWorld.CreateEntityWith<ClientDisconnectedEvent>().DisconnectedClient = clientInfo;
            }
        }

        private void SendComponents()
        {
#if DEBUG
            int prepareCount = _prepareEvents.EntitiesCount;
            int sendCount = _sendEvents.EntitiesCount;
            if (prepareCount != sendCount)
            {
                throw new Exception(string.Format("You have {0} PrepareEvents and {1} SendEvents." +
                                                  "Did you register all NetworkComponentProcessSystems?", prepareCount, sendCount));
            }
            _prepareEvents.RemoveAllEntities();
#endif
            
            foreach (int i in _sendEvents)
            {
                SendNetworkComponentEvent sendEvent = _sendEvents.Components1[i];
                _networkConfig.EcsNetworkListener.AddComponentsForSend(sendEvent);
                sendEvent.ComponentBytes = null;
            }
            _sendEvents.RemoveAllEntities();
            
            _networkConfig.EcsNetworkListener.Send();
        }

        private void ReceiveComponents()
        {
#if DEBUG
            if (_receivedComponents.EntitiesCount > 0)
            {
                throw new Exception("Not all received components was processed. " +
                                    "Did you register all NetworkComponentProcessSystems?");
            }
#endif
            
            _newEntities.RemoveAllEntities();
            _receivedComponents.RemoveAllEntities();
            
            foreach (ReceivedNetworkComponentEvent receivedComponent in _networkConfig.EcsNetworkListener.GetReceivedComponents())
            {
                ReceivedNetworkComponentEvent receivedNetworkComponentEvent;
                _ecsWorld.CreateEntityWith(out receivedNetworkComponentEvent);
                
                receivedNetworkComponentEvent.ComponentFlags = receivedComponent.ComponentFlags;
                receivedNetworkComponentEvent.NetworkEntityUid = receivedComponent.NetworkEntityUid;
                receivedNetworkComponentEvent.ComponentTypeUid = receivedComponent.ComponentTypeUid;
                receivedNetworkComponentEvent.ComponentBytes = receivedComponent.ComponentBytes;
            }
        }

        private void RemoveNetworkEntities()
        {
            foreach (int i in _removeEntityEvents)
            {
                int localEntity = _removeEntityEvents.Components1[i].LocalEntity;
                if(!_networkConfig.LocalEntitiesToNetwork.ContainsKey(localEntity)) continue;

                long networkEntity = _networkConfig.LocalEntitiesToNetwork[localEntity];
                _networkConfig.NetworkEntitiesToLocal.Remove(networkEntity);
                _networkConfig.LocalEntitiesToNetwork.Remove(localEntity);
            }
            _removeEntityEvents.RemoveAllEntities();
        }

        public void Destroy()
        {
            if (_networkConfig.EcsNetworkListener.IsRunning)
            {
                _networkConfig.EcsNetworkListener.Stop();
            }
            _networkConfig.EcsNetworkListener = null;
            _networkConfig.LocalEntitiesToNetwork = null;
            _networkConfig.NetworkEntitiesToLocal = null;
            _networkConfig.NetworkUidToType = null;
            _networkConfig.Serializator = null;

            foreach (int i in _receivedComponents)
            {
                _receivedComponents.Components1[i].ComponentBytes = null;
            }
            
            foreach (int i in _sendEvents)
            {
                _sendEvents.Components1[i].ComponentBytes = null;
            }
        }
    }

    [EcsInject]
    public abstract class BaseNetworkProcessSystem<T> : IEcsInitSystem, IEcsRunSystem
        where T : class, new()
    {
        public short ComponentUid { get; protected set; }

        protected EcsWorld EcsWorld = null;
        protected EcsNetworkConfig NetworkConfig = null;

        protected EcsFilter<ReceivedNetworkComponentEvent> ReceivedComponents = null;
        protected EcsFilter<PrepareComponentToSendEvent<T>> ComponentsToPrepare = null;

        private bool _uidIsInited;

        public void Initialize()
        {
            if(_uidIsInited) return;
            
            var attr = Attribute.GetCustomAttribute(typeof(T), typeof(EcsNetComponentUidAttribute)) as EcsNetComponentUidAttribute;
#if DEBUG
            if (attr == null)
            {
                throw new Exception(typeof(T).Name + " doesn't has " + nameof(EcsNetComponentUidAttribute));
            }
            else if (NetworkConfig.NetworkUidToType.ContainsKey(attr.Uid))
            {
                throw new Exception(string.Format("Component with UID {0} already registered", attr.Uid));
            }
#endif
            ComponentUid = attr.Uid;
            NetworkConfig.NetworkUidToType.Add(attr.Uid, typeof(T));
            _uidIsInited = true;
        }

        public void Run()
        {
            foreach (int i in ReceivedComponents)
            {
                ReceivedNetworkComponentEvent receivedComponent = ReceivedComponents.Components1[i];
                if (receivedComponent.ComponentTypeUid != ComponentUid) continue;

                ProcessReceivedComponent(receivedComponent);
                receivedComponent.ComponentBytes = null;
                EcsWorld.RemoveEntity(ReceivedComponents.Entities[i]);
            }

            foreach (int i in ComponentsToPrepare)
            {
                PrepareComponentToNetwork(ComponentsToPrepare.Components1[i]);
            }
            ComponentsToPrepare.RemoveAllEntities();
        }

        protected abstract void ProcessReceivedComponent(ReceivedNetworkComponentEvent receivedComponent);

        protected abstract void PrepareComponentToNetwork(PrepareComponentToSendEvent<T> componentToNetwork);

        protected void AddNetworkToLocalEntity(long network, int local)
        {
            NetworkConfig.NetworkEntitiesToLocal.Add(network, local);
            NetworkConfig.LocalEntitiesToNetwork.Add(local, network);
        }

        protected void RemoveNetworkToLocalEntity(long network, int local)
        {
            NetworkConfig.NetworkEntitiesToLocal.Remove(network);
            NetworkConfig.LocalEntitiesToNetwork.Remove(local);
        }

        public void Destroy()
        {
            NetworkConfig.NetworkUidToType.Remove(ComponentUid);
        }
    }

    [EcsInject]
    public sealed class NetworkComponentProcessSystem<TComponent> : BaseNetworkProcessSystem<TComponent>
        where TComponent : class, new()
    {
        private Action<TComponent, TComponent> NewToOldConverter { get; }

        public NetworkComponentProcessSystem(Action<TComponent, TComponent> newToOldConverter)
        {
            NewToOldConverter = newToOldConverter;
        }

        protected override void ProcessReceivedComponent(ReceivedNetworkComponentEvent received)
        {
            TComponent oldComponent;
            long networkEntity = received.NetworkEntityUid;
            bool localEntityExist = NetworkConfig.NetworkEntitiesToLocal.ContainsKey(networkEntity);
            int localEntity;
            bool componentWasRemoved = received.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);

            if (componentWasRemoved && !localEntityExist)
            {
#if DEBUG
                throw new Exception(string.Format("Attempt to remove {0} for non exist local entity", typeof(TComponent).Name));
#endif
                return;
            }

            if (localEntityExist)
            {
                localEntity = NetworkConfig.NetworkEntitiesToLocal[received.NetworkEntityUid];
                bool isNew;
                oldComponent = EcsWorld.EnsureComponent<TComponent>(localEntity, out isNew);
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
                if (EcsWorld.IsEntityExists(localEntity)) return;

                RemoveNetworkToLocalEntity(networkEntity, localEntity);
            }
            else
            {
                var newComponent = NetworkConfig.Serializator.GetComponentFromBytes<TComponent>(received.ComponentBytes);
                NewToOldConverter(newComponent, oldComponent);
            }
        }

        protected override void PrepareComponentToNetwork(PrepareComponentToSendEvent<TComponent> prepareComponent)
        {
            TComponent componentToSend = EcsWorld.GetComponent<TComponent>(prepareComponent.LocalEntityUid);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);

#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception(string.Format("{0} doesn't exist on this entity", typeof(TComponent).Name));
            }
#endif
            SendNetworkComponentEvent sendEvent;
            EcsWorld.CreateEntityWith(out sendEvent);
            sendEvent.ComponentTypeUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;

            int localEntity = prepareComponent.LocalEntityUid;
            bool localEntityExist = NetworkConfig.LocalEntitiesToNetwork.ContainsKey(localEntity);
            long networkEntity;

            if (componentWasRemoved)
            {
#if DEBUG
                if (!localEntityExist)
                {
                    throw new Exception(string.Format("You've tried to send removed {0} for not network entity", typeof(TComponent).Name));
                }
#endif

                networkEntity = NetworkConfig.LocalEntitiesToNetwork[localEntity];
                sendEvent.NetworkEntityUid = networkEntity;

                if (EcsWorld.IsEntityExists(localEntity)) return;
                RemoveNetworkToLocalEntity(networkEntity, localEntity);
            }
            else
            {
                sendEvent.ComponentBytes = NetworkConfig.Serializator.GetBytesFromComponent(componentToSend);

                if (localEntityExist)
                {
                    networkEntity = NetworkConfig.LocalEntitiesToNetwork[localEntity];
                    sendEvent.NetworkEntityUid = networkEntity;
                }
                else
                {
                    do
                    {
                        networkEntity = NetworkConfig.Random.NextInt64();
                    } 
                    while (NetworkConfig.NetworkEntitiesToLocal.ContainsKey(networkEntity));
                    
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
        private Action<TEvent, TEvent> NewToOldConverter { get; }

        public NetworkEventProcessSystem(Action<TEvent, TEvent> newToOldConverter)
        {
            NewToOldConverter = newToOldConverter;
        }

        protected override void ProcessReceivedComponent(ReceivedNetworkComponentEvent received)
        {
            TEvent newEvent;
            var receivedEvent = NetworkConfig.Serializator.GetComponentFromBytes<TEvent>(received.ComponentBytes);
            EcsWorld.CreateEntityWith(out newEvent);
            NewToOldConverter(receivedEvent, newEvent);
        }

        protected override void PrepareComponentToNetwork(PrepareComponentToSendEvent<TEvent> prepareComponent)
        {
            TEvent componentToSend = EcsWorld.GetComponent<TEvent>(prepareComponent.LocalEntityUid);
            bool componentWasRemoved = prepareComponent.ComponentFlags.HasFlag(EcsNetComponentFlags.WAS_REMOVED);
#if DEBUG
            if (!componentWasRemoved && componentToSend == null)
            {
                throw new Exception(string.Format("Component {0} doesn't exist on this entity", typeof(TEvent).Name));
            }
#endif
            SendNetworkComponentEvent sendEvent;
            EcsWorld.CreateEntityWith(out sendEvent);
            sendEvent.ComponentTypeUid = ComponentUid;
            sendEvent.ComponentFlags = prepareComponent.ComponentFlags;
            sendEvent.NetworkEntityUid = 0;
            sendEvent.ComponentBytes = NetworkConfig.Serializator.GetBytesFromComponent(componentToSend);
        }
    }
}