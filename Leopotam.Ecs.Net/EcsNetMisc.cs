using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public string Address;
        public short Port;
    }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EcsNetComponentUidAttribute : Attribute
    {
        public short Uid { get; }

        public EcsNetComponentUidAttribute(short uid)
        {
            Uid = uid;
        }
    }
    
    public class EcsNetworkConfig
    {
        [EcsIgnoreNullCheck]
        public readonly Random Random = new Random();
        
        public string LocalAddress;
        public short LocalPort;
        
        public Dictionary<long, int> NetworkEntitiesToLocal;
        public Dictionary<int, long> LocalEntitiesToNetwork;

        public IEcsNetworkListener EcsNetworkListener;
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

    public static class StreamExtensions
    {
        public static void WriteAsciiString(this Stream stream, string ascii)
        {
            byte[] asciiBytes = Encoding.ASCII.GetBytes(ascii);
            stream.Write(asciiBytes, 0, asciiBytes.Length);
        }
        
        public static void WriteShort(this Stream stream, short int16)
        {
            byte[] shortBytes = BitConverter.GetBytes(int16);
            stream.Write(shortBytes, 0, shortBytes.Length);
        }
        
        public static void WriteLong(this Stream stream, long int64)
        {
            byte[] longBytes = BitConverter.GetBytes(int64);
            stream.Write(longBytes, 0, longBytes.Length);
        }


        public static string ReadAsciiString(this Stream stream, int symbolCount)
        {
            byte[] asciiBytes = new byte[symbolCount];
            int count = stream.Read(asciiBytes, 0, symbolCount);
            if (count == 0)
            {
                throw new Exception("Disconnected");
            }
            return Encoding.ASCII.GetString(asciiBytes);
        }
        
        public static short ReadShort(this Stream stream)
        {
            byte[] shortBytes = new byte[2];
            int count = stream.Read(shortBytes, 0, 2);
            if (count == 0)
            {
                throw new Exception("Disconnected");
            }
            return BitConverter.ToInt16(shortBytes, 0);
        }

        public static long ReadLong(this Stream stream)
        {
            byte[] longBytes = new byte[8];
            int count = stream.Read(longBytes, 0, 8);
            if (count == 0)
            {
                throw new Exception("Disconnected");
            }
            return BitConverter.ToInt64(longBytes, 0);
        }
    }
}