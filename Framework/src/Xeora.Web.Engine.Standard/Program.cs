﻿using System;
using Xeora.Web.Service;

namespace Xeora.Web.Engine.Standard
{
    class Program
    {
        static void Main(string[] args)
        {
            WebServer webServer = new WebServer();
            int exitCode = webServer.Start();

            Environment.Exit(exitCode);
        }
    }
}
