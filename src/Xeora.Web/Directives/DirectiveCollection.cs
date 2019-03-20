﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xeora.Web.Basics;
using Xeora.Web.Directives.Elements;

namespace Xeora.Web.Directives
{
    public class DirectiveCollection : List<IDirective>
    {
        private readonly Dictionary<int, bool> _Toucheds;
        private readonly ConcurrentQueue<int> _Undones;

        private readonly IMother _Mother;
        private IDirective _Parent;

        public DirectiveCollection(IMother mother, IDirective parent)
        {
            this._Toucheds = new Dictionary<int, bool>();
            this._Undones = new ConcurrentQueue<int>();

            this._Mother = mother;
            this._Parent = parent;
        }

        public void OverrideParent(IDirective parent) =>
            this._Parent = parent;

        public new void Add(IDirective item)
        {
            item.Mother = this._Mother;
            item.Parent = this._Parent;

            if (item is Control)
                ((Control)item).Load();

            this._Undones.Enqueue(this.Count);
            base.Add(item);
        }

        public new void AddRange(IEnumerable<IDirective> collection)
        {
            foreach (IDirective item in collection)
                this.Add(item);
        }

        public void Render(string requesterUniqueID)
        {
            string currentHandlerID = Helpers.CurrentHandlerID;

            while(this._Undones.TryDequeue(out int index))
            {
                switch (this[index].Status)
                {
                    case RenderStatus.Rendering:
                        this._Undones.Enqueue(index);

                        continue;
                    case RenderStatus.Rendered:
                        continue;
                    case RenderStatus.None:
                        if (!this._Toucheds.ContainsKey(index))
                            break;

                        this._Undones.Enqueue(index);
                        continue;
                }

                if (!this[index].CanAsync)
                {
                    this.Render(currentHandlerID, requesterUniqueID, this[index]);
                    continue;
                }

                Task.Run(() => this.Render(currentHandlerID, requesterUniqueID, this[index]));
                this._Undones.Enqueue(index);

                this._Toucheds[index] = true;
            }

            StringBuilder resultContainer = new StringBuilder();

            foreach (IDirective directive in this)
                resultContainer.Append(directive.Result);

            this._Parent.Deliver(RenderStatus.Rendering, resultContainer.ToString());
        }

        private void Render(string handlerID, string requesterUniqueID, IDirective directive)
        {
            Helpers.AssignHandlerID(handlerID);

            try
            {
                // Analytics Calculator
                DateTime renderBegins = DateTime.Now;

                directive.Render(requesterUniqueID);

                if (directive.Parent != null)
                    directive.Parent.HasInlineError |= directive.HasInlineError;

                if (Configurations.Xeora.Application.Main.PrintAnalytics)
                {
                    string analyticOutput = directive.UniqueID;
                    if (directive is INamable)
                        analyticOutput = string.Format("{0} - {1}", analyticOutput, ((INamable)directive).DirectiveID);
                    Basics.Console.Push(
                        string.Format("analytic - {0}", directive.GetType().Name),
                        string.Format("{0}ms {{{1}}}", DateTime.Now.Subtract(renderBegins).TotalMilliseconds, analyticOutput),
                        string.Empty, false);
                }
            }
            catch (System.Exception ex)
            {
                if (directive.Parent != null)
                    directive.Parent.HasInlineError = true;

                Helper.EventLogger.Log(ex);

                if (Configurations.Xeora.Application.Main.Debugging)
                {
                    string exceptionString = string.Empty;
                    do
                    {
                        exceptionString =
                            string.Format(
                                "<div align='left' style='border: solid 1px #660000; background-color: #FFFFFF'><div align='left' style='font-weight: bolder; color:#FFFFFF; background-color:#CC0000; padding: 4px;'>{0}</div><br><div align='left' style='padding: 4px'>{1}{2}</div></div>",
                                ex.Message,
                                ex.Source,
                                (!string.IsNullOrEmpty(exceptionString) ? string.Concat("<hr size='1px' />", exceptionString) : null)
                            );

                        ex = ex.InnerException;
                    } while (ex != null);

                    directive.Deliver(RenderStatus.Rendered, exceptionString);
                }
                else
                    directive.Deliver(RenderStatus.Rendered, string.Empty);
            }
        }

        public IDirective Find(string directiveID) => 
            this.Find(this, directiveID);

        private IDirective Find(DirectiveCollection directives, string directiveID)
        {
            foreach (IDirective directive in directives)
            {
                if (!directive.Searchable)
                    continue;

                if (!(directive is INamable))
                    continue;

                if (string.Compare(((INamable)directive).DirectiveID, directiveID) == 0)
                    return directive;

                if (directive is Control control)
                {
                    switch (control.Type)
                    {
                        case Basics.Domain.Control.ControlTypes.ConditionalStatement:
                        case Basics.Domain.Control.ControlTypes.VariableBlock:
                            directive.Render(directives._Parent?.UniqueID);

                            break;
                    }
                }
                else
                    directive.Parse();

                DirectiveCollection children =
                    ((IHasChildren)directive).Children;

                IDirective result =
                    this.Find(children, directiveID);

                if (result != null)
                    return result;
            }

            return null;
        }
    }
}