using System;
using System.Net;

namespace LightReflectiveMirror
{
    partial class Program
    {
        void RunNATPunchLoop()
        {
            WriteLogMessage("OK\n", ConsoleColor.Green);
            IPEndPoint remoteEndpoint = new(IPAddress.Any, conf.NATPunchtroughPort);

            // Stock Data server sends to everyone:
            var serverResponse = new byte[1] { 1 };

            byte[] readData;
            bool isConnectionEstablished;
            int pos;
            string connectionID;

            Console.WriteLine($"[NAT] NAT Punchthrough server listening on port {conf.NATPunchtroughPort}");

            while (true)
            {
                readData = _punchServer.Receive(ref remoteEndpoint);
                pos = 0;
                try
                {
                    isConnectionEstablished = readData.ReadBool(ref pos);

                    if (isConnectionEstablished)
                    {
                        connectionID = readData.ReadString(ref pos);
                        Console.WriteLine($"[NAT] NAT Punchthrough: Received connection attempt from {remoteEndpoint}, ConnectionID: {connectionID}");

                        if (_pendingNATPunches.TryGetBySecond(connectionID, out pos))
                        {
                            NATConnections.Add(pos, new IPEndPoint(remoteEndpoint.Address, remoteEndpoint.Port));
                            _pendingNATPunches.Remove(pos);
                            Console.WriteLine($"[NAT] Client Successfully Established Puncher Connection. Client: {pos}, Endpoint: {remoteEndpoint}");
                            Console.WriteLine($"[NAT] NAT Connections count: {NATConnections.Count}");
                        }
                        else
                        {
                            Console.WriteLine($"[NAT] NAT Punchthrough: Unknown connection ID {connectionID} from {remoteEndpoint}");
                            // Console.WriteLine($"[NAT] Pending NAT punches: {string.Join(", ", _pendingNATPunches.GetAllValues())}");
                        }
                    }
                    else
                    {
                        // Only log non-heartbeat packets (not just 0 or 1 byte)
                        if (readData.Length > 1)
                        {
                            Console.WriteLine($"[NAT] NAT Punchthrough: Received non-connection packet from {remoteEndpoint}, data length: {readData.Length}");
                        }
                    }

                    _punchServer.Send(serverResponse, 1, remoteEndpoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NAT] NAT Punchthrough error: {ex.Message}");
                }
            }
        }
    }
}
