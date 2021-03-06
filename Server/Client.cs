using System;
using System.Collections.Concurrent;
using System.IO;
using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using VirtualRadio.Common;

namespace VirtualRadio.Server
{
    public class Client
    {
        public bool sendIQ = false;
        public double SEND_VOLUME = 0.1;
        public TcpClient tcpClient;
        public bool connected = true;
        private Thread networkThread;
        byte[] receiveBuffer = new byte[1 * 1024 * 1024];
        bool receiveHeader = true;
        MessageType receiveType = MessageType.HEARTBEAT;
        int receiveSize = 8;
        int receiveLeft = 8;
        byte[] buffer = new byte[1 * 1024 * 1024];
        byte[] compressCopy = new byte[1 * 1024 * 1024];
        int compressLength = 0;
        long lastRecieve = DateTime.UtcNow.Ticks;
        long lastSend = DateTime.UtcNow.Ticks;
        long TIMEOUT = TimeSpan.TicksPerMinute;
        RadioMode radioMode = RadioMode.USB;
        double vfo = 0;
        double carrierAngle = 0;
        double sendSampleTime = 0;
        Complex[] transmitSamples = null;
        int transmitPos = 0;
        ConcurrentQueue<Complex[]> transmitQueue = new ConcurrentQueue<Complex[]>();
        ConcurrentQueue<Complex[]> freeQueue = new ConcurrentQueue<Complex[]>();
        double audioServerSampleRatio = Constants.AUDIO_RATE / (double)Constants.SERVER_BANDWIDTH;
        long transmitDelay = 0;
        string remoteEndpoint = null;
        Complex lastSample = Complex.Zero;
        Complex sendSample = Complex.Zero;

        public Client(TcpClient tcpClient)
        {
            //~1 second of buffers
            for (int i = 0; i < 50; i++)
            {
                freeQueue.Enqueue(new Complex[Constants.CHUNK_SIZE]);
            }
            this.tcpClient = tcpClient;
            tcpClient.NoDelay = true;
            networkThread = new Thread(new ThreadStart(NetworkThreadMain));
            remoteEndpoint = tcpClient.Client.RemoteEndPoint.ToString();
            networkThread.Name = $"Network Thread {remoteEndpoint}";
            networkThread.Start();
            Console.WriteLine($"Client connected {remoteEndpoint}");
        }

        public void QueueBytes(byte[] input, int inputLength)
        {
            while (compressLength > 0)
            {
                Thread.Sleep(1);
            }
            Array.Copy(input, 0, compressCopy, 0, inputLength);
            compressLength = inputLength;
        }

        public void WriteSamples(Complex[] samples)
        {
            if (transmitSamples == null && transmitDelay == 0 && transmitQueue.Count > 0)
            {
                //100ms delay, network jitter buffer.
                transmitDelay = DateTime.UtcNow.Ticks + 100 * TimeSpan.TicksPerMillisecond;
            }
            if (transmitDelay != 0 && DateTime.UtcNow.Ticks > transmitDelay)
            {
                transmitQueue.TryDequeue(out transmitSamples);
                transmitDelay = 0;
                sendSample = transmitSamples[0];
            }
            if (transmitSamples == null)
            {
                return;
            }
            double carrierOffset = vfo - Constants.SERVER_BANDWIDTH / 2.0;
            for (int i = 0; i < samples.Length; i++)
            {
                sendSampleTime += audioServerSampleRatio;
                while (sendSampleTime > 1.0)
                {
                    sendSampleTime -= 1.0;
                    transmitPos++;
                    if (transmitSamples.Length == transmitPos)
                    {
                        //Mark the buffer free for future use
                        freeQueue.Enqueue(transmitSamples);
                        //Process the next buffer
                        transmitQueue.TryDequeue(out transmitSamples);
                        transmitPos = 0;
                    }
                    //We finished transmitting
                    if (transmitSamples == null)
                    {
                        return;
                    }
                    lastSample = sendSample;
                    sendSample = transmitSamples[transmitPos];
                }
                Complex interpolated = ((1.0 - sendSampleTime) * lastSample) + (sendSampleTime * sendSample);
                Complex carrier = new Complex(Math.Cos(carrierAngle), Math.Sin(carrierAngle));

                if (radioMode == RadioMode.AM)
                {
                    //AM mode
                    double amOffset = (interpolated.Real + 1.0) / 2.0;
                    samples[i] = samples[i] + (carrier * amOffset * SEND_VOLUME);
                }

                //FM mode
                if (radioMode == RadioMode.FM)
                {
                    double fmFreqOffset = interpolated.Real * Constants.FM_BANDWIDTH / 2.0;
                    carrierAngle += (Math.Tau * fmFreqOffset) / (double)Constants.SERVER_BANDWIDTH;
                    samples[i] = samples[i] + (carrier * SEND_VOLUME);
                }

                //WFM mode
                if (radioMode == RadioMode.WFM)
                {
                    double fmFreqOffset = interpolated.Real * Constants.WFM_BANDWIDTH / 2.0;
                    carrierAngle += (Math.Tau * fmFreqOffset) / (double)Constants.SERVER_BANDWIDTH;
                    samples[i] = samples[i] + (carrier * SEND_VOLUME);
                }

                //SSB mode
                if (radioMode == RadioMode.LSB || radioMode == RadioMode.USB)
                {
                    Complex signal = interpolated;
                    if (radioMode == RadioMode.USB)
                    {
                        signal = Complex.Conjugate(signal);
                    }
                    Complex addSample = carrier * (signal * SEND_VOLUME);
                    samples[i] = samples[i] + addSample;
                }

                //Increase carrier phase
                carrierAngle += (Math.Tau * carrierOffset) / (double)Constants.SERVER_BANDWIDTH;
                carrierAngle = carrierAngle % Math.Tau;
            }
        }

        private void NetworkThreadMain()
        {
#if !DEBUG
            try
            {
#endif
            while (connected)
            {
                bool sleep = true;
                //Receive
                if (tcpClient.Available > 0)
                {
                    sleep = false;
                    int readBytes = 0;
                    try
                    {
                        readBytes = tcpClient.GetStream().Read(receiveBuffer, receiveSize - receiveLeft, receiveLeft);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Client {remoteEndpoint} error: {e.Message}");
                        connected = false;
                    }
                    if (readBytes == 0)
                    {
                        connected = false;
                        break;
                    }
                    receiveLeft -= readBytes;
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
                //Send compressed samples
                if (compressLength > 0)
                {
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.DATA)).CopyTo(u8, 0);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder(compressLength)).CopyTo(u8, 4);
                    try
                    {
                        tcpClient.GetStream().Write(u8, 0, 8);
                        tcpClient.GetStream().Write(compressCopy, 0, compressLength);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Client {remoteEndpoint} error: {e.Message}");
                        connected = false;
                    }
                    compressLength = 0;
                }
                //Send heartbeat every half timeout
                long currentTime = DateTime.UtcNow.Ticks;
                if (currentTime - lastSend > TIMEOUT / 2)
                {
                    lastSend = currentTime;
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)MessageType.HEARTBEAT)).CopyTo(u8, 0);
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder(0)).CopyTo(u8, 4);
                    try
                    {
                        tcpClient.GetStream().Write(u8, 0, 8);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Client {remoteEndpoint} error: {e.Message}");
                        connected = false;
                    }
                }
                if (sleep)
                {
                    Thread.Sleep(1);
                }
            }
#if !DEBUG
        }
            catch (Exception e)
            {
                Console.WriteLine($"Client {remoteEndpoint} error: {e.Message}");
            }
#endif
            Console.WriteLine($"Client {remoteEndpoint} disconnected");
            connected = false;
        }

        byte[] u8 = new byte[8];
        private void ProcessMessage()
        {
            switch (receiveType)
            {
                case MessageType.HEARTBEAT:
                    //Do nothing
                    break;
                case MessageType.SET_VFO:
                    Array.Copy(receiveBuffer, 0, u8, 0, 8);
                    Array.Reverse(u8);
                    vfo = BitConverter.ToDouble(u8);
                    break;
                case MessageType.SET_MODE:
                    int radioModeInt = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(receiveBuffer, 0));
                    radioMode = (RadioMode)radioModeInt;
                    break;
                case MessageType.DATA:
                    int bytesToAdd = Compression.Decompress(receiveBuffer, 0, receiveSize, buffer);
                    Complex[] free = null;
                    if (!freeQueue.TryDequeue(out free))
                    {
                        //The transmit buffers have been filled, let's assume they aren't sending at the correct audio rate and clear the buffers.
                        Console.WriteLine($"Client {remoteEndpoint} clearing transmit buffers, client sending rate too fast");
                        Complex[] moveObject = null;
                        while (transmitQueue.TryDequeue(out moveObject))
                        {
                            freeQueue.Enqueue(moveObject);
                        }
                        if (!freeQueue.TryDequeue(out free))
                        {
                            //I don't think this is possible
                            Console.WriteLine($"Client {remoteEndpoint}: No transmit buffers available, disconnecting");
                            connected = false;
                        }
                    }
                    for (int i = 0; i < bytesToAdd / 4; i++)
                    {
                        free[i] = FormatConvert.ByteArrayToIQ16(buffer, i * 4);
                    }
                    transmitQueue.Enqueue(free);
                    break;
                case MessageType.ENABLE_IQ:
                    sendIQ = true;
                    break;
            }
            lastRecieve = DateTime.UtcNow.Ticks;
        }
    }
}