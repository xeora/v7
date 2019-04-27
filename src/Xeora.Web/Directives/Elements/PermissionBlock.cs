﻿using Xeora.Web.Basics;
using Xeora.Web.Basics.Domain;
using Xeora.Web.Global;

namespace Xeora.Web.Directives.Elements
{
    public class PermissionBlock : Directive, INamable, IHasChildren
    {
        private enum ContentTypes
        {
            None,
            Allowed,
            Forbidden
        }

        private ContentTypes _SelectedContent = ContentTypes.None;

        private readonly ContentDescription _Contents;
        private DirectiveCollection _Children;
        private bool _Parsed;

        public PermissionBlock(string rawValue, ArgumentCollection arguments) :
            base(DirectiveTypes.PermissionBlock, arguments)
        {
            this.DirectiveID = DirectiveHelper.CaptureDirectiveID(rawValue);
            this._Contents = new ContentDescription(rawValue);
        }

        public string DirectiveID { get; private set; }

        public override bool Searchable => true;
        public override bool CanAsync => false;

        public DirectiveCollection Children => this._Children;

        public override void Parse()
        {
            if (this._SelectedContent == ContentTypes.None)
                return;

            if (this._Parsed)
                return;
            this._Parsed = true;

            this._Children = new DirectiveCollection(this.Mother, this);

            string statementContent = string.Empty;
            switch (this._SelectedContent)
            {
                case ContentTypes.Allowed:
                    statementContent = this._Contents.Parts[0];

                    break;
                case ContentTypes.Forbidden:
                    statementContent = this._Contents.MessageTemplate;

                    break;
            }

            if (string.IsNullOrEmpty(statementContent))
                return;

            // PermissionBlock does not have any ContentArguments, That's why it copies it's parent Arguments
            if (this.Parent != null)
                this.Arguments.Replace(this.Parent.Arguments);

            this.Mother.RequestParsing(statementContent, ref this._Children, this.Arguments);
        }

        public override void Render(string requesterUniqueID)
        {
            if (this.Status != RenderStatus.None)
                return;
            this.Status = RenderStatus.Rendering;

            PermissionResult permissionResult = 
                this.EnsurePermission();

            this._SelectedContent = 
                permissionResult.Result == PermissionResult.Results.Allowed ? ContentTypes.Allowed : ContentTypes.Forbidden;
            this.Parse();

            this.Children.Render(this.UniqueID);

            this.Mother.Pool.Register(this);
            this.Mother.Scheduler.Fire(this.DirectiveID);
        }

        private PermissionResult EnsurePermission()
        {
            IDomain instance = null;
            this.Mother.RequestInstance(ref instance);

            if (string.IsNullOrEmpty(instance.Settings.Configurations.SecurityExecutable))
                return new PermissionResult(PermissionResult.Results.Forbidden);

            Basics.Execution.Bind permissionBind =
                Basics.Execution.Bind.Make(string.Format("{0}?EnsurePermission,p1", instance.Settings.Configurations.SecurityExecutable));
            permissionBind.Parameters.Prepare(parameter => this.DirectiveID);
            permissionBind.InstanceExecution = true;

            Basics.Execution.InvokeResult<PermissionResult> permissionInvokeResult =
                Manager.AssemblyCore.InvokeBind<PermissionResult>(Helpers.Context.Request.Header.Method, permissionBind, Manager.ExecuterTypes.Undefined);

            if (permissionInvokeResult.Result == null || permissionInvokeResult.Exception != null)
                return new PermissionResult(PermissionResult.Results.Forbidden);

            return permissionInvokeResult.Result;
        }
    }
}