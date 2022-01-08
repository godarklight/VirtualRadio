//Not thread safe due to shared buffer
using System;
using System.IO;
using System.IO.Compression;

namespace VirtualRadio.Common
{
    public static class Compression
    {
        private static byte[] buffer = new byte[8 * 1024 * 1024];
        public static Tuple<int, byte[]> Compress(byte[] input, int offset, int length)
        {
            Tuple<int, byte[]> retVal = null;
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (ZLibStream zls = new ZLibStream(ms, CompressionMode.Compress))
                {
                    zls.Write(input, offset, length);
                    zls.Flush();
                    retVal = new Tuple<int, byte[]>((int)ms.Position, buffer);
                }
            }
            return retVal;
        }

        public static Tuple<int, byte[]> Decompress(byte[] input, int offset, int length)
        {
            Tuple<int, byte[]> retVal = null;
            using (MemoryStream ms = new MemoryStream(input, offset, length))
            {
                using (ZLibStream zls = new ZLibStream(ms, CompressionMode.Decompress))
                {
                    int decompressBytes = 0;
                    while (true)
                    {
                        int thisRead = zls.Read(buffer, decompressBytes, buffer.Length - decompressBytes);
                        decompressBytes += thisRead;
                        if (thisRead == 0)
                        {
                            break;
                        }
                    }
                    retVal = new Tuple<int, byte[]>(decompressBytes, buffer);
                }
            }
            return retVal;
        }
    }
}