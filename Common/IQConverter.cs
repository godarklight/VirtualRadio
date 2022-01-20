using System;
using System.Numerics;

namespace VirtualRadio.Common
{
    public static class FormatConvert
    {
        public static double S16ToDouble(byte[] input, int index)
        {
            short rawShort = (short)(input[index + 1] << 8);
            rawShort |= (short)(input[index]);
            return rawShort / (double)short.MaxValue;
        }
        public static void DoubleToS16(double input, byte[] output, int index)
        {
            short rawShort = (short)(input * short.MaxValue);
            output[index] = (byte)(rawShort & 0xFF);
            output[index + 1] = (byte)(rawShort >> 8);
        }
        public static void IQToByteArray8(Complex input, byte[] output, int index)
        {
            output[index] = (byte)((input.Real + 1.0) * 127);
            output[index + 1] = (byte)((input.Imaginary + 1.0) * 127);
        }
        public static Complex ByteArrayToIQ8(byte[] input, int index)
        {
            double realPart = (input[index] / 127.0) - 1.0;
            double imaginaryPart = (input[index + 1] / 127.0) - 1.0;
            return new Complex(realPart, imaginaryPart);
        }
        public static void IQToByteArray16(Complex input, byte[] output, int index)
        {
            DoubleToS16(input.Real, output, index);
            DoubleToS16(input.Imaginary, output, index + 2);
        }
        public static Complex ByteArrayToIQ16(byte[] input, int index)
        {
            return new Complex(S16ToDouble(input, index), S16ToDouble(input, index + 2));
        }
    }
}