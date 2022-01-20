using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;

using VirtualRadio.Common;

namespace VirtualRadio.Client
{
    class AudioChunker
    {
        private IFilter audioFilter;
        HilbertSmoother hilbertSmoother = new HilbertSmoother();
        private ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();
        private ConcurrentQueue<byte[]> freeQueue = new ConcurrentQueue<byte[]>();
        private Thread processThread = null;
        private bool isRunning = true;
        private byte[] chunk = null;
        public volatile bool isReading = false;

        public AudioChunker()
        {
            SetFilterMode(RadioMode.USB);
            processThread = new Thread(new ThreadStart(ProcessThread));
            processThread.Start();
            for (int i = 0; i < 16; i++)
            {
                freeQueue.Enqueue(new byte[4 * Constants.CHUNK_SIZE]);
            }
        }

        public void SetFilterMode(RadioMode mode)
        {
            if (mode == RadioMode.USB || mode == RadioMode.LSB)
            {
                audioFilter = new WindowedSinc(2700, 1024, 48000, false);
            }
            audioFilter = new WindowedSinc(5000, 2048, 48000, false);
        }

        public void Stop()
        {
            isRunning = false;
            processThread.Join();
        }

        private void ProcessThread()
        {
            while (isRunning)
            {
                if (isReading)
                {
                    Complex[] samples = new Complex[Constants.CHUNK_SIZE];
                    for (int i = 0; i < Constants.CHUNK_SIZE; i++)
                    {
                        double rawDouble = FormatConvert.S16ToDouble(chunk, i * 2);
                        audioFilter.AddSample(rawDouble);
                        double filteredDouble = audioFilter.GetSample();
                        samples[i] = filteredDouble;
                    }
                    hilbertSmoother.AddChunk(samples);
                    Complex[] hilbertSmooth = hilbertSmoother.GetChunk();
                    if (hilbertSmooth != null)
                    {
                        //Complex[] hilbert = Hilbert.Calculate(samples);
                        byte[] writeBuffer = null;
                        //Grab a free buffer
                        while (!freeQueue.TryDequeue(out writeBuffer))
                        {
                            Thread.Sleep(1);
                        }
                        for (int i = 0; i < Constants.CHUNK_SIZE; i++)
                        {
                            FormatConvert.IQToByteArray16(hilbertSmooth[i], writeBuffer, i * 4);
                        }
                        sendQueue.Enqueue(writeBuffer);
                    }
                    isReading = false;
                }
                Thread.Sleep(1);
            }
        }

        public bool GetSamples(out byte[] output)
        {
            return sendQueue.TryDequeue(out output);
        }

        public void ReturnFreeBuffer(byte[] buffer)
        {
            freeQueue.Enqueue(buffer);
        }

        public void ReceiveBytes(byte[] buffer)
        {
            chunk = buffer;
            isReading = true;
        }
    }
}