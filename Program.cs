using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;

namespace Bras
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Config config = null;
            await using (var stream = new FileStream("config.json", FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                config = JsonConvert.DeserializeObject<Config>(await reader.ReadToEndAsync());
                if (config == null)
                {
                    Console.WriteLine("Error: cannot parse config json.");
                    Environment.Exit(1);
                }
            }

            Console.WriteLine($"Started at {DateTime.Now}");

            Bras bras = new Bras(config.Bras);
            DnsPod pod = new DnsPod(config.DnsPod);

            await bras.Login();
            Console.WriteLine("Bras login OK");
            var (ipv4, ipv6) = await bras.GetIpAddresses();
            Console.WriteLine($" -> IPv4: {ipv4}");
            Console.WriteLine($" -> IPv6: {ipv6}");

            if (pod.Enabled)
            {
                var infoList = await pod.FetchRecordInfos();
                Console.WriteLine("DnsPod query OK");
                foreach (var (id, name, type) in infoList)
                {
                    Console.WriteLine($" -> #{id} {name} {type}");
                }

                await pod.UpdateRecordInfos(ipv4.ToString(), ipv6.ToString());
            }

            var timer = new System.Timers.Timer(config.General.Interval * 60 * 1000);
            timer.Elapsed += async (object sender, ElapsedEventArgs eventArgs) =>
            {
                Console.WriteLine($"Loop at {DateTime.Now}");
                await bras.Login();
                if (pod.Enabled)
                {
                    (ipv4, ipv6) = await bras.GetIpAddresses();
                    await pod.UpdateRecordInfos(ipv4.ToString(), ipv6.ToString());
                }
            };
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs eventArgs) =>
            {
                if (timer.Enabled) timer.Stop();
                Console.WriteLine("Exited gracefully");
                Environment.Exit(0);
            };
            timer.Start();
            Console.WriteLine("Timer started, press Ctrl+C to interrupt");
            while (true)
            {
                Thread.Sleep(1);
            }
        }
    }
}