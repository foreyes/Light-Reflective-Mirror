using LightReflectiveMirror.Endpoints;
using Mirror;
using System;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        /// <summary>
        /// Invoked when a client connects to this LRM server.
        /// </summary>
        /// <param name="clientId">The ID of the client who connected.</param>
        public void ClientConnected(int clientId)
        {
            Program.WriteLogMessage($"ClientConnected: Client {clientId} connected, requesting authentication", ConsoleColor.Cyan);
            _pendingAuthentication.Add(clientId);
            var buffer = _sendBuffers.Rent(1);
            int pos = 0;
            buffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationRequest);
            Program.transport.ServerSend(clientId, new ArraySegment<byte>(buffer, 0, pos), Channels.Reliable);
            _sendBuffers.Return(buffer);
            Program.WriteLogMessage($"ClientConnected: Authentication request sent to Client {clientId}", ConsoleColor.Cyan);
        }

        /// <summary>
        /// Handles the processing of data from a client.
        /// </summary>
        /// <param name="clientId">The client who sent the data</param>
        /// <param name="segmentData">The binary data</param>
        /// <param name="channel">The channel the client sent the data on</param>
        public void HandleMessage(int clientId, ArraySegment<byte> segmentData, int channel)
        {
            try
            {
                var data = segmentData.Array;
                int pos = segmentData.Offset;

                OpCodes opcode = (OpCodes)data.ReadByte(ref pos);

                if (_pendingAuthentication.Contains(clientId))
                {
                    if (opcode == OpCodes.AuthenticationResponse)
                    {
                        string authResponse = data.ReadString(ref pos);
                        Program.WriteLogMessage($"Authentication: Client {clientId} sent auth key: {authResponse}, Expected: {Program.conf.AuthenticationKey}", ConsoleColor.Yellow);
                        
                        if (authResponse == Program.conf.AuthenticationKey)
                        {
                            _pendingAuthentication.Remove(clientId);
                            int writePos = 0;
                            var sendBuffer = _sendBuffers.Rent(1);
                            sendBuffer.WriteByte(ref writePos, (byte)OpCodes.Authenticated);
                            Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, writePos), Channels.Reliable);
                            
                            Program.WriteLogMessage($"Authentication: Client {clientId} authenticated successfully", ConsoleColor.Green);
                            _sendBuffers.Return(sendBuffer);
                        }
                        else
                        {
                            Program.WriteLogMessage($"Authentication: Client {clientId} sent wrong auth key! Removing from LRM node.", ConsoleColor.Red);
                            Program.transport.ServerDisconnect(clientId);
                        }
                    }
                    return;
                }

                switch (opcode)
                {
                    case OpCodes.CreateRoom: // bruh
                        int maxPlayers = data.ReadInt(ref pos);
                        string serverName = data.ReadString(ref pos);
                        bool isPublic = data.ReadBool(ref pos);
                        string serverData = data.ReadString(ref pos);
                        bool canDirectConnect = data.ReadBool(ref pos);
                        string hostLocalIP = data.ReadString(ref pos);
                        bool useNatPunch = data.ReadBool(ref pos);
                        int port = data.ReadInt(ref pos);
                        int appId = data.ReadInt(ref pos);
                        string version = data.ReadString(ref pos);
                        
                        Program.WriteLogMessage($"CreateRoom request from Client: {clientId}, ServerName: {serverName}, MaxPlayers: {maxPlayers}, IsPublic: {isPublic}, CanDirectConnect: {canDirectConnect}, HostLocalIP: {hostLocalIP}, UseNatPunch: {useNatPunch}, Port: {port}, AppId: {appId}, Version: {version}", ConsoleColor.Green);
                        CreateRoom(clientId, maxPlayers, serverName, isPublic, serverData, canDirectConnect, hostLocalIP, useNatPunch, port, appId, version);
                        break;
                    case OpCodes.RequestID:
                        Program.WriteLogMessage($"RequestID from Client: {clientId}", ConsoleColor.Cyan);
                        SendClientID(clientId);
                        break;
                    case OpCodes.LeaveRoom:
                        Program.WriteLogMessage($"LeaveRoom from Client: {clientId}", ConsoleColor.Cyan);
                        LeaveRoom(clientId);
                        break;
                    case OpCodes.JoinServer:
                        string serverId = data.ReadString(ref pos);
                        bool canDirectConnectJoin = data.ReadBool(ref pos);
                        string localIP = data.ReadString(ref pos);
                        Program.WriteLogMessage($"JoinServer request from Client: {clientId}, ServerId: {serverId}, CanDirectConnect: {canDirectConnectJoin}, LocalIP: {localIP}", ConsoleColor.Green);
                        JoinRoom(clientId, serverId, canDirectConnectJoin, localIP);
                        break;
                    case OpCodes.KickPlayer:
                        int targetClientId = data.ReadInt(ref pos);
                        Program.WriteLogMessage($"KickPlayer request from Client: {clientId}, Target: {targetClientId}", ConsoleColor.Red);
                        LeaveRoom(targetClientId, clientId);
                        break;
                    case OpCodes.SendData:
                        ProcessData(clientId, data.ReadBytes(ref pos), channel, data.ReadInt(ref pos));
                        break;
                    case OpCodes.UpdateRoomData:
                        var plyRoom = _cachedClientRooms[clientId];

                        if (plyRoom == null || plyRoom.hostId != clientId)
                            return;

                        if (data.ReadBool(ref pos))
                            plyRoom.serverName = data.ReadString(ref pos);

                        if (data.ReadBool(ref pos))
                            plyRoom.serverData = data.ReadString(ref pos);

                        if (data.ReadBool(ref pos))
                            plyRoom.isPublic = data.ReadBool(ref pos);

                        if (data.ReadBool(ref pos))
                            plyRoom.maxPlayers = data.ReadInt(ref pos);

                        Endpoint.RoomsModified();
                        break;
                }
            }
            catch
            {
                // sent invalid data, boot them hehe
                Program.WriteLogMessage($"Client {clientId} sent bad data! Removing from LRM node.");
                Program.transport.ServerDisconnect(clientId);
            }
        }

        /// <summary>
        /// Invoked when a client disconnects from the relay.
        /// </summary>
        /// <param name="clientId">The ID of the client who disconnected</param>
        public void HandleDisconnect(int clientId) => LeaveRoom(clientId);
    }
}
