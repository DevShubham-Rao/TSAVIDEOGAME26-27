using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SignalLost
{
    /// <summary>Shared Newtonsoft settings: C# camelCase fields map to snake_case JSON keys.</summary>
    public static class JsonUtil
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.Indented,
        };

        public static T Parse<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public static string Write(object o)
        {
            return JsonConvert.SerializeObject(o, Settings);
        }
    }
}
