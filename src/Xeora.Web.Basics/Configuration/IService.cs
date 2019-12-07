﻿using System.Net;

namespace Xeora.Web.Basics.Configuration
{
    public interface IService
    {
        IPAddress Address { get; }
        short Port { get; }
        bool Ssl { get; }
        string CertificatePassword { get; }
        IParallelism Parallelism { get; }
        bool Print { get; }
    }
}
