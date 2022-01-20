using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;

using VirtualRadio.Common;

namespace VirtualRadio.Client
{
    class CmdParser
    {
        public RadioMode mode = RadioMode.USB;
        public int vfo = 0;
        public int iqPort = 1234;
        public int audioPort = 1236;
        public string server = "godarklight.privatedns.org";
        public int serverPort = 1235;
        public CmdParser(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith("--vfo="))
                {
                    vfo = Int32.Parse(arg.Substring("--vfo=".Length));
                }
                if (arg.StartsWith("--iqPort="))
                {
                    iqPort = Int32.Parse(arg.Substring("--iqPort=".Length));
                }
                if (arg.StartsWith("--audioPort="))
                {
                    audioPort = Int32.Parse(arg.Substring("--audioPort=".Length));
                }
                if (arg.StartsWith("--server="))
                {
                    server = arg.Substring("--server=".Length);
                    int splitIndex = server.IndexOf(":", StringComparison.InvariantCulture);
                    if (splitIndex > 0)
                    {
                        string portString = server.Substring(splitIndex + 1);
                        serverPort = Int32.Parse(portString);
                        server = server.Substring(0, splitIndex);
                    }
                }
                if (arg.StartsWith("--mode="))
                {
                    string modeString = arg.Substring("--mode=".Length);
                    if (!Enum.TryParse<RadioMode>(modeString, true, out mode))
                    {
                        Console.WriteLine("Mode error, defaulting to USB");
                        mode = RadioMode.USB;
                    }
                }
                if (arg == "--no-audio")
                {
                    audioPort = 0;
                }
                if (arg == "--no-iq")
                {
                    iqPort = 0;
                }
                if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                }
            }
        }


        public void PrintHelp()
        {
            string writeText = @"--vfo=125000: Set the VFO to 125kHZ
            --iqPort=1236: Set the IQ port (rtl_tcp compatible)
            --audioPort: Set the audio port. Format should be mono 48000kHz S16LE
            --server=godarklight.privatedns.org[:port]: Set the server address
            --mode=USB: Set the modulation mode. AM/FM/USB/LSB
            --no-audio: Disable the Audio port
            --no-iq: Disable the IQ port
            --help or -h: Display this text";
            Console.WriteLine(writeText);
        }
    }
}