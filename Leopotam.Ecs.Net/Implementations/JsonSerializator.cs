using System.Text;
using Newtonsoft.Json;

namespace Leopotam.Ecs.Net.Implementations
{
    public class JsonSerializator : ISerializator
    {
        public byte[] GetBytesFromComponent<T>(T component) where T : class, new()
        {
            string json = JsonConvert.SerializeObject(component);
            return Encoding.UTF8.GetBytes(json);
        }

        public T GetComponentFromBytes<T>(byte[] bytes) where T : class, new()
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}