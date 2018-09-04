using System;
using System.Collections.Generic;

namespace Leopotam.Ecs.Net
{
    public enum EcsNetTypes
    {
        COMPONENT,
        EVENT
    }
    
    public class ClientInfo
    {
        
    }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EcsNetComponentUidAttribute : Attribute
    {
        public long Uid { get; }

        public EcsNetComponentUidAttribute(long uid)
        {
            Uid = uid;
        }
    }
    
    public class EcsNetworkConfig
    {
        public Guid ThisClientGuid;
        public Dictionary<Guid, int> NetworkEntitiesGuidToLocal;
        public Dictionary<int, Guid> LocalEntitiesToNetworkGuid;

        public IRetranslator Retranslator;
        public ISerializator Serializator;
    }

    public static class RandomExtensions
    {
        public static Int64 NextInt64(this Random rnd)
        {
            var buffer = new byte[sizeof(Int64)];
            rnd.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }
    }
}