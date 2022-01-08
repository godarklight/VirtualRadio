//https://flylib.com/books/en/2.729.1/designing_a_discrete_hilbert_transformer.html 9.4.2
using System;
using System.Numerics;

namespace VirtualRadio.Common
{
    public static class Hilbert
    {
        public static Complex[] Calculate(Complex[] input)
        {
            Complex[] fft = FFT.CalcFFT(input);
            Complex[] retVal = new Complex[fft.Length];
            for (int i = 0; i < fft.Length / 2; i++)
            {
                //Multiply FFT by 2
                retVal[i] = 2.0 * fft[i];
            }

            //Divide DC term and half term by two
            retVal[0] = retVal[0] / 2;
            retVal[fft.Length / 2] = retVal[fft.Length / 2];

            return (FFT.CalcIFFT(retVal));
        }

        public static double[] Calculate(double[] samples)
        {
            double[] output = new double[samples.Length];
            Complex[] transfer = new Complex[8192];
            int transfer8 = transfer.Length / 8;
            int samplesLeft = samples.Length;
            bool first = true;
            while (samplesLeft > transfer.Length)
            {
                int offset = samples.Length - samplesLeft;
                first = false;

                for (int i = 0; i < transfer.Length; i++)
                {
                    transfer[i] = samples[i + offset];
                }
                Complex[] transferHilbert = Calculate(transfer);
                //First 1/8th, we overlap
                for (int i = 0; i < transfer8; i++)
                {
                    double amplitude = i / (double)transfer8;
                    double oldAmplitude = 1 - amplitude;
                    if (first)
                    {
                        amplitude = 1;
                        oldAmplitude = 0;
                    }
                    output[offset + i] = transferHilbert[i].Imaginary * amplitude + output[offset + i] * oldAmplitude;
                }

                //Copy rest
                for (int i = transfer8; i < transfer.Length; i++)
                {
                    output[offset + i] = transferHilbert[i].Imaginary;
                }

                //Overlap last quarter
                samplesLeft -= (7 * transfer8);
            }
            return output;
        }
    }
}