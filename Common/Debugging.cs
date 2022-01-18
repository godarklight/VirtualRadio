using System.IO;
using System.Numerics;

namespace VirtualRadio.Common
{
    public static class Debugging
    {

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