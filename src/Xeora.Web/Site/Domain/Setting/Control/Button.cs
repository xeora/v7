﻿using Xeora.Web.Basics.Domain.Control;
using Xeora.Web.Basics.Domain.Control.Definitions;
using Xeora.Web.Basics.Execution;

namespace Xeora.Web.Site.Setting.Control
{
    public class Button : Base, IButton
    {
        public Button(Bind bind, SecurityDefinition security, string text, Updates updates, AttributeCollection attributes) :
            base(ControlTypes.Button, bind, security)
        {
            this.Text = text;
            this.Updates = updates;
            this.Attributes = attributes;
        }

        public string Text { get; }
        public Updates Updates { get; }
        public AttributeCollection Attributes { get; }

        public override IBase Clone()
        {
            Bind bind = null;

            if (base.Bind != null)
                base.Bind.Clone(out bind);

            SecurityDefinition security = null;

            if (base.Security != null)
                base.Security.Clone(out security);

            return new Button(bind, security, this.Text, this.Updates.Clone(), this.Attributes.Clone());
        }
    }
}