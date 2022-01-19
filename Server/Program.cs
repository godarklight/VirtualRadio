using System;
using System.Net;
using System.Threading;

namespace VirtualRadio.Server
{
    class Program
    {
        private static RadioServer radioServer;
        public static void Main()
        {
            Thread.CurrentThread.Name = "Main Thread";
            radioServer = new RadioServer();
            radioServer.Run();
        }
    }
}