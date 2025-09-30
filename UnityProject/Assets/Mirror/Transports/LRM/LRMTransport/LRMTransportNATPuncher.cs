using Mirror;
using System;
using System.Collections;
using System.Net;
using UnityEngine;

namespace LightReflectiveMirror
{
    public partial class LightReflectiveMirrorTransport : Transport
    {
        IEnumerator NATPunch(IPEndPoint remoteAddress)
        {
            for (int i = 0; i < 10; i++)
            {
                _NATPuncher.Send(_punchData, 1, remoteAddress);
                yield return new WaitForSeconds(0.25f);
            }
        }

        void RecvData(IAsyncResult result)
        {
            IPEndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
            var data = _NATPuncher.EndReceive(result, ref newClientEP);
            _NATPuncher.BeginReceive(new AsyncCallback(RecvData), _NATPuncher);

            Debug.Log($"[LRM] NAT RecvData: from {newClientEP}, data length: {data.Length}, isServer: {_isServer}, isClient: {_isClient}");

            if (!newClientEP.Address.Equals(_relayPuncherIP.Address))
            {
                Debug.Log($"[LRM] NAT RecvData: Direct connection data from {newClientEP} (not relay server)");
                
                if (_isServer)
                {
                    if (_serverProxies.TryGetByFirst(newClientEP, out SocketProxy foundProxy))
                    {
                        Debug.Log($"[LRM] NAT RecvData: Found existing server proxy for {newClientEP}");
                        if (data.Length > 2)
                            foundProxy.RelayData(data, data.Length);
                    }
                    else
                    {
                        Debug.Log($"[LRM] NAT RecvData: Creating new server proxy for {newClientEP}");
                        _serverProxies.Add(newClientEP, new SocketProxy(_NATIP.Port + 1, newClientEP));
                        _serverProxies.GetByFirst(newClientEP).dataReceived += ServerProcessProxyData;
                    }
                }

                if (_isClient)
                {
                    if (_clientProxy == null)
                    {
                        Debug.Log($"[LRM] NAT RecvData: Creating client proxy for direct connection");
                        _clientProxy = new SocketProxy(_NATIP.Port - 1);
                        _clientProxy.dataReceived += ClientProcessProxyData;
                    }
                    else
                    {
                        Debug.Log($"[LRM] NAT RecvData: Relaying data through existing client proxy");
                        _clientProxy.ClientRelayData(data, data.Length);
                    }
                }
            }
            else
            {
                Debug.Log($"[LRM] NAT RecvData: Data from relay server {newClientEP}, ignoring");
            }
        }

        void ServerProcessProxyData(IPEndPoint remoteEndpoint, byte[] data)
        {
            _NATPuncher.Send(data, data.Length, remoteEndpoint);
        }

        void ClientProcessProxyData(IPEndPoint _, byte[] data)
        {
            _NATPuncher.Send(data, data.Length, _directConnectEndpoint);
        }
    }
}