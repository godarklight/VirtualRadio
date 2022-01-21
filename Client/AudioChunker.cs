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
        private RadioMode mode;
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

        private IFilter GenerateFilterWFM(int index)
        {
            if (index == 0)
            {
                return new WindowedSinc(15000, 1024, 48000, false);
            }
            return new Butterworth(15000, 48000, false);

        }

        private IFilter GenerateFilterAMFM(int index)
        {
            if (index == 0)
            {
                return new WindowedSinc(5000, 1024, 48000, false);
            }
            return new Butterworth(5000, 48000, false);

        }

        private IFilter GenerateFilterSSB(int index)
        {
            if (index == 0)
            {
                return new WindowedSinc(2700, 1024, 48000, false);
            }
            return new Butterworth(2700, 48000, false);
        }

        public void SetFilterMode(RadioMode mode)
        {
            this.mode = mode;
            switch (mode)
            {
                case RadioMode.USB:
                case RadioMode.LSB:
                    audioFilter = new LayeredFilter(GenerateFilterSSB, 2);
                    break;
                case RadioMode.AM:
                case RadioMode.FM:
                    audioFilter = new LayeredFilter(GenerateFilterAMFM, 2);
                    break;
                case RadioMode.WFM:
                    audioFilter = new LayeredFilter(GenerateFilterWFM, 2);
                    break;
            }
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
                    Complex[] hilbertSmooth = samples;
                    //Only run the hilbert transform on LSB/USB mode
                    if (mode == RadioMode.LSB || mode == RadioMode.USB)
                    {
                        hilbertSmoother.AddChunk(samples);
                        hilbertSmooth = hilbertSmoother.GetChunk();
                    }
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