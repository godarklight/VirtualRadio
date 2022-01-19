//Generated IFFT's need a little bit of overlap to "smooth" the edges. I do this by generating a small IFFT on the joins.

using System;
using System.Numerics;
using VirtualRadio.Common;

namespace VirtualRadio.Client
{
    public class HilbertSmoother
    {
        Complex[] currentRaw;
        Complex[] nextRaw;
        Complex[] current;
        Complex[] next;
        int count = 0;

        public void AddChunk(Complex[] samples)
        {
            count++;
            current = next;
            currentRaw = nextRaw;
            nextRaw = samples;
            next = Hilbert.Calculate(samples);
            if (current == null)
            {
                return;
            }
            Complex[] join = new Complex[samples.Length / 4];
            int currentOffset = (7 * current.Length) / 8;
            Array.Copy(currentRaw, currentOffset, join, 0, join.Length / 2);
            Array.Copy(nextRaw, 0, join, join.Length / 2, join.Length / 2);
            Complex[] joinHilbert = Hilbert.Calculate(join);

            for (int i = 0; i < joinHilbert.Length / 2; i++)
            {
                double joinScaling = 2.0 * i / (double)joinHilbert.Length;
                double origScaling = 1.0 - joinScaling;
                Complex orig = current[i + currentOffset];
                current[i + currentOffset] = new Complex(orig.Real, (origScaling * orig.Imaginary) + (joinScaling * joinHilbert[i].Imaginary));
            }
            for (int i = 0; i < joinHilbert.Length / 2; i++)
            {
                int iOffset = i + joinHilbert.Length / 2;
                double origScaling = 2.0 * i / (double)joinHilbert.Length;
                double joinScaling = 1.0 - origScaling;
                Complex orig = next[i];
                next[i] = new Complex(orig.Real, (origScaling * orig.Imaginary) + (joinScaling * joinHilbert[iOffset].Imaginary));
            }
        }

        public Complex[] GetChunk()
        {
            if (current == null)
            {
                return null;
            }
            return current;
        }
    }
}