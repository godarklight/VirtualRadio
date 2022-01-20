using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.IO;
using VirtualRadio.Common;

namespace VirtualRadio.Server
{
    public class RadioServer
    {
        TcpListener listener;
        List<Client> clients = new List<Client>();
        public const int chunksSec = 50;
        Client addClient = null;

        //Reset chunk ID daily
        const int resetChunk = 24 * 60 * 60 * chunksSec;

        public RadioServer()
        {
            listener = new TcpListener(new IPEndPoint(IPAddress.IPv6Any, 1235));
            listener.Server.DualMode = true;
            listener.Start();
            listener.BeginAcceptTcpClient(ConnectCallback, listener);
        }

        public void ConnectCallback(IAsyncResult ar)
        {
            TcpClient tcpClient = listener.EndAcceptTcpClient(ar);
            Client client = new Client(tcpClient);
            addClient = client;
            listener.BeginAcceptTcpClient(ConnectCallback, listener);
        }

        public void Run()
        {
            //Time sync
            int currentChunk = 0;
            long startTime = DateTime.UtcNow.Ticks;

            //Send buffer
            Complex[] sendSamples = new Complex[Constants.SERVER_BANDWIDTH / chunksSec];
            byte[] sendBuffer = new byte[2 * sendSamples.Length];
            byte[] compressBuffer = new byte[1 * 1024 * 1024];

            //Server main loop
            bool running = true;

            Random rand = new Random();
            while (running)
            {
                //Clear send buffer and
                Array.Clear(sendSamples, 0, sendSamples.Length);
                
                //Add noise
                for (int i = 0; i < sendSamples.Length; i++)
                {
                    sendSamples[i] = new Complex(0.01 * rand.NextDouble(), 0.01 * rand.NextDouble());
                }

                currentChunk++;

                //Load clients data into samples
                foreach (Client c in clients)
                {
                    c.WriteSamples(sendSamples);
                }

                //Generate samples
                for (int i = 0; i < sendSamples.Length; i++)
                {
                    FormatConvert.IQToByteArray8(sendSamples[i], sendBuffer, i * 2);
                }

                int compressLength = Compression.Compress(sendBuffer, 0, sendBuffer.Length, compressBuffer);

                //TCP send and disconnect
                Client removeClient = null;
                foreach (Client c in clients)
                {
                    if (!c.connected)
                    {
                        removeClient = c;
                        continue;
                    }
                    if (c.sendIQ)
                    {
                        c.QueueBytes(compressBuffer, compressLength);
                    }
                }
                if (removeClient != null)
                {
                    clients.Remove(removeClient);
                }
                if (addClient != null)
                {
                    clients.Add(addClient);
                    addClient = null;
                }

                //Reset chunk IDs daily
                if (currentChunk > resetChunk)
                {
                    currentChunk = 0;
                    startTime = DateTime.UtcNow.Ticks;
                }

                //Sleep until it's time to generate a chunk
                while (running)
                {
                    long timeDelta = DateTime.UtcNow.Ticks - startTime;
                    long targetChunk = (chunksSec * timeDelta) / TimeSpan.TicksPerSecond;
                    if (targetChunk > currentChunk)
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }
    }
}