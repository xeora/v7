﻿using System.Collections.Concurrent;
using System.Threading;

namespace Xeora.Web.Deployment
{
    public sealed class InstanceFactory
    {
        private readonly ConcurrentDictionary<string, Domain> _Instances;

        public InstanceFactory() =>
            this._Instances = new ConcurrentDictionary<string, Domain>();

        private static object _Lock = new object();
        private static InstanceFactory _Current;
        public static InstanceFactory Current
        {
            get
            {
                Monitor.Enter(InstanceFactory._Lock);
                try
                {
                    if (InstanceFactory._Current == null)
                        InstanceFactory._Current = new InstanceFactory();
                }
                finally
                {
                    Monitor.Exit(InstanceFactory._Lock);
                }

                return InstanceFactory._Current;
            }
        }

        public Domain GetOrCreate(string[] domainIdAccessTree)
        {
            string instancenKey = 
                string.Join<string>("-", domainIdAccessTree);

            if (!this._Instances.TryGetValue(instancenKey, out Domain domain))
            {
                domain = new Domain(domainIdAccessTree);

                if (!this._Instances.TryAdd(instancenKey, domain))
                    return this.GetOrCreate(domainIdAccessTree);
            }
            else
                domain.Reload();

            return domain;
        }

        public void Reset()
        {
            foreach (string key in this._Instances.Keys)
            {
                this._Instances.TryRemove(key, out Domain domain);

                if (domain != null)
                    domain.Dispose();
            }
        }

        ~InstanceFactory() => this.Reset();
    }
}