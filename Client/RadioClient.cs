using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;

using VirtualRadio.Common;

namespace VirtualRadio.Client
{
    class RadioClient
    {

        public bool running = true;
        private byte[] u8 = new byte[8];
        AudioChunker chunker = new AudioChunker();
        //S16 has two bytes per sample
        private byte[] audioReceiveBuffer = new byte[Constants.CHUNK_SIZE * 2];
        private int audioReceivePos = 0;
        private byte[] compressBuffer = new byte[1 * 1024 * 1024];
        private byte[] sendBuffer = new byte[1 * 1024 * 1024];
        private byte[] receiveBuffer = new byte[1 * 1024 * 1024];
        private int receiveLeft = 8;
        private int receiveSize = 8;
        private bool receiveHeader = true;
        private MessageType receiveType = MessageType.HEARTBEAT;
        public double sendVfo = -1;
        public RadioMode? sendMode = null;
        List<TcpClient> rtlClients = new List<TcpClient>();
        TcpClient serverConnection;
        TcpListener rtlServer;
        TcpListener audioServer;
        TcpClient audioConnection;
        public RadioClient(string radioServerAddress, int audioPort)
        {
            try
            {
                rtlServer = new TcpListener(IPAddress.IPv6Any, 1234);
                rtlServer.Server.DualMode = true;
                rtlServer.Start();
                rtlServer.BeginAcceptTcpClient(RtlConnect, null);
            }
            catch
            {
                Console.WriteLine("Disabling IQ server - already running");
                rtlServer = null;
            }
            try
            {
                audioServer = new TcpListener(IPAddress.IPv6Any, audioPort);
                audioServer.Server.DualMode = true;
                audioServer.Start();
                audioServer.BeginAcceptTcpClient(AudioConnect, null);
            }
            catch
            {
                Console.WriteLine("Disabling Audio Input - already running");
                audioServer = null;
            }

            int port = 1235;
            int splitIndex = radioServerAddress.IndexOf(":", StringComparison.InvariantCulture);
            if (splitIndex > 0)
            {
                string portString = radioServerAddress.Substring(splitIndex + 1);
                port = Int32.Parse(portString);
                radioServerAddress = radioServerAddress.Substring(0, splitIndex);
            }
            IPHostEntry he = Dns.GetHostEntry(radioServerAddress);
            if (he.AddressList.Length == 0)
            {
                Console.WriteLine("No addresses listed in DNS, cannot connect");
                return;
            }
            //Preference IPv6
            try
            {
                foreach (IPAddress addr in he.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        TcpClient temp = new TcpClient(AddressFamily.InterNetworkV6);
                        temp.BeginConnect(addr, port, FinishConnect, temp);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting IPv6: {e.Message}");
            }
            Thread.Sleep(200);
            try
            {
                foreach (IPAddress addr in he.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        TcpClient temp = new TcpClient(AddressFamily.InterNetwork);
                        temp.BeginConnect(addr, port, FinishConnect, temp);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting IPv4: {e.Message}");
            }
            int waitSeconds = 5;
            while (serverConnection == null)
            {
                waitSeconds--;
                Thread.Sleep(1000);
                if (waitSeconds == 0)
                {
                    running = false;
                    break;
                }
            }
        }

        public void FinishConnect(IAsyncResult ar)
        {
            TcpClient temp = (TcpClient)ar.AsyncState;
            try
            {
                temp.EndConnect(ar);
                if (serverConnection == null)
                {
                    Console.WriteLine($"Connected to {temp.Client.RemoteEndPoint}");
                    serverConnection = temp;
                }
                else
                {
                    temp.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting, {e.Message}");
            }
        }

        public void RtlConnect(IAsyncResult ar)
        {
            TcpClient newClient = rtlServer.EndAcceptTcpClient(ar);
            Console.WriteLine($"Rtl_tcp connection: {newClient.Client.RemoteEndPoint}");
            rtlClients.Add(newClient);
            rtlServer.BeginAcceptTcpClient(RtlConnect, null);
        }

        public void AudioConnect(IAsyncResult ar)
        {
            TcpClient newClient = audioServer.EndAcceptTcpClient(ar);
            if (audioConnection != null)
            {
                try
                {
                    audioConnection.Close();
                }
                catch
                {
                    //Don't care
                }
                Console.WriteLine("Disconnected audio input connection");
            }
            Console.WriteLine($"New audio connection: {newClient.Client.RemoteEndPoint}");
            audioConnection = newClient;
            audioServer.BeginAcceptTcpClient(AudioConnect, null);
        }

        public void Run()
        {
            if (running && rtlServer != null)
            {
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.ENABLE_IQ)).CopyTo(sendBuffer, 0);
                Array.Clear(sendBuffer, 4, 4);
                serverConnection.GetStream().Write(sendBuffer, 0, 8);
            }
            while (running)
            {
                bool sleep = true;
                //VFO setting
                if (sendVfo != -1)
                {
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.SET_VFO)).CopyTo(sendBuffer, 0);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder(8)).CopyTo(sendBuffer, 4);
                    BitConverter.GetBytes(sendVfo).CopyTo(u8, 0);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(u8);
                    }
                    u8.CopyTo(sendBuffer, 8);
                    serverConnection.GetStream().Write(sendBuffer, 0, 16);
                    sendVfo = -1;
                }
                if (sendMode != null)
                {
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.SET_MODE)).CopyTo(sendBuffer, 0);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder(4)).CopyTo(sendBuffer, 4);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)sendMode)).CopyTo(sendBuffer, 8);
                    serverConnection.GetStream().Write(sendBuffer, 0, 12);
                    sendMode = null;
                }
                if (serverConnection.Available > 0)
                {
                    int readBytes = serverConnection.GetStream().Read(receiveBuffer, receiveSize - receiveLeft, receiveLeft);
                    receiveLeft -= readBytes;
                    if (readBytes == 0)
                    {
                        Console.WriteLine("Server closed connection");
                        Stop();
                        return;
                    }
                    else
                    {
                        sleep = false;
                        if (receiveLeft == 0)
                        {
                            if (receiveHeader)
                            {
                                receiveType = (MessageType)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(receiveBuffer, 0));
                                receiveSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(receiveBuffer, 4));
                                receiveLeft = receiveSize;
                                receiveHeader = false;
                                if (receiveLeft == 0)
                                {
                                    ProcessMessage();
                                    receiveSize = 8;
                                    receiveLeft = 8;
                                    receiveHeader = true;
                                }
                            }
                            else
                            {
                                ProcessMessage();
                                receiveSize = 8;
                                receiveLeft = 8;
                                receiveHeader = true;
                            }
                        }
                    }
                }
                if (audioConnection != null && !chunker.isReading)
                {
                    try
                    {
                        if (audioConnection.Available > 0)
                        {
                            int readBytes = audioConnection.GetStream().Read(audioReceiveBuffer, audioReceivePos, audioReceiveBuffer.Length - audioReceivePos);
                            if (readBytes == 0)
                            {
                                Console.WriteLine($"Disconnected audio connection");
                                audioConnection.Close();
                                audioConnection = null;
                            }
                            else
                            {
                                audioReceivePos += readBytes;
                                if (audioReceivePos == audioReceiveBuffer.Length)
                                {
                                    chunker.ReceiveBytes(audioReceiveBuffer);
                                    audioReceivePos = 0;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            Console.WriteLine($"Closing audio connection {e.Message}");
                            audioConnection.Close();
                            audioConnection = null;
                        }
                        catch
                        {
                        }
                    }
                }
                //Build up a buffer of samples and transmit them.
                if (chunker.GetSamples(out byte[] sendChunk))
                {
                    sleep = false;
                    int compressLength = Compression.Compress(sendChunk, 0, sendChunk.Length, compressBuffer);
                    //We must return the buffer back or the program will hang
                    chunker.ReturnFreeBuffer(sendChunk);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.DATA)).CopyTo(sendBuffer, 0);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)compressLength)).CopyTo(sendBuffer, 4);
                    serverConnection.GetStream().Write(sendBuffer, 0, 8);
                    serverConnection.GetStream().Write(compressBuffer, 0, compressLength);
                }

                if (sleep)
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void Stop()
        {
            chunker.Stop();
            running = false;
        }

        private void ProcessMessage()
        {
            switch (receiveType)
            {
                case (MessageType.HEARTBEAT):
                    //Do nothing
                    break;
                case (MessageType.DATA):
                    int decompressSize = Compression.Decompress(receiveBuffer, 0, receiveSize, compressBuffer);
                    TcpClient removeClient = null;
                    try
                    {
                        foreach (TcpClient c in rtlClients)
                        {
                            removeClient = c;
                            c.GetStream().Write(compressBuffer, 0, decompressSize);
                        }
                        removeClient = null;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error sending data: {e.Message}");
                    }
                    if (removeClient != null)
                    {
                        rtlClients.Remove(removeClient);
                    }
                    break;
            }
        }
    }
}