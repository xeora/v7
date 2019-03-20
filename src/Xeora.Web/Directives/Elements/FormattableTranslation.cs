﻿using System.Text.RegularExpressions;
using Xeora.Web.Basics.Domain;
using Xeora.Web.Global;

namespace Xeora.Web.Directives.Elements
{
    public class FormattableTranslation : Directive, INamable, IHasChildren
    {
        private readonly ContentDescription _Contents;
        private DirectiveCollection _Children;
        private bool _Parsed;

        public FormattableTranslation(string rawValue, ArgumentCollection arguments) :
            base(DirectiveTypes.FormattableTranslation, arguments)
        {
            this.DirectiveID = DirectiveHelper.CaptureDirectiveID(rawValue);
            this._Contents = new ContentDescription(rawValue);
        }

        public string DirectiveID { get; private set; }

        public override bool Searchable => false;
        public override bool CanAsync => false;

        public DirectiveCollection Children => this._Children;

        public override void Parse()
        {
            if (this._Parsed)
                return;
            this._Parsed = true;

            this._Children = new DirectiveCollection(this.Mother, this);

            // FormattableTranslation does not have any ContentArguments, That's why it copies it's parent Arguments
            if (this.Parent != null)
                this.Arguments.Replace(this.Parent.Arguments);

            string formatContent = this._Contents.Parts[0];
            if (string.IsNullOrEmpty(formatContent))
                throw new Exception.EmptyBlockException();

            this.Mother.RequestParsing(formatContent, ref this._Children, this.Arguments);
        }

        private static Regex _FormatIndexRegEx =
            new Regex("\\{(?<index>\\d+)\\}", RegexOptions.Compiled);
        public override void Render(string requesterUniqueID)
        {
            this.Parse();

            if (this.Status != RenderStatus.None)
                return;
            this.Status = RenderStatus.Rendering;

            this.Children.Render(this.UniqueID);

            IDomain instance = null;
            this.Mother.RequestInstance(ref instance);

            string translationValue =
                instance.Languages.Current.Get(this.DirectiveID);
            string[] parameters = this.Result.Split('|');

            MatchCollection matches =
                FormattableTranslation._FormatIndexRegEx.Matches(translationValue);

            for (int c = matches.Count - 1; c >= 0; c--)
            {
                Match current = matches[c];
                int formatIndex =
                    int.Parse(current.Groups["index"].Value);

                if (formatIndex >= parameters.Length)
                    throw new Exception.FormatIndexOutOfRangeException();

                translationValue =
                    translationValue.Remove(current.Index, current.Length);
                translationValue =
                    translationValue.Insert(current.Index, parameters[formatIndex]);
            }

            this.Deliver(RenderStatus.Rendered, translationValue);
            this.Mother.Pool.Register(this);
            this.Mother.Scheduler.Fire(this.DirectiveID);
        }
    }
}
