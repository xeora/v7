﻿using System.Collections.Generic;
using System.IO;
using Xeora.Web.Basics;
using Xeora.Web.Global;

namespace Xeora.Web.Directives.Elements
{
    public class InLineStatement : Directive, INamable, IBoundable
    {
        private readonly ContentDescription _Contents;
        private DirectiveCollection _Children;
        private bool _Parsed;

        private bool _Cache;
        private string _ParametersDefinition;

        public InLineStatement(string rawValue, ArgumentCollection arguments) :
            base(DirectiveTypes.InLineStatement, arguments)
        {
            this.DirectiveID = DirectiveHelper.CaptureDirectiveID(rawValue);
            this.BoundDirectiveID = DirectiveHelper.CaptureBoundDirectiveID(rawValue);

            this._Contents = new ContentDescription(rawValue);
            this._Cache = true;
            this._ParametersDefinition = null;
        }

        public string DirectiveID { get; private set; }
        public string BoundDirectiveID { get; private set; }
        public bool HasBound => !string.IsNullOrEmpty(this.BoundDirectiveID);

        public override bool Searchable => false;
        public override bool CanAsync => false;

        public DirectiveCollection Children => this._Children;

        public override void Parse()
        {
            if (this._Parsed)
                return;
            this._Parsed = true;

            this._Children = new DirectiveCollection(this.Mother, this);

            // InLineStatement needs to link ContentArguments of its parent.
            if (this.Parent != null)
                this.Arguments.Replace(this.Parent.Arguments);

            string statementContent = this._Contents.Parts[0];
            if (string.IsNullOrEmpty(statementContent))
                throw new Exception.EmptyBlockException();

            this.Mother.RequestParsing(statementContent, ref this._Children, this.Arguments);
        }

        public override void Render(string requesterUniqueID)
        {
            this.Parse();

            string uniqueID =
                string.IsNullOrEmpty(requesterUniqueID) ? this.UniqueID : requesterUniqueID;

            if (this.HasBound)
            {
                if (string.IsNullOrEmpty(requesterUniqueID))
                    return;

                this.Mother.Pool.GetByDirectiveID(this.BoundDirectiveID, out IDirective[] directives);

                if (directives == null) return;

                foreach (IDirective directive in directives)
                {
                    if (!(directive is INamable)) return;

                    string directiveID = ((INamable)directive).DirectiveID;
                    if (string.Compare(directiveID, this.BoundDirectiveID) != 0) return;

                    if (directive.Status != RenderStatus.Rendered)
                    {
                        directive.Scheduler.Register(this.UniqueID);
                        return;
                    }
                }
            }

            if (this.Status != RenderStatus.None)
                return;
            this.Status = RenderStatus.Rendering;

            this.Children.Render(this.UniqueID);
            this.ExecuteStatement(uniqueID);

            this.Scheduler.Fire();
        }

        private void ExecuteStatement(string requesterUniqueID)
        {
            Basics.Domain.IDomain instance = null;
            this.Mother.RequestInstance(ref instance);

            string result = this.Result;

            this.ExtractSubDirectives(ref result);

            if (!this._Cache && string.IsNullOrEmpty(result))
                throw new Exception.EmptyBlockException();

            object methodResultInfo =
                Manager.AssemblyCore.ExecuteStatement(instance.IDAccessTree, this.DirectiveID, result, this.RenderParameters(requesterUniqueID), this._Cache);

            if (methodResultInfo != null && methodResultInfo is System.Exception)
                throw new Exception.ExecutionException(((System.Exception)methodResultInfo).Message, ((System.Exception)methodResultInfo).InnerException);

            if (methodResultInfo != null)
            {
                string renderResult = string.Empty;

                if (methodResultInfo is Basics.ControlResult.RedirectOrder)
                    Helpers.Context.AddOrUpdate("RedirectLocation", ((Basics.ControlResult.RedirectOrder)methodResultInfo).Location);
                else
                    renderResult = Manager.AssemblyCore.GetPrimitiveValue(methodResultInfo);

                this.Deliver(RenderStatus.Rendered, renderResult);

                return;
            }

            this.Deliver(RenderStatus.Rendered, string.Empty);
        }

        private void ExtractSubDirectives(ref string blockContent)
        {
            Dictionary<string, System.Func<string, string>> subDirectives =
                new Dictionary<string, System.Func<string, string>>() {
                    {
                        "!NOCACHE",
                        new System.Func<string, string>(
                            (d) =>
                            {
                                this._Cache = false;
                                return d.Replace("!NOCACHE", string.Empty);
                            }
                        )
                    },
                    {
                        "!PARAMS",
                        new System.Func<string, string>(
                            (d) =>
                            {
                                this._ParametersDefinition = this.ParseParameters(ref d);
                                return d;
                            }
                        )
                    }
                };

            // Sub Directive Test
            if (blockContent.IndexOf('!') == 0)
            {
                string directives = string.Empty;
                StringReader sR = null;
                try
                {
                    sR = new StringReader(blockContent);
                    directives = sR.ReadLine();

                    blockContent = sR.ReadToEnd();
                }
                catch (Exception.GrammerException)
                {
                    throw;
                }
                catch (System.Exception)
                {
                    // Just Handle Exceptions
                }
                finally
                {
                    if (sR != null)
                        sR.Close();
                }

                foreach (string key in subDirectives.Keys)
                {
                    int dIdx = directives.IndexOf(key, System.StringComparison.InvariantCulture);

                    if (dIdx == -1)
                        continue;

                    directives = subDirectives[key].Invoke(directives);
                }
            }
            blockContent = blockContent.Trim();
        }

        private string ParseParameters(ref string directives)
        {
            string paramMarker = "!PARAMS(";

            int openBracketIdx = directives.IndexOf(paramMarker, System.StringComparison.InvariantCulture);
            if (openBracketIdx == -1)
                return null;

            int closeBracketIdx = directives.LastIndexOf(")", System.StringComparison.InvariantCulture);
            if (closeBracketIdx == -1)
                throw new Exception.GrammerException();
            closeBracketIdx++;

            string paramDefinition =
                directives.Substring(openBracketIdx, closeBracketIdx - openBracketIdx);

            directives = directives.Replace(paramDefinition, string.Empty);

            return paramDefinition.Substring(8, paramDefinition.Length - 9);
        }

        public object[] RenderParameters(string requesterUniqueID)
        {
            if (string.IsNullOrEmpty(this._ParametersDefinition))
                return null;

            List<object> parameters = new List<object>();

            string[] paramDefs = this._ParametersDefinition.Split('|');

            foreach (string paramDef in paramDefs)
                parameters.Add(
                    DirectiveHelper.RenderProperty(this, paramDef, this.Arguments, requesterUniqueID));

            return parameters.ToArray();
        }
    }
}