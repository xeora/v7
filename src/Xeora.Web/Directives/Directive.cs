﻿using System;
using Xeora.Web.Global;

namespace Xeora.Web.Directives
{
    public abstract class Directive : IDirective
    {
        protected Directive(DirectiveTypes type, ArgumentCollection arguments)
        {
            this.UniqueId = Guid.NewGuid().ToString();

            this.Mother = null;
            this.Parent = null;

            this.Type = type;
            this.Arguments = arguments;
            if (this.Arguments == null)
                this.Arguments = new ArgumentCollection();

            this.Scheduler =
                new DirectiveScheduler(
                    (uniqueId) =>
                    {
                        this.Mother.Pool.GetByUniqueId(uniqueId, out IDirective directive);

                        if (directive != null) directive.Render(this.UniqueId);
                    }
                );

            this.Result = string.Empty;
        }

        public string UniqueId { get; private set; }

        public IMother Mother { get; set; }
        public IDirective Parent { get; set; }

        public DirectiveTypes Type { get; private set; }
        public ArgumentCollection Arguments { get; private set; }

        public DirectiveScheduler Scheduler { get; private set; }

        public abstract bool Searchable { get; }
        public abstract bool CanAsync { get; }
        public bool HasInlineError { get; set; }
        public RenderStatus Status { get; protected set; }

        public abstract void Parse();
        public abstract void Render(string requesterUniqueId);

        public void Deliver(RenderStatus status, string result)
        {
            this.Result = result;
            this.Status = status;
        }
        public string Result { get; private set; }
    }
}