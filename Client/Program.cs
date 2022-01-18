using System;
using System.Threading;
namespace VirtualRadio.Client
{
    class Program
    {
        private static Thread clientThread;
        private static RadioClient radioClient;
        public static void Main(string[] args)
        {
            string radioServerAddress = "godarklight.privatedns.org:1235";
            if (args.Length >= 1)
            {
                radioServerAddress = args[0];
            }
            Thread.CurrentThread.Name = "Main Thread";
            radioClient = new RadioClient(radioServerAddress);
            if (!radioClient.running)
            {
                Console.WriteLine("Failed to connect to server");
                return;
            }
            clientThread = new Thread(new ThreadStart(radioClient.Run));
            clientThread.Start();
            Console.WriteLine("Commmands: ");
            Console.WriteLine("v: Set transmit carrier frequency [0-250000]");
            Console.WriteLine("m: Set mode [AM,FM,USB,LSB]");
            Console.WriteLine("q: Quit");
            while (true)
            {
                string currentLine = Console.ReadLine();
                if (currentLine == "q")
                {
                    break;
                }
                if (currentLine == "v")
                {
                    Console.WriteLine("Enter VFO frequency");
                    currentLine = Console.ReadLine();
                    double divide = 1;
                    int kIndex = currentLine.IndexOf("k", StringComparison.InvariantCultureIgnoreCase);
                    if (kIndex > 0)
                    {
                        currentLine = currentLine.Substring(0, kIndex);
                        divide = 1000;
                    }
                    int mIndex = currentLine.IndexOf("M", StringComparison.InvariantCultureIgnoreCase);
                    if (mIndex > 0)
                    {
                        currentLine = currentLine.Substring(0, mIndex);
                        divide = 1000000;
                    }
                    radioClient.sendVfo = double.Parse(currentLine) / divide;
                }
            }
            radioClient.Stop();
            clientThread.Join();
            Console.WriteLine("Quit");
        }
    }
}