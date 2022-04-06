﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Design.Model.Entity
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Xml.Linq;

    /// <summary>
    ///     Used as base class for the OnDelete, OnCopy etc actions
    /// </summary>
    internal abstract class ActionBase : EFElement
    {
        internal static readonly string AttributeAction = "Action";
        private DefaultableValue<string> _actionAttr;

        protected ActionBase(EFElement parent, XElement element)
            : base(parent, element)
        {
        }

        /// <summary>
        ///     Manages the content of the Action attribute
        /// </summary>
        internal DefaultableValue<string> Action
        {
            get
            {
                if (_actionAttr == null)
                {
                    _actionAttr = new ActionDefaultableValue(this);
                }
                return _actionAttr;
            }
        }

        private class ActionDefaultableValue : DefaultableValue<string>
        {
            internal ActionDefaultableValue(EFElement parent)
                : base(parent, AttributeAction)
            {
            }

            internal override string AttributeName
            {
                get { return AttributeAction; }
            }

            public override string DefaultValue
            {
                get { return string.Empty; }
            }
        }

#if DEBUG
        internal override ICollection<string> MyAttributeNames()
        {
            var s = base.MyAttributeNames();
            s.Add(AttributeAction);
            return s;
        }
#endif

        protected override void PreParse()
        {
            Debug.Assert(State != EFElementState.Parsed, "this object should not already be in the parsed state");

            ClearEFObject(_actionAttr);
            _actionAttr = null;

            base.PreParse();
        }

        internal override bool ParseSingleElement(ICollection<XName> unprocessedElements, XElement elem)
        {
            return false;
        }

        // we unfortunately get a warning from the compiler when we use the "base" keyword in "iterator" types generated by using the
        // "yield return" keyword.  By adding this method, I was able to get around this.  Unfortunately, I wasn't able to figure out
        // a way to implement this once and have derived classes share the implementation (since the "base" keyword is resolved at 
        // compile-time and not at runtime.
        private IEnumerable<EFObject> BaseChildren
        {
            get { return base.Children; }
        }

        internal override IEnumerable<EFObject> Children
        {
            get
            {
                foreach (var efobj in BaseChildren)
                {
                    yield return efobj;
                }
                yield return Action;
            }
        }
    }
}
