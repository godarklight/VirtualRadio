using System;
using System.Net;
using System.Net.Sockets;

namespace VirtualRadio
{
    public class Client
    {
        public TcpClient tcpClient;
        public bool connected;

        public Client(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            connected = true;
        }

        public void QueueBytes(byte[] input)
        {

        }
    }
}