using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Bras
{
    class Credential
    {
        [JsonProperty("domain")] public const string Domain = "default";
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("password")] public string Password { get; set; }
        [JsonProperty("challenge")] public string Challenge { get; set; }

        public Credential(string username, string password)
        {
            Username = username;
            Password = password;
            Challenge = null;
        }

        public Credential(string username, string password, string challenge)
        {
            var rand = new Random();
            var id = rand.Next(0, 255);

            var builder = new StringBuilder();
            builder.Append(Convert.ToChar(id));
            builder.Append(password);

            for (var i = 0; i < challenge.Length; i += 2)
            {
                var hex = challenge.Substring(i, 2);
                var dec = int.Parse(hex, NumberStyles.HexNumber);
                builder.Append(Convert.ToChar(dec));
            }

            using var md5 = MD5.Create();
            var bytes = Encoding.Latin1.GetBytes(builder.ToString());
            var hash = string.Concat(md5.ComputeHash(bytes).Select(x => x.ToString("x2")));

            Username = username;
            Password = id.ToString("x2") + hash;
            Challenge = challenge;
        }
    }

    class Response<TResult>
    {
        [JsonProperty("reply_code")] public int? ReplyCode { get; set; }
        [JsonProperty("reply_msg")] public string ReplyMessage { get; set; }
        [JsonProperty("results")] public TResult Results { get; set; }
    }

    class OnlineResult
    {
        public class OnlineInfo
        {
            [JsonProperty("mac")] public string MacAddress { get; set; }
            [JsonProperty("user_ipv4")] public uint Ipv4Uint { get; set; }
            [JsonProperty("user_ipv6")] public string Ipv6Address { get; set; }

            public string GetIpv4Address()
            {
                var bytes = BitConverter.GetBytes(Ipv4Uint);
                return string.Join('.', bytes.Reverse().Select(b => Convert.ToInt32(b).ToString("D")));
            }
        }

        [JsonProperty("rows")] public OnlineInfo[] OnlineInfos { get; set; }
        [JsonProperty("total")] public int Total { get; set; }
    }

    public class Bras
    {
        private const string Host = "http://p.nju.edu.cn";
        private string ChallengeApi => Host + "/api/portal/v1/challenge";
        private string LoginApi => Host + "/api/portal/v1/login";
        private string OnlineApi => Host + "/api/selfservice/v1/online";

        private HttpClient Client { get; set; }
        private string Username { get; set; }
        private string Password { get; set; }

        public Bras(BrasConfig config)
        {
            Client = new HttpClient();
            Username = config.Username;
            Password = config.Password;
        }

        public async Task Login()
        {
            var challengeMessage = await Client.GetAsync(ChallengeApi);
            if (!challengeMessage.IsSuccessStatusCode)
            {
                throw new Exception($"Challenge responded with code {challengeMessage.StatusCode}");
            }

            var challengeContent = await challengeMessage.Content.ReadAsStringAsync();
            var challengeResponse = JsonConvert.DeserializeObject<Response<string>>(challengeContent);
            if (challengeResponse == null || challengeResponse.ReplyCode != 0)
            {
                throw new Exception("Cannot read bras challenge api response.");
            }

            var challenge = challengeResponse.Results;
            var credential = new Credential(Username, Password, challenge);
            var content = JsonConvert.SerializeObject(credential);
            var loginMessage = await Client.PostAsync(LoginApi, new StringContent(content));
            if (!loginMessage.IsSuccessStatusCode)
            {
                throw new Exception($"Login responded with code {loginMessage.StatusCode}");
            }

            var loginContent = await loginMessage.Content.ReadAsStringAsync();
            var loginResponse = JsonConvert.DeserializeObject<Response<Object>>(loginContent);
            if (loginResponse == null)
            {
                throw new Exception("Cannot read bras login api response.");
            }
            else if (loginResponse.ReplyCode != 0)
            {
                throw new Exception($"Login responded with code {loginResponse.ReplyCode}," +
                                    $" message: {loginResponse.Results}");
            }
        }

        public async Task<(IPAddress, IPAddress)> GetIpAddresses()
        {
            var onlineMessage = await Client.GetAsync(OnlineApi);
            var onlineContent = await onlineMessage.Content.ReadAsStringAsync();
            var onlineResponse = JsonConvert.DeserializeObject<Response<OnlineResult>>(onlineContent);
            if (onlineResponse == null)
            {
                throw new Exception("Cannot read bras online api response.");
            }

            var possibleIpList = onlineResponse.Results.OnlineInfos.Select(info => info.GetIpv4Address()).ToList();

            var ipv4Address = IPAddress.None;
            var ipv6Address = IPAddress.IPv6None;
            var networkInterfaceList = NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up &&
                                           networkInterface.GetIPProperties().GatewayAddresses.Count > 0)
                .ToList();

            foreach (var networkInterface in networkInterfaceList)
            {
                var informationList = networkInterface.GetIPProperties().UnicastAddresses
                    .Where(information => !IPAddress.IsLoopback(information.Address)).ToList();
                var isInterNetwork = informationList
                    .Where(information => information.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Any(information => possibleIpList.Contains(information.Address.ToString()));
                if (isInterNetwork)
                {
                    ipv4Address = informationList.FirstOrDefault(information =>
                        information.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
                    ipv6Address = informationList.FirstOrDefault(information =>
                        information.Address.AddressFamily == AddressFamily.InterNetworkV6)?.Address;
                    break;
                }
            }

            return (ipv4Address, ipv6Address);
        }
    }
}