﻿using System;
using Xeora.Web.Basics;

namespace Xeora.Web.Controller.Directive.Control
{
    public class Checkbox : Control, IHasText, IUpdateBlocks
    {
        public Checkbox(int rawStartIndex, string rawValue, Global.ArgumentInfoCollection contentArguments, ControlSettings settings) :
            base(rawStartIndex, rawValue, contentArguments, settings)
        {
            this.Text = settings.Text;
            this.UpdateLocalBlock = settings.UpdateLocalBlock;
            this.BlockIDsToUpdate = settings.BlockIDsToUpdate;
        }

        public string Text { get; private set; }
        public bool UpdateLocalBlock { get; private set; }
        public string[] BlockIDsToUpdate { get; private set; }

        public override IControl Clone() =>
            new Checkbox(this.RawStartIndex, this.RawValue, this.ContentArguments, this.Settings);

        protected override void RenderControl(string requesterUniqueID)
        {
            this.BlockIDsToUpdate = base.FixBlockIDs(this);

            // Checkbox Control does not have any ContentArguments, That's why it copies it's parent Arguments
            if (this.Parent != null)
                this.ContentArguments.Replace(this.Parent.ContentArguments);

            // Render Text Content
            this.Text = ControllerHelper.RenderSingleContent(this.Text, this, this.ContentArguments, requesterUniqueID);

            string itemIndex = Convert.ToString(this.ContentArguments["_sys_ItemIndex"]);
            string checkBoxID = this.ControlID;

            if (itemIndex != null)
                checkBoxID = string.Format("{0}_{1}", this.ControlID, itemIndex);
            string checkBoxLabel = 
                string.Format("<label for=\"{0}\">{1}</label>", checkBoxID, this.Text);

            // Render Bind Parameters
            this.Bind = ControllerHelper.RenderBind(this.Bind, this, this.ContentArguments, requesterUniqueID);

            // Define OnClick Server event for Button
            if (this.Bind != null)
            {
                string xeoraCall;

                if (this.IsUpdateBlockController)
                {
                    xeoraCall = string.Format(
                        "__XeoraJS.update('{1}', '{0}')",
                        Manager.AssemblyCore.EncodeFunction(
                            Helpers.Context.HashCode,
                            this.Bind.ToString()
                        ),
                        string.Join(",", this.BlockIDsToUpdate)
                    );
                }
                else
                    xeoraCall = string.Format(
                            "__XeoraJS.post('{0}')",
                            Manager.AssemblyCore.EncodeFunction(
                                Helpers.Context.HashCode,
                                this.Bind.ToString()
                            )
                        );

                if (string.IsNullOrEmpty(this.Attributes["onclick"]))
                    this.Attributes["onclick"] = string.Format("javascript:{0};", xeoraCall);
                else
                {
                    this.Attributes["onclick"] = base.CleanJavascriptSignature(this.Attributes["onclick"]);

                    this.Attributes["onclick"] =
                        string.Format(
                            "javascript:try{{{0};{1};}}catch(ex){{}};",
                            this.Attributes["onclick"], xeoraCall
                        );
                }

            }
            // !--

            // Render Attributes
            for (int aC = 0; aC < this.Attributes.Count; aC++)
            {
                AttributeDefinition item = this.Attributes[aC];

                this.Attributes[aC] =
                        new AttributeDefinition(
                        item.Key,
                        ControllerHelper.RenderSingleContent(item.Value, this, this.ContentArguments, requesterUniqueID)
                    );
            }
            // !--

            if (this.Security.Disabled.Set && this.Security.Disabled.Type == SecurityDefinition.DisabledDefinition.Types.Dynamic)
                this.RenderedValue = this.Security.Disabled.Value;
            else
            {
                this.RenderedValue =
                    string.Format(
                        "<input type=\"checkbox\" name=\"{0}\" id=\"{1}\"{2}>{3}",
                        this.ControlID,
                        checkBoxID,
                        this.Attributes.ToString(),
                        checkBoxLabel
                    );
            }

            this.Mother.Scheduler.Fire(this.ControlID);
        }
    }
}