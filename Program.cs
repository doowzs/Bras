using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Bras
{
    class Program
    {
        public static (IPAddress, IPAddress) GetBrasIpAddress()
        {
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
                    .Any(information => information.Address.ToString().StartsWith("172."));
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
        
        static void Main(string[] args)
        {
            Console.WriteLine(GetBrasIpAddress());
        }
    }
}