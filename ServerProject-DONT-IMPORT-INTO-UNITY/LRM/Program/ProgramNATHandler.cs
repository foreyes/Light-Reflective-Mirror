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

            WriteLogMessage($"NAT Punchthrough server listening on port {conf.NATPunchtroughPort}", ConsoleColor.Cyan);

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
                        WriteLogMessage($"NAT Punchthrough: Received connection attempt from {remoteEndpoint}, ConnectionID: {connectionID}", ConsoleColor.Yellow);

                        if (_pendingNATPunches.TryGetBySecond(connectionID, out pos))
                        {
                            NATConnections.Add(pos, new IPEndPoint(remoteEndpoint.Address, remoteEndpoint.Port));
                            _pendingNATPunches.Remove(pos);
                            WriteLogMessage($"Client Successfully Established Puncher Connection. Client: {pos}, Endpoint: {remoteEndpoint}", ConsoleColor.Green);
                            WriteLogMessage($"NAT Connections count: {NATConnections.Count}", ConsoleColor.Cyan);
                        }
                        else
                        {
                            WriteLogMessage($"NAT Punchthrough: Unknown connection ID {connectionID} from {remoteEndpoint}", ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        WriteLogMessage($"NAT Punchthrough: Received non-connection packet from {remoteEndpoint}", ConsoleColor.Gray);
                    }

                    _punchServer.Send(serverResponse, 1, remoteEndpoint);
                }
                catch (Exception ex)
                {
                    WriteLogMessage($"NAT Punchthrough error: {ex.Message}", ConsoleColor.Red);
                }
            }
        }
    }
}
