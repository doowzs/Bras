using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Bras
{
    class BaseResponse
    {
        public class Status
        {
            [JsonProperty("code")] public int Code { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
        }

        [JsonProperty("status")] public Status status { get; set; }
    }

    public class RecordInfo
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("line_id")] public string LineId { get; set; }
        [JsonProperty("value")] public string Value { get; set; }

        public string ToParamString()
        {
            return $"record_id={Id}&record_line_id={LineId}&value={Value}";
        }
    }

    class RecordListResponse : BaseResponse
    {
        [JsonProperty("records")] public RecordInfo[] Records { get; set; }
    }

    public class DnsPod
    {
        private const string Host = "https://dnsapi.cn";
        private string RecordListApi => Host + "/Record.List";
        private string RecordDdnsApi => Host + "/Record.Ddns";

        private HttpClient Client { get; set; }
        private string Token { get; set; }
        private string Domain { get; set; }
        private string SubDomain { get; set; }
        private List<RecordInfo> RecordInfoList { get; set; }

        public DnsPod(DnsPodConfig config)
        {
            Client = new HttpClient();
            Token = config.Id + "," + config.Token;
            Domain = config.Domain;
            SubDomain = config.SubDomain;
            RecordInfoList = new List<RecordInfo>();
        }

        private async Task<TResponse> PostApiRequestAsync<TResponse>(string api, string extraParams)
            where TResponse : BaseResponse
        {
            var paramList = new List<string>
            {
                extraParams,
                $"login_token={Token}", $"domain={Domain}", $"sub_domain={SubDomain}",
                "lang=cn", "format=json"
            };
            var paramString = string.Join('&', paramList.Where(s => !string.IsNullOrEmpty(s)));
            var content = new StringContent(paramString, Encoding.UTF8, "application/x-www-form-urlencoded");
            var message = await Client.PostAsync(api, content);
            if (!message.IsSuccessStatusCode)
            {
                throw new Exception($"ApiRequest responded with code {message.StatusCode}.");
            }

            var response = JsonConvert.DeserializeObject<TResponse>(await message.Content.ReadAsStringAsync());
            if (response == null)
            {
                throw new Exception("Cannot read dns pod api response.");
            }
            else if (response.status.Code != 1)
            {
                Console.WriteLine($"{api.Substring(Host.Length + 1)} responded with code {response.status.Code}");
                Console.WriteLine($"Message: {await message.Content.ReadAsStringAsync()}");
            }

            return response;
        }

        public async Task<List<(string, string, string)>> FetchRecordInfos()
        {
            var response = await PostApiRequestAsync<RecordListResponse>(RecordListApi, string.Empty);
            if (response.Records == null)
            {
                throw new Exception("No records found in record list response.");
            }

            RecordInfoList = response.Records
                .Where(info => info.Name == SubDomain &&
                               (info.Type == "A" || info.Type == "AAAA"))
                .ToList();
            return RecordInfoList.Select(info => (info.Id, info.Name, info.Type)).ToList();
        }

        public async Task UpdateRecordInfos(string ipv4Address, string ipv6Address)
        {
            if (RecordInfoList.Count == 0) return;

            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(ipv4Address, "A"),
                new KeyValuePair<string, string>(ipv6Address, "AAAA")
            };
            foreach (var (addr, type) in pairs)
            {
                if (addr == null) continue;

                var info = RecordInfoList.FirstOrDefault(ri => ri.Type == type);
                if (info == null) continue;

                if (addr == info.Value)
                {
                }
                else
                {
                    Console.WriteLine($" -> Updating record #{info.Id} (type {type}) to {addr}");
                    info.Value = addr;
                    await PostApiRequestAsync<BaseResponse>(RecordDdnsApi, info.ToParamString());
                }
            }
        }
    }
}