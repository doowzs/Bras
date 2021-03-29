using Newtonsoft.Json;

namespace Bras
{
    public class GeneralConfig
    {
        public int Interval { get; set; }
    }

    public class BrasConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class DnsPodConfig
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public string Domain { get; set; }
        public string SubDomain { get; set; }
    }
    
    public class Config
    {
        [JsonProperty("general")] public GeneralConfig General;
        [JsonProperty("bras")] public BrasConfig Bras;
        [JsonProperty("dnspod")] public DnsPodConfig DnsPod;
    }
}