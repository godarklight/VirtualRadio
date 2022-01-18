using System;
using System.IO;
using System.IO.Compression;

namespace VirtualRadio.Common
{
    public static class Compression
    {
        public static int Compress(byte[] input, int offset, int length, byte[] buffer)
        {
            int retVal = 0;
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (ZLibStream zls = new ZLibStream(ms, CompressionMode.Compress))
                {
                    zls.Write(input, offset, length);
                    zls.Flush();
                    retVal = (int)ms.Position;
                }
            }
            return retVal;
        }

        public static int Decompress(byte[] input, int offset, int length, byte[] buffer)
        {
            int retVal = 0;
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
                    retVal = decompressBytes;
                }
            }
            return retVal;
        }
    }
}