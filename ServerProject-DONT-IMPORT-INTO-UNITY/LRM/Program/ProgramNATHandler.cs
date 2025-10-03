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

            Program.WriteTimestampedLog($"[NAT] NAT Punchthrough server listening on port {conf.NATPunchtroughPort}");

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
                        Program.WriteTimestampedLog($"[NAT] NAT Punchthrough: Received connection attempt from {remoteEndpoint}, ConnectionID: {connectionID}");

                        if (_pendingNATPunches.TryGetBySecond(connectionID, out pos))
                        {
                            NATConnections.Add(pos, new IPEndPoint(remoteEndpoint.Address, remoteEndpoint.Port));
                            _pendingNATPunches.Remove(pos);
                            Program.WriteTimestampedLog($"[NAT] Client Successfully Established Puncher Connection. Client: {pos}, Endpoint: {remoteEndpoint}");
                            Program.WriteTimestampedLog($"[NAT] NAT Connections count: {NATConnections.Count}");
                        }
                        else
                        {
                            Program.WriteTimestampedLog($"[NAT] NAT Punchthrough: Unknown connection ID {connectionID} from {remoteEndpoint}");
                            // Console.WriteLine($"[NAT] Pending NAT punches: {string.Join(", ", _pendingNATPunches.GetAllValues())}");
                        }
                    }
                    else
                    {
                        // Only log non-heartbeat packets (not just 0 or 1 byte)
                        if (readData.Length > 1)
                        {
                            Program.WriteTimestampedLog($"[NAT] NAT Punchthrough: Received non-connection packet from {remoteEndpoint}, data length: {readData.Length}");
                        }
                    }

                    _punchServer.Send(serverResponse, 1, remoteEndpoint);
                }
                catch (Exception ex)
                {
                    Program.WriteTimestampedLog($"[NAT] NAT Punchthrough error: {ex.Message}");
                }
            }
        }
    }
}
