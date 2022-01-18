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
        public RadioMode mode = RadioMode.AM;
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
        List<TcpClient> rtlClients = new List<TcpClient>();
        TcpClient serverConnection;
        TcpListener rtlServer;
        TcpListener audioServer;
        TcpClient audioConnection;
        public RadioClient(string radioServerAddress)
        {
            rtlServer = new TcpListener(IPAddress.IPv6Any, 1234);
            rtlServer.Server.DualMode = true;
            rtlServer.Start();
            rtlServer.BeginAcceptTcpClient(RtlConnect, null);
            audioServer = new TcpListener(IPAddress.IPv6Any, 1236);
            audioServer.Server.DualMode = true;
            audioServer.Start();
            audioServer.BeginAcceptTcpClient(AudioConnect, null);

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
            TcpClient newClient = rtlServer.EndAcceptTcpClient(ar);
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
            while (running)
            {
                bool sleep = true;
                //VFO setting
                if (sendVfo < 0 && sendVfo != -1)
                {
                    Console.WriteLine("VFO frequency must be above 0");
                }
                if (sendVfo > 250000)
                {
                    Console.WriteLine("VFO frequency must be below 250000");
                }
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