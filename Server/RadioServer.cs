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

            //sweep freq
            double sweepFreq = 0;
            double sweepAngle = 0;
            double toneAngle = 0;

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

                //Add sweep
                /*
                for (int i = 0; i < sendSamples.Length; i++)
                {
                    double toneAmp = (Math.Sin(toneAngle) + 1.0) / 2.0;
                    sendSamples[i] += new Complex(0.1 * toneAmp * Math.Cos(sweepAngle), 0.1 * toneAmp * Math.Sin(sweepAngle));
                    sweepFreq += 1.0 / (double)(Constants.SERVER_BANDWIDTH * 5);
                    sweepAngle += Math.Tau * sweepFreq;
                    sweepAngle = sweepAngle % Math.Tau;
                    if (sweepFreq > 250000)
                    {
                        sweepFreq = 0;
                    }
                    toneAngle += (Math.Tau * 50 / 48000);
                    toneAngle = toneAngle % Math.Tau;
                }
                */
                currentChunk++;

                //Load clients data into samples
                foreach (Client c in clients)
                {
                    c.WriteSamples(sendSamples);
                }

                //Generate samples
                for (int i = 0; i < sendSamples.Length; i++)
                {
                    Complex sendSample = sendSamples[i];
                    byte iByte = (byte)(((sendSample.Real + 1.0) / 2.0) * 256);
                    byte qByte = (byte)(((sendSample.Imaginary + 1.0) / 2.0) * 256);
                    sendBuffer[i * 2] = iByte;
                    sendBuffer[i * 2 + 1] = qByte;
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