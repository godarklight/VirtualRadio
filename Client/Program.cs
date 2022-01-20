using System;
using System.Threading;
using VirtualRadio.Common;
namespace VirtualRadio.Client
{
    class Program
    {
        private static Thread clientThread;
        private static RadioClient radioClient;
        public static void Main(string[] args)
        {
            CmdParser parser = new CmdParser(args);
            Thread.CurrentThread.Name = "Main Thread";
            radioClient = new RadioClient(parser);
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
            while (radioClient.running)
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
                    double sendVfo = double.Parse(currentLine) / divide;
                    if (sendVfo < 0)
                    {
                        sendVfo = -1;
                        Console.WriteLine("VFO frequency must be above 0");
                    }
                    if (sendVfo > 250000)
                    {
                        sendVfo = -1;
                        Console.WriteLine("VFO frequency must be below 250000");
                    }
                    radioClient.sendVfo = sendVfo;
                }
                if (currentLine == "m")
                {
                    Console.WriteLine("Select one of the following modes:");
                    foreach (string valid in Enum.GetNames<RadioMode>())
                    {
                        Console.WriteLine(valid);
                    }
                    currentLine = Console.ReadLine();
                    //Default to upper side band
                    RadioMode mode = RadioMode.USB;
                    if (!Enum.TryParse<RadioMode>(currentLine, true, out mode))
                    {
                        Console.WriteLine("Failed to parse, defaulting to upper side band");
                    }
                    radioClient.sendMode = mode;
                }
                Thread.Sleep(10);
            }
            radioClient.Stop();
            clientThread.Join();
            Console.WriteLine("Quit");
        }
    }
}