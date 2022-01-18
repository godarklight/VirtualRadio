//https://flylib.com/books/en/2.729.1/designing_a_discrete_hilbert_transformer.html 9.4.2
using System;
using System.Numerics;

namespace VirtualRadio.Common
{
    public static class Constants
    {
        public const int CHUNK_SIZE = 1024;
        //Delay in milliseconds, defends against jitter.
        public const double DELAY = 50;
        //Server bandwidth
        public const int SERVER_BANDWIDTH = 250000;
        public const int AUDIO_RATE = 48000;
        public const int FM_BANDWIDTH = 12500;
    }
}