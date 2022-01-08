using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.IO;
using VirtualRadio.Common;

namespace VirtualRadio
{
    public class TcpServer
    {
        TcpListener listener;
        List<Client> clients = new List<Client>();
        const int samplesSec = 250000;
        const int chunksSec = 50;

        //Reset chunk ID daily
        const int resetChunk = 24 * 60 * 60 * chunksSec;

        public TcpServer()
        {
            listener = new TcpListener(new IPEndPoint(IPAddress.IPv6Any, 1234));
            listener.Server.DualMode = true;
            listener.Start();
            listener.BeginAcceptTcpClient(ConnectCallback, listener);
        }

        public void ConnectCallback(IAsyncResult ar)
        {
            TcpClient tcpClient = listener.EndAcceptTcpClient(ar);
            Client client = new Client(tcpClient);
            clients.Add(client);
        }

        public void Run()
        {
            //Time sync
            int currentChunk = 0;
            long startTime = DateTime.UtcNow.Ticks;

            //Send buffer
            Complex[] sendSamples = new Complex[samplesSec / chunksSec];
            byte[] sendBuffer = new byte[2 * samplesSec / chunksSec];

            //Server main loop
            bool running = true;
            while (running)
            {
                //Clear send buffer and
                Array.Clear(sendBuffer, 0, sendBuffer.Length);
                currentChunk++;

                //Generate samples
                for (int i = 0; i < sendSamples.Length; i++)
                {
                    Complex sendSample = sendSamples[i];
                    byte iByte = (byte)(((sendSample.Real + 1.0) / 2.0) * byte.MaxValue);
                    byte qByte = (byte)(((sendSample.Imaginary + 1.0) / 2.0) * byte.MaxValue);
                    sendBuffer[i * 2] = iByte;
                    sendBuffer[i * 2 + 1] = qByte;
                }

                //TCP send and disconnect
                Client removeClient = null;
                foreach (Client c in clients)
                {
                    if (!c.connected)
                    {
                        removeClient = c;
                        continue;
                    }
                    c.QueueBytes(sendBuffer);
                }
                if (removeClient != null)
                {
                    clients.Remove(removeClient);
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

        private static void WriteComplex(Complex[] input, string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                for (int i = 0; i < input.Length; i++)
                {
                    sw.WriteLine($"{input[i].Real} {input[i].Imaginary}");
                }
            }
        }

        private static double[] LoadWav(string filename)
        {
            IFilter wavFilter = new WindowedSinc(9000, 2048, 48000, false);
            byte[] wavRaw = File.ReadAllBytes(filename);
            double[] wavSamples = new double[wavRaw.Length / 2];
            for (int i = 0; i < wavSamples.Length; i++)
            {
                short wavData = (short)(wavRaw[(i * 2)]);
                wavData += (short)(wavRaw[1 + (i * 2)] << 8);
                double wavAmplitude = wavData / (double)short.MaxValue;
                wavFilter.AddSample(wavAmplitude);
                wavSamples[i] = wavFilter.GetSample();
                wavSamples[i] = wavAmplitude;
            }
            return wavSamples;
        }

        private static void WriteWav(string filename, double[] samples)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create))
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    short wavData = (short)(samples[i] * short.MaxValue);
                    byte lower = (byte)(wavData & 0xFF);
                    byte upper = (byte)((wavData & 0xFF00) >> 8);
                    fs.WriteByte(lower);
                    fs.WriteByte(upper);
                }
            }
        }


    }
}