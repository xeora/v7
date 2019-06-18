﻿using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Xeora.Web.Global
{
    public class ContentDescription
    {
        private class PartCache
        {
            public string Content { get; set; }
            public List<string> Parts { get; set; }
            public string MessageTemplate { get; set; }
        }

        private static ConcurrentDictionary<string, PartCache> _PartsCache = 
            new ConcurrentDictionary<string, PartCache>();

        private const string MESSAGE_TEMPLATE_POINTER_TEXT = "!MESSAGETEMPLATE";

        public ContentDescription(string rawValue)
        {
            this.Parts = new List<string>();
            this.MessageTemplate = string.Empty;

            // Parse Block Content
            int firstContentIndex =
                rawValue.IndexOf(":{", System.StringComparison.InvariantCulture);

            if (firstContentIndex == -1)
                return;

            string directiveIdentifier =
                rawValue.Substring(0, firstContentIndex);

            int colonIndex = 
                directiveIdentifier.IndexOf(':');
            bool isSpecialDirective = false;
            string blockContent;

            if (colonIndex == -1)
            {
                // Special Directive such as PC, MB, XF, AG
                blockContent = rawValue;
                isSpecialDirective = true;
            }
            else
            {
                // Common Directive such as DirectiveType:DirectiveID
                blockContent =
                    rawValue.Substring(colonIndex + 1);
            }

            // Cleanup if there is parameters
            int parameterBeginIndex = blockContent.IndexOf('(');
            int parameterEndIndex = blockContent.IndexOf('~');
            if (parameterBeginIndex > -1 && parameterEndIndex > -1 && 
                parameterEndIndex > parameterBeginIndex && 
                parameterEndIndex < firstContentIndex)
                blockContent = blockContent.Remove(parameterBeginIndex, parameterEndIndex - parameterBeginIndex);

            // Update First Content Index
            firstContentIndex = 
                blockContent.IndexOf(":{", System.StringComparison.InvariantCulture);

            // ControlIDWithIndex is Like ControlID~INDEX
            string controlIDWithIndex = 
                blockContent.Substring(0, firstContentIndex);

            string openingTag = string.Format("{0}:{{", controlIDWithIndex);
            string closingTag = string.Format("}}:{0}", controlIDWithIndex);

            int idxCoreContStart =
                blockContent.IndexOf(openingTag, System.StringComparison.InvariantCulture) + openingTag.Length;
            int idxCoreContEnd =
                blockContent.LastIndexOf(closingTag, blockContent.Length, System.StringComparison.InvariantCulture);

            if (idxCoreContStart != openingTag.Length || 
                idxCoreContEnd != (blockContent.Length - openingTag.Length))
                throw new Exception.ParseException();

            string coreContent = 
                blockContent.Substring(idxCoreContStart, idxCoreContEnd - idxCoreContStart);
            if (isSpecialDirective)
                coreContent = coreContent.Trim();

            if (ContentDescription._PartsCache.TryGetValue(controlIDWithIndex, out PartCache partCache))
            {
                if (string.Compare(partCache.Content, coreContent) == 0)
                {
                    this.Parts = partCache.Parts;
                    this.MessageTemplate = partCache.MessageTemplate;

                    return;
                }
            }

            this.PrepareDesciption(coreContent, controlIDWithIndex, isSpecialDirective);
        }

        private void PrepareDesciption(string content, string controlIDWithIndex, bool isSpecialDirective)
        {
            string searchString =
                string.Format("}}:{0}:{{", controlIDWithIndex);
            string contentPart;
            int sIdx, cIdx = 0;

            do
            {
                sIdx = content.IndexOf(searchString, cIdx, System.StringComparison.InvariantCulture);

                if (sIdx > -1)
                {
                    contentPart = content.Substring(cIdx, sIdx - cIdx);

                    // Set cIdx and Move Forward to Length of SearchString
                    cIdx = sIdx + searchString.Length;
                }
                else
                    contentPart = content.Substring(cIdx);

                if (isSpecialDirective)
                    contentPart = contentPart.Trim();

                if (contentPart.IndexOf(ContentDescription.MESSAGE_TEMPLATE_POINTER_TEXT, System.StringComparison.InvariantCulture) == 0)
                {
                    if (!string.IsNullOrEmpty(this.MessageTemplate))
                        throw new Exception.MultipleBlockException("Only One Message Template Block Allowed!");

                    this.MessageTemplate = contentPart.Substring(ContentDescription.MESSAGE_TEMPLATE_POINTER_TEXT.Length);
                }
                else
                    if (!string.IsNullOrEmpty(contentPart))
                        this.Parts.Add(contentPart);
            } while (sIdx != -1);

            if (!this.HasParts)
                throw new Exception.EmptyBlockException();

            // Cache Result
            PartCache partCache = new PartCache
            {
                Content = content,
                Parts = this.Parts,
                MessageTemplate = this.MessageTemplate
            };

            ContentDescription._PartsCache.TryAdd(controlIDWithIndex, partCache);
        }

        public List<string> Parts { get; private set; }
        public bool HasParts => this.Parts.Count > 0;
        public string MessageTemplate { get; private set; }
        public bool HasMessageTemplate => !string.IsNullOrEmpty(this.MessageTemplate);
    }
}