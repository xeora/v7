﻿using Xeora.Web.Basics;
using Xeora.Web.Global;

namespace Xeora.Web.Directives.Elements
{
    public class Execution : Directive, ILevelable, IBoundable
    {
        private readonly string _RawValue;
        private bool _Rendered;
        private bool _Queued;

        public Execution(string rawValue, ArgumentCollection arguments) :
            base(DirectiveTypes.Execution, arguments)
        {
            this._RawValue = rawValue;

            this.Leveling = LevelingInfo.Create(rawValue);
            this.BoundDirectiveID = DirectiveHelper.CaptureBoundDirectiveID(rawValue);
        }

        public LevelingInfo Leveling { get; private set; }
        public string BoundDirectiveID { get; private set; }
        public bool HasBound => !string.IsNullOrEmpty(this.BoundDirectiveID);

        public override bool Searchable => false;
        public override bool Rendered => this._Rendered;

        public override void Parse()
        { }

        public override void Render(string requesterUniqueID)
        {
            this.Parse();

            string uniqueID = this.UniqueID;

            if (this.HasBound)
            {
                if (string.IsNullOrEmpty(requesterUniqueID))
                    return;

                this.Mother.Pool.GetInto(requesterUniqueID, out IDirective directive);

                if (directive == null ||
                    (directive is INamable &&
                        string.Compare(((INamable)directive).DirectiveID, this.BoundDirectiveID) != 0)
                    )
                {
                    if (!this._Queued)
                    {
                        this._Queued = true;
                        this.Mother.Scheduler.Register(this.BoundDirectiveID, this.UniqueID);
                    }

                    return;
                }

                uniqueID = requesterUniqueID;
            }

            if (this._Rendered)
                return;
            this._Rendered = true;

            this.ExecuteBind(uniqueID);
        }

        private void ExecuteBind(string requesterUniqueID)
        {
            string[] controlValueSplitted = 
                this._RawValue.Split(':');

            // Call Related Function and Exam It
            IDirective leveledDirective = this;
            int level = this.Leveling.Level;

            do
            {
                if (level == 0)
                    break;

                leveledDirective = leveledDirective.Parent;

                if (leveledDirective is Renderless)
                    leveledDirective = leveledDirective.Parent;

                level -= 1;
            } while (leveledDirective != null);

            Basics.Execution.Bind bind =
                Basics.Execution.Bind.Make(string.Join(":", controlValueSplitted, 1, controlValueSplitted.Length - 1));

            // Execution preparation should be done at the same level with it's parent. Because of that, send parent as parameters
            bind.Parameters.Prepare(
                (parameter) => DirectiveHelper.RenderProperty(leveledDirective.Parent, parameter.Query, leveledDirective.Parent.Arguments, requesterUniqueID)
            );

            Basics.Execution.InvokeResult<object> invokeResult =
                Manager.AssemblyCore.InvokeBind<object>(Helpers.Context.Request.Header.Method, bind, Manager.ExecuterTypes.Other);

            if (invokeResult.Exception != null)
                throw new Exception.ExecutionException(invokeResult.Exception.Message, invokeResult.Exception.InnerException);

            if (invokeResult.Result != null && invokeResult.Result is Basics.ControlResult.RedirectOrder)
            {
                Helpers.Context.AddOrUpdate("RedirectLocation",
                    ((Basics.ControlResult.RedirectOrder)invokeResult.Result).Location);

                this.Result = string.Empty;

                return;
            }

            this.Result = Manager.AssemblyCore.GetPrimitiveValue(invokeResult.Result);
        }
    }
}
