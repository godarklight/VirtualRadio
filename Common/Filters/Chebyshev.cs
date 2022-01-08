//http://www.dspguide.com/ch20/4.htm
using System;
namespace VirtualRadio.Common
{
    public class Chebyshev : IFilter
    {
        int poles;
        double[] a = new double[22];
        double[] b = new double[22];
        double[] inputValues = new double[22];
        double[] filterValues = new double[22];
        public Chebyshev(double frequency, double samplerate, double precentRipple, int poles, bool highpass)
        {
            this.poles = poles;
            double fc = frequency / samplerate;
            if (fc > 0.5)
            {
                throw new ArgumentOutOfRangeException("Frequency must be less than half the sample rate");
            }
            if (precentRipple > 29)
            {
                throw new ArgumentOutOfRangeException("Percent ripple must be less than 29");
            }
            if (poles > 20)
            {
                throw new ArgumentOutOfRangeException("Must be less than 20 poles");
            }
            if (poles % 2 == 1)
            {
                throw new ArgumentOutOfRangeException("Must have an even number of poles");
            }

            double[] ta = new double[22];
            double[] tb = new double[22];

            a[2] = 1;
            b[2] = 1;

            //Loop each pole pair
            for (int pole = 1; pole < poles / 2; pole++)
            {
                double[] subReturn = CalculatePoles(pole, poles, fc, precentRipple, highpass);
                double a0 = subReturn[0];
                double a1 = subReturn[1];
                double a2 = subReturn[2];
                double b1 = subReturn[3];
                double b2 = subReturn[4];

                //Add coefficents to the cascade
                for (int j = 0; j < 22; j++)
                {
                    ta[j] = a[j];
                    tb[j] = b[j];
                }

                for (int j = 2; j < 22; j++)
                {
                    a[j] = a0 * ta[j] + a1 * ta[j - 1] + a2 * ta[j - 2];
                    b[j] = b[j] - b1 * tb[j - 1] - b2 * tb[j - 2];
                }
            }

            //Finish combining
            b[2] = 0;
            for (int i = 0; i < 20; i++)
            {
                a[i] = a[i + 2];
                b[i] = b[i + 2];
            }
        }

        private double[] CalculatePoles(int pole, int poles, double fc, double precentRipple, bool highpass)
        {
            //Calculate the pole location on the unit circle
            double rp = -Math.Cos(Math.PI / (poles * 2) + (pole - 1) * Math.PI / poles);
            double ip = Math.Sin(Math.PI / (poles * 2) + (pole - 1) * Math.PI / poles);

            //Wrap from a circle to an ellipse
            if (precentRipple != 0)
            {
                double es = Math.Sqrt(Math.Pow(100.0 / (100.0 - precentRipple), 2) - 1.0);
                double vx = (1.0 / poles) * Math.Log((1.0 / es) + Math.Sqrt(1 / (es * es) + 1.0));
                double kx = (1.0 / poles) * Math.Log((1.0 / es) + Math.Sqrt(1 / (es * es) - 1.0));
                kx = (Math.Exp(kx) + Math.Exp(-kx)) / 2.0;
                rp = rp * ((Math.Exp(vx) - Math.Exp(-vx)) / 2) / kx;
                ip = ip * ((Math.Exp(vx) + Math.Exp(-vx)) / 2) / kx;
            }

            //s-domain to z-domain conversion
            double t = 2 * Math.Tan(0.5);
            double w = Math.Tau * fc;
            double m = (rp * rp) + (ip * ip);
            double d = 4.0 - 4.0 * rp * t + m * (t * t);
            double x0 = (t * t) / d;
            double x1 = 2 * x0;
            double x2 = x0;
            double y1 = (8.0 - 2.0 * m * (t * t)) / d;
            double y2 = (-4.0 - 4.0 * rp * t - m * (t * t)) / d;

            //LP to LP or LP to HP transform
            double k = -Math.Cos(w / 2.0 + 0.5) / Math.Cos(w / 2.0 - 0.5);
            if (!highpass)
            {
                k = Math.Sin(0.5 - w / 2.0) / Math.Sin(0.5 + w / 2.0);
            }
            d = 1 + y1 * k - y2 * (k * k);
            double a0 = (x0 - x1 * k + x2 * (k * k)) / d;
            double a1 = ((-2.0 * x0 * k) + x1 + (x1 * (k * k)) - (2.0 * x2 * k)) / d;
            double a2 = (x0 * (k * k) - x1 * k + x2) / d;
            double b1 = (2.0 * k + y1 + y1 * (k * k) - 2.0 * y2 * k) / d;
            double b2 = (-(k * k) - y1 * k + y2) / d;
            if (highpass)
            {
                a1 = -a1;
                b1 = -b1;
            }
            return new double[] { a0, a1, a2, b1, b2 };
        }

        public void AddSample(double input)
        {
            //Shift values
            for (int i = 20; i >= 0; i--)
            {
                inputValues[i + 1] = inputValues[i];
                filterValues[i + 1] = filterValues[i];
            }
            inputValues[0] = input;
            filterValues[0] = 0;


            //Calculate
            double newValue = 0;
            for (int i = 0; i < inputValues.Length; i++)
            {
                newValue += a[i] * inputValues[i];
            }

            filterValues[0] = newValue;
        }

        public double GetSample()
        {
            return filterValues[0];
        }
    }
}