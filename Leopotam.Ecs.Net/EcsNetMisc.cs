using System;
using System.Collections.Generic;

namespace Leopotam.Ecs.Net
{
    [Flags]
    public enum EcsNetComponentFlags
    {
        IS_EVENT = 1,
        WAS_REMOVED = 2
    }
    
    public class ClientInfo
    {
        
    }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EcsNetComponentUidAttribute : Attribute
    {
        public int Uid { get; }

        public EcsNetComponentUidAttribute(int uid)
        {
            Uid = uid;
        }
    }
    
    public class EcsNetworkConfig
    {
        public readonly Random Random = new Random();
        public int ThisClientId;
        public Dictionary<long, int> NetworkEntitiesToLocal;
        public Dictionary<int, long> LocalEntitiesToNetwork;

        public IRetranslator Retranslator;
        public ISerializator Serializator;
    }

    public static class RandomExtensions
    {
        public static long NextInt64(this Random rnd)
        {
            var buffer = new byte[sizeof(long)];
            rnd.NextBytes(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }
    }
}