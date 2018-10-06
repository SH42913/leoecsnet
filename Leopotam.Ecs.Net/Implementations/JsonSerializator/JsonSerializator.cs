using System.Text;
using Newtonsoft.Json;

namespace Leopotam.Ecs.Net.Implementations.JsonSerializator
{
    public class JsonSerializator : ISerializator
    {
        public byte[] GetBytesFromComponent<T>(T component) where T : class, new()
        {
            string json = JsonConvert.SerializeObject(component);
            return Encoding.ASCII.GetBytes(json);
        }

        public T GetComponentFromBytes<T>(byte[] bytes) where T : class, new()
        {
            string json = Encoding.ASCII.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}