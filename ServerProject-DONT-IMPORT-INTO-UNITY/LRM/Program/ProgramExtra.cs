using Grapevine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LightReflectiveMirror
{
    partial class Program
    {
        public List<Room> GetRooms() => _relay.rooms;
        public int GetConnections() => _currentConnections.Count;
        public TimeSpan GetUptime() => DateTime.Now - _startupTime;
        public int GetPublicRoomCount() => _relay.rooms.Where(x => x.isPublic).Count();
        
        /// <summary>
        /// 获取连接统计信息
        /// </summary>
        /// <returns>包含总连接数、Host数量、Client数量、Relay客户端数量和预计带宽使用的元组</returns>
        public (int totalConnections, int hostCount, int clientCount, int relayClientCount, double estimatedBandwidthKbps) GetConnectionStats()
        {
            int totalConnections = _currentConnections.Count;
            int hostCount = _relay.rooms.Count;
            int clientCount = 0;
            int relayClientCount = 0;
            
            // 计算客户端数量和Relay客户端数量
            foreach (var room in _relay.rooms)
            {
                clientCount += room.clients.Count;
                
                // 如果房间不支持直接连接，所有客户端都使用Relay
                if (!room.supportsDirectConnect)
                {
                    relayClientCount += room.clients.Count;
                }
                else
                {
                    // 房间支持直接连接，但只有有NAT连接的客户端才能直接连接
                    // 没有NAT连接的客户端仍然使用Relay
                    foreach (int clientId in room.clients)
                    {
                        if (!NATConnections.ContainsKey(clientId))
                        {
                            relayClientCount++;
                        }
                    }
                }
            }
            
            // 计算预计带宽使用：每个Relay Client占用350 kBit/s
            double estimatedBandwidthKbps = relayClientCount * 350.0;
            
            return (totalConnections, hostCount, clientCount, relayClientCount, estimatedBandwidthKbps);
        }

        public static void WriteLogMessage(string message, ConsoleColor color = ConsoleColor.White, bool oneLine = false)
        {
            Console.ForegroundColor = color;
            if (oneLine)
                Console.Write(message);
            else
                Console.WriteLine(message);
        }

        private static void GetPublicIP()
        {
            publicIP = "1.13.197.43";
        }

        static void WriteTitle()
        {
            string t = @"  
                           w  c(..)o   (
  _       _____   __  __    \__(-)    __)
 | |     |  __ \ |  \/  |       /\   (
 | |     | |__) || \  / |      /(_)___)
 | |     |  _  / | |\/| |      w /|
 | |____ | | \ \ | |  | |       | \
 |______||_|  \_\|_|  |_|      m  m copyright monkesoft 2021

";

            string load = $"Chimp Event Listener Initializing... OK" +
                            "\nHarambe Memorial Initializing...     OK" +
                            "\nBananas Initializing...              OK\n";

            WriteLogMessage(t, ConsoleColor.Green);
            WriteLogMessage(load, ConsoleColor.Cyan);
        }
    }
}
