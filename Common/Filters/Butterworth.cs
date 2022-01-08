//First order butterworth
using System;
namespace VirtualRadio.Common
{
    public class Butterworth : IFilter
    {
        double[] inputCoeff = new double[3];
        double[] filterCoeff = new double[3];
        double[] inputValues = new double[3];
        double[] filterValues = new double[3];
        public Butterworth(double frequency, double sampleRate, bool highpass)
        {
            //https://stackoverflow.com/questions/20924868/calculate-coefficients-of-2nd-order-butterworth-low-pass-filter
            double ff = frequency / sampleRate;
            double ita = 1.0 / Math.Tan(Math.PI * ff);
            double q = Math.Sqrt(2.0);

            inputCoeff[0] = 1.0 / (1.0 + (q * ita) + (ita * ita));
            inputCoeff[1] = 2 * inputCoeff[0];
            inputCoeff[2] = inputCoeff[0];
            filterCoeff[1] = 2.0 * ((ita * ita) - 1.0) * inputCoeff[0];
            filterCoeff[2] = -(1.0 - (q * ita) + (ita * ita)) * inputCoeff[0];

            if (highpass)
            {
                inputCoeff[0] = inputCoeff[0] * ita * ita;
                inputCoeff[1] = -inputCoeff[1] * ita * ita;
                inputCoeff[2] = inputCoeff[2] * ita * ita;
            }
        }

        public void AddSample(double input)
        {
            //Shift values
            inputValues[2] = inputValues[1];
            inputValues[1] = inputValues[0];
            inputValues[0] = input;
            filterValues[2] = filterValues[1];
            filterValues[1] = filterValues[0];


            //Calculate
            double newValue = 0;
            newValue += inputCoeff[0] * inputValues[0];
            newValue += inputCoeff[1] * inputValues[1];
            newValue += inputCoeff[2] * inputValues[2];
            newValue += filterCoeff[1] * filterValues[1];
            newValue += filterCoeff[2] * filterValues[2];
            filterValues[0] = newValue;
        }

        public double GetSample()
        {
            return filterValues[0];
        }
    }
}