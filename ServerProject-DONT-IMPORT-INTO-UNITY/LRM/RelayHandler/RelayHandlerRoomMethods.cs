using LightReflectiveMirror.Endpoints;
using Mirror;
using System;
using System.Collections.Generic;
using System.Net;

namespace LightReflectiveMirror
{
    public partial class RelayHandler
    {
        /// <summary>
        /// Creates a room on the LRM node.
        /// </summary>
        /// <param name="clientId">The client requesting to create a room</param>
        /// <param name="maxPlayers">The maximum amount of players for this room</param>
        /// <param name="serverName">The name for the server</param>
        /// <param name="isPublic">Whether or not the server should show up on the server list</param>
        /// <param name="serverData">Extra data the host can include</param>
        /// <param name="useDirectConnect">Whether or not, the host is capable of doing direct connections</param>
        /// <param name="hostLocalIP">The hosts local IP</param>
        /// <param name="useNatPunch">Whether or not, the host is supporting NAT Punch</param>
        /// <param name="port">The port of the direct connect transport on the host</param>
        private void CreateRoom(int clientId, int maxPlayers, string serverName, bool isPublic, string serverData, bool useDirectConnect, string hostLocalIP, bool useNatPunch, int port,int appId, string version)
        {
            Console.WriteLine($"[ROOM] CreateRoom: Processing room creation for Client: {clientId}, ServerName: {serverName}, MaxPlayers: {maxPlayers}, IsPublic: {isPublic}, UseDirectConnect: {useDirectConnect}, HostLocalIP: {hostLocalIP}, UseNatPunch: {useNatPunch}, Port: {port}, AppId: {appId}, Version: {version}");
            
            LeaveRoom(clientId);
            Program.instance.NATConnections.TryGetValue(clientId, out IPEndPoint hostIP);
            
            Console.WriteLine($"[ROOM] CreateRoom: Client {clientId} NAT connection status - HostIP: {hostIP}, UseDirectConnect: {useDirectConnect}");

            string serverId = GetRandomServerID();
            bool supportsDirectConnect = hostIP != null && useDirectConnect;
            
            Console.WriteLine($"[ROOM] CreateRoom: Generated ServerId: {serverId}, SupportsDirectConnect: {supportsDirectConnect}");

            Room room = new()
            {
                hostId = clientId,
                maxPlayers = maxPlayers,
                serverName = serverName,
                isPublic = isPublic,
                serverData = serverData,
                appId = appId,
                version = version,
                clients = new List<int>(),
                serverId = serverId,
                hostIP = hostIP,
                hostLocalIP = hostLocalIP,
                supportsDirectConnect = supportsDirectConnect,
                port = port,
                useNATPunch = useNatPunch,
                relayInfo = new RelayAddress { address = Program.publicIP, port = Program.conf.TransportPort, endpointPort = Program.conf.EndpointPort, serverRegion = Program.conf.LoadBalancerRegion }
            };

            rooms.Add(room);
            _cachedClientRooms.Add(clientId, room);
            _cachedRooms.Add(room.serverId, room);
            
            Console.WriteLine($"[ROOM] CreateRoom: Room created successfully - ServerId: {serverId}, HostId: {clientId}, SupportsDirectConnect: {supportsDirectConnect}, UseNATPunch: {useNatPunch}, HostIP: {hostIP}, HostLocalIP: {hostLocalIP}, Port: {port}");

            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(5);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.RoomCreated);
            sendBuffer.WriteString(ref pos, room.serverId);

            Console.WriteLine($"[ROOM] CreateRoom: Sending RoomCreated response to Client: {clientId}, ServerId: {serverId}");
            Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
            _sendBuffers.Return(sendBuffer);

            Endpoint.RoomsModified();
        }

        /// <summary>
        /// Attempts to join a room for a client.
        /// </summary>
        /// <param name="clientId">The client requesting to join the room</param>
        /// <param name="serverId">The server ID of the room</param>
        /// <param name="canDirectConnect">If the client is capable of a direct connection</param>
        /// <param name="localIP">The local IP of the client joining</param>
        private void JoinRoom(int clientId, string serverId, bool canDirectConnect, string localIP)
        {
            Console.WriteLine($"[JOIN] JoinRoom: Processing join request for Client: {clientId}, ServerId: {serverId}, CanDirectConnect: {canDirectConnect}, LocalIP: {localIP}");
            
            LeaveRoom(clientId);

            if (_cachedRooms.ContainsKey(serverId))
            {
                var room = _cachedRooms[serverId];
                Console.WriteLine($"[JOIN] JoinRoom: Room found - HostId: {room.hostId}, CurrentPlayers: {room.clients.Count}/{room.maxPlayers}, SupportsDirectConnect: {room.supportsDirectConnect}, UseNATPunch: {room.useNATPunch}, HostIP: {room.hostIP}, HostLocalIP: {room.hostLocalIP}");

                if (room.clients.Count < room.maxPlayers)
                {
                    room.clients.Add(clientId);
                    _cachedClientRooms.Add(clientId, room);
                    Console.WriteLine($"[JOIN] JoinRoom: Client {clientId} added to room {serverId}, new player count: {room.clients.Count}");

                    int sendJoinPos = 0;
                    byte[] sendJoinBuffer = _sendBuffers.Rent(500);

                    bool hasNATConnection = Program.instance.NATConnections.ContainsKey(clientId);
                    Console.WriteLine($"[JOIN] JoinRoom: Client {clientId} NAT connection status - HasNATConnection: {hasNATConnection}, CanDirectConnect: {canDirectConnect}, RoomSupportsDirectConnect: {room.supportsDirectConnect}");

                    if (canDirectConnect && hasNATConnection && room.supportsDirectConnect)
                    {
                        Console.WriteLine($"[JOIN] JoinRoom: Attempting direct connection for Client: {clientId}");
                        sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.DirectConnectIP);

                        string targetIP;
                        int targetPort;
                        if (Program.instance.NATConnections[clientId].Address.Equals(room.hostIP.Address))
                        {
                            targetIP = room.hostLocalIP == localIP ? "127.0.0.1" : room.hostLocalIP;
                            Console.WriteLine($"[JOIN] JoinRoom: Same network detected, using local IP: {targetIP}");
                        }
                        else
                        {
                            targetIP = room.hostIP.Address.ToString();
                            Console.WriteLine($"[JOIN] JoinRoom: Different networks, using host IP: {targetIP}");
                        }

                        targetPort = room.useNATPunch ? room.hostIP.Port : room.port;
                        sendJoinBuffer.WriteString(ref sendJoinPos, targetIP);
                        sendJoinBuffer.WriteInt(ref sendJoinPos, targetPort);
                        sendJoinBuffer.WriteBool(ref sendJoinPos, room.useNATPunch);

                        Console.WriteLine($"[JOIN] JoinRoom: Sending DirectConnectIP to Client: {clientId}, IP: {targetIP}, Port: {targetPort}, UseNATPunch: {room.useNATPunch}");
                        Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos), Channels.Reliable);

                        if (room.useNATPunch)
                        {
                            sendJoinPos = 0;
                            sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.DirectConnectIP);

                            string clientNATIP = Program.instance.NATConnections[clientId].Address.ToString();
                            int clientNATPort = Program.instance.NATConnections[clientId].Port;
                            sendJoinBuffer.WriteString(ref sendJoinPos, clientNATIP);
                            sendJoinBuffer.WriteInt(ref sendJoinPos, clientNATPort);
                            sendJoinBuffer.WriteBool(ref sendJoinPos, true);

                            Console.WriteLine($"[JOIN] JoinRoom: Sending NAT punch info to Host: {room.hostId}, ClientNATIP: {clientNATIP}, ClientNATPort: {clientNATPort}");
                            Program.transport.ServerSend(room.hostId, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos), Channels.Reliable);
                        }

                        _sendBuffers.Return(sendJoinBuffer);
                        Endpoint.RoomsModified();
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"[JOIN] JoinRoom: Using relay mode for Client: {clientId} - CanDirectConnect: {canDirectConnect}, HasNATConnection: {hasNATConnection}, RoomSupportsDirectConnect: {room.supportsDirectConnect}");
                        sendJoinBuffer.WriteByte(ref sendJoinPos, (byte)OpCodes.ServerJoined);
                        sendJoinBuffer.WriteInt(ref sendJoinPos, clientId);

                        Console.WriteLine($"[JOIN] JoinRoom: Sending ServerJoined to Client: {clientId} and Host: {room.hostId}");
                        Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos), Channels.Reliable);
                        Program.transport.ServerSend(room.hostId, new ArraySegment<byte>(sendJoinBuffer, 0, sendJoinPos), Channels.Reliable);
                        _sendBuffers.Return(sendJoinBuffer);

                        Endpoint.RoomsModified();
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"[JOIN] JoinRoom: Room {serverId} is full - Current: {room.clients.Count}, Max: {room.maxPlayers}");
                }
            }
            else
            {
                Console.WriteLine($"[JOIN] JoinRoom: Room {serverId} not found! Available rooms: {string.Join(", ", _cachedRooms.Keys)}");
            }

            // If it got to here, then the server was not found, or full. Tell the client.
            Console.WriteLine($"[JOIN] JoinRoom: Sending ServerLeft to Client: {clientId} - Room not found or full");
            int pos = 0;
            byte[] sendBuffer = _sendBuffers.Rent(1);

            sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

            Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
            _sendBuffers.Return(sendBuffer);
        }

        /// <summary>
        /// Makes the client leave their room.
        /// </summary>
        /// <param name="clientId">The client of which to remove from their room</param>
        /// <param name="requiredHostId">The ID of the client who kicked the client. -1 if the client left on their own terms</param>
        private void LeaveRoom(int clientId, int requiredHostId = -1)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                // if host left
                if (rooms[i].hostId == clientId)
                {
                    int pos = 0;
                    byte[] sendBuffer = _sendBuffers.Rent(1);
                    sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

                    for (int x = 0; x < rooms[i].clients.Count; x++)
                    {
                        Program.transport.ServerSend(rooms[i].clients[x], new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
                        _cachedClientRooms.Remove(rooms[i].clients[x]);
                    }

                    _sendBuffers.Return(sendBuffer);
                    rooms[i].clients.Clear();
                    _cachedRooms.Remove(rooms[i].serverId);
                    rooms.RemoveAt(i);
                    _cachedClientRooms.Remove(clientId);
                    Endpoint.RoomsModified();
                    return;
                }
                else
                {
                    // if the person that tried to kick wasnt host and it wasnt the client leaving on their own
                    if (requiredHostId != -1 && rooms[i].hostId != requiredHostId)
                        continue;

                    if (rooms[i].clients.RemoveAll(x => x == clientId) > 0)
                    {
                        int pos = 0;
                        byte[] sendBuffer = _sendBuffers.Rent(5);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.PlayerDisconnected);
                        sendBuffer.WriteInt(ref pos, clientId);

                        Program.transport.ServerSend(rooms[i].hostId, new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
                        _sendBuffers.Return(sendBuffer);

                        // temporary solution to kicking bug
                        // this tells the local player that got kicked that he, well, got kicked.
                        pos = 0;
                        sendBuffer = _sendBuffers.Rent(1);

                        sendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerLeft);

                        Program.transport.ServerSend(clientId, new ArraySegment<byte>(sendBuffer, 0, pos), Channels.Reliable);
                        _sendBuffers.Return(sendBuffer);

                        //end temporary solution

                        Endpoint.RoomsModified();
                        _cachedClientRooms.Remove(clientId);
                    }
                }
            }
        }
    }
}
