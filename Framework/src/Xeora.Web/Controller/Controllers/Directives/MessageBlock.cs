﻿namespace Xeora.Web.Controller.Directive
{
    public class MessageBlock : DirectiveWithChildren
    {
        public MessageBlock(int rawStartIndex, string rawValue, Global.ArgumentInfoCollection contentArguments) :
            base(rawStartIndex, rawValue, DirectiveTypes.MessageBlock, contentArguments)
        { }

        public override void Render(string requesterUniqueID)
        {
            Global.ContentDescription contentDescription = 
                new Global.ContentDescription(this.Value);

            string blockContent = contentDescription.Parts[0];

            this.Parse(blockContent);

            if (this.Mother.MessageResult != null)
            {
                this.ContentArguments.AppendKeyWithValue("MessageType", this.Mother.MessageResult.Type);
                this.ContentArguments.AppendKeyWithValue("Message", this.Mother.MessageResult.Content);
            }

            base.Render(requesterUniqueID);
        }
    }
}