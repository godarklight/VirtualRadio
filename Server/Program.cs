using System;
using VirtualRadio.Common;

namespace VirtualRadio
{
    class Program
    {
        private static TcpServer tcps;
        public static void Main()
        {
            Random r = new Random();
            byte[] randomData = new byte[1024 * 1024];
            r.NextBytes(randomData);


            Tuple<int, byte[]> compressBytes = Compression.Compress(randomData, 0, randomData.Length);
            Tuple<int, byte[]> decompressBytes = Compression.Decompress(compressBytes.Item2, 0, compressBytes.Item1);

            Console.WriteLine($"Compression ratio: {compressBytes.Item1 / (double)randomData.Length}");

            for (int i = 0; i < randomData.Length; i++)
            {
                if (randomData[i] != decompressBytes.Item2[i])
                {
                    Console.WriteLine("Error");
                }
            }

            tcps = new TcpServer();
            tcps.Run();
        }
    }
}