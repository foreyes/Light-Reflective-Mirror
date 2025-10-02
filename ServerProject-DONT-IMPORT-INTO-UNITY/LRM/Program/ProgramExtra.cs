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
        /// <returns>包含总连接数、NAT连接数和Relay连接数的元组</returns>
        public (int totalConnections, int natConnections, int relayConnections) GetConnectionStats()
        {
            int totalConnections = _currentConnections.Count;
            int natConnections = NATConnections.Count;
            int relayConnections = totalConnections - natConnections;
            return (totalConnections, natConnections, relayConnections);
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
