﻿using Xeora.Web.Directives.Elements;
using Xeora.Web.Global;

namespace Xeora.Web.Directives.Controls.Elements
{
    public class ConditionalStatement : IControl
    {
        private int _SelectedContent = -1;

        private readonly Control _Parent;
        private readonly ContentDescription _Contents;
        private readonly string[] _Parameters;
        private readonly Application.Domain.Controls.ConditionalStatement _Settings;
        private DirectiveCollection _Children;
        private bool _Parsed;

        public ConditionalStatement(Control parent, ContentDescription contents, string[] parameters, Application.Domain.Controls.ConditionalStatement settings)
        {
            this._Parent = parent;
            this._Contents = contents;
            this._Parameters = parameters;
            this._Settings = settings;
        }

        public DirectiveCollection Children => this._Children;
        public bool LinkArguments => true;

        public void Parse()
        {
            if (this._SelectedContent == -1)
                return;

            if (this._Parsed)
                return;
            this._Parsed = true;

            this._Children = new DirectiveCollection(this._Parent.Mother, this._Parent);

            this._Parent.Mother.RequestParsing(
                this._Contents.Parts[this._SelectedContent], ref this._Children, this._Parent.Arguments);
        }

        public void Render(string requesterUniqueId)
        {
            if (this._Settings.Bind == null)
                throw new System.ArgumentNullException(nameof(this._Settings.Bind));

            this._Settings.Bind.Parameters.Prepare(
                parameter =>
                {
                    string query = parameter.Query;

                    int paramIndex = 
                        DirectiveHelper.CaptureParameterPointer(query);

                    if (paramIndex > -1)
                    { 
                        if (paramIndex >= this._Parameters.Length)
                            throw new Exception.FormatIndexOutOfRangeException();

                        query = this._Parameters[paramIndex];
                    }

                    return DirectiveHelper.RenderProperty(this._Parent, query, this._Parent.Arguments, requesterUniqueId);
                }
            );

            Basics.Execution.InvokeResult<Basics.ControlResult.Conditional> invokeResult =
                Manager.AssemblyCore.InvokeBind<Basics.ControlResult.Conditional>(Basics.Helpers.Context.Request.Header.Method, this._Settings.Bind, Manager.ExecuterTypes.Control);

            if (invokeResult.Exception != null)
                throw new Exception.ExecutionException(invokeResult.Exception.Message, invokeResult.Exception.InnerException);
            // ----

            if (invokeResult.Result == null)
                return;

            switch (invokeResult.Result.Result)
            {
                case Basics.ControlResult.Conditional.Conditions.True:
                    if (string.IsNullOrEmpty(this._Contents.Parts[0]))
                        break;

                    this._SelectedContent = 0;
                    this.Parse();

                    break;
                case Basics.ControlResult.Conditional.Conditions.False:
                    if (this._Contents.Parts.Count < 2)
                        break;

                    if (string.IsNullOrEmpty(this._Contents.Parts[1]))
                        break;

                    this._SelectedContent = 1;
                    this.Parse();

                    break;
                case Basics.ControlResult.Conditional.Conditions.Unknown:
                    // Reserved For Future Uses

                    break;
            }

            if (this._SelectedContent > -1)
            {
                this._Children.Render(requesterUniqueId);
                this._Parent.Deliver(RenderStatus.Rendered, this._Parent.Result);
            }
        }
    }
}