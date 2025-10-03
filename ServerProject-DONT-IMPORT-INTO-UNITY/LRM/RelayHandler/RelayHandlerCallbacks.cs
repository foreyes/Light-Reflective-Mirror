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
            Program.WriteTimestampedLog($"[AUTH] Client {clientId} connected, requesting authentication");
            _pendingAuthentication.Add(clientId);
            var buffer = _sendBuffers.Rent(1);
            int pos = 0;
            buffer.WriteByte(ref pos, (byte)OpCodes.AuthenticationRequest);
            Program.transport.ServerSend(clientId, new ArraySegment<byte>(buffer, 0, pos), Channels.Reliable);
            _sendBuffers.Return(buffer);
            Program.WriteTimestampedLog($"[AUTH] Authentication request sent to Client {clientId}");
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

                byte opcodeByte = data.ReadByte(ref pos);
                
                // Check if opcode is valid (but don't log for heartbeat opcode 200)
                if (opcodeByte > 23)
                {
                    if (opcodeByte != 200) // Don't log heartbeat as invalid
                    {
                        Program.WriteTimestampedLog($"[LRM] HandleMessage: Invalid opcode {opcodeByte} from Client {clientId}, ignoring message");
                    }
                    return;
                }
                
                OpCodes opcode = (OpCodes)opcodeByte;
                
                // Only log important messages, not SendData spam
                if (opcodeByte != 200 && opcode != OpCodes.SendData)
                {
                    Program.WriteTimestampedLog($"[LRM] HandleMessage: Client {clientId}, data length: {segmentData.Count}, channel: {channel}, opcode: {opcode}");
                }

                if (_pendingAuthentication.Contains(clientId))
                {
                    if (opcode == OpCodes.AuthenticationResponse)
                    {
                        string authResponse = data.ReadString(ref pos);
                        Program.WriteTimestampedLog($"[AUTH] Client {clientId} sent auth key: {authResponse}, Expected: {Program.conf.AuthenticationKey}");
                        
                        if (authResponse == Program.conf.AuthenticationKey)
                        {
                            _pendingAuthentication.Remove(clientId);
                            int writePos = 0;
                            var sendBuffer = _sendBuffers.Rent(1);
                            sendBuffer.WriteByte(ref writePos, (byte)OpCodes.Authenticated);
                            Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, writePos), Channels.Reliable);
                            
                            Program.WriteTimestampedLog($"[AUTH] Client {clientId} authenticated successfully");
                            _sendBuffers.Return(sendBuffer);
                        }
                        else
                        {
                            Program.WriteTimestampedLog($"[AUTH] Client {clientId} sent wrong auth key! Removing from LRM node.");
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
                        
                        Program.WriteTimestampedLog($"[ROOM] CreateRoom request from Client: {clientId}, ServerName: {serverName}, MaxPlayers: {maxPlayers}, IsPublic: {isPublic}, CanDirectConnect: {canDirectConnect}, HostLocalIP: {hostLocalIP}, UseNatPunch: {useNatPunch}, Port: {port}, AppId: {appId}, Version: {version}");
                        CreateRoom(clientId, maxPlayers, serverName, isPublic, serverData, canDirectConnect, hostLocalIP, useNatPunch, port, appId, version);
                        break;
                    case OpCodes.RequestID:
                        Program.WriteTimestampedLog($"[ID] RequestID from Client: {clientId}");
                        SendClientID(clientId);
                        break;
                    case OpCodes.LeaveRoom:
                        Program.WriteTimestampedLog($"[ROOM] LeaveRoom from Client: {clientId}");
                        LeaveRoom(clientId, -1, true); // Send ServerLeft when client voluntarily leaves
                        break;
                    case OpCodes.JoinServer:
                        string serverId = data.ReadString(ref pos);
                        bool canDirectConnectJoin = data.ReadBool(ref pos);
                        string localIP = data.ReadString(ref pos);
                        Program.WriteTimestampedLog($"[JOIN] JoinServer request from Client: {clientId}, ServerId: {serverId}, CanDirectConnect: {canDirectConnectJoin}, LocalIP: {localIP}");
                        JoinRoom(clientId, serverId, canDirectConnectJoin, localIP);
                        break;
                    case OpCodes.KickPlayer:
                        int targetClientId = data.ReadInt(ref pos);
                        Program.WriteTimestampedLog($"[KICK] KickPlayer request from Client: {clientId}, Target: {targetClientId}");
                        LeaveRoom(targetClientId, clientId, true); // Send ServerLeft when kicked
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
            catch (Exception ex)
            {
                // sent invalid data, boot them hehe
                Program.WriteTimestampedLog($"[LRM] HandleMessage Error: Client {clientId} sent bad data! Exception: {ex.Message}");
                Program.WriteLogMessage($"Client {clientId} sent bad data! Removing from LRM node.");
                Program.transport.ServerDisconnect(clientId);
            }
        }

        /// <summary>
        /// Invoked when a client disconnects from the relay.
        /// </summary>
        /// <param name="clientId">The ID of the client who disconnected</param>
        public void HandleDisconnect(int clientId) => LeaveRoom(clientId, -1, true); // Send ServerLeft when disconnected
    }
}
