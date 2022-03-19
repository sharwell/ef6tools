﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Design.Model.Entity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Xml.Linq;
    using Microsoft.Data.Entity.Design.Model.Commands;

    internal class NavigationProperty : PropertyBase
    {
        internal static readonly string ElementName = "NavigationProperty";
        internal const string AttributeRelationship = "Relationship";
        internal const string AttributeToRole = "ToRole";
        internal const string AttributeFromRole = "FromRole";

        private SingleItemBinding<Association> _relationshipBinding;
        private SingleItemBinding<AssociationEnd> _fromRoleBinding;
        private SingleItemBinding<AssociationEnd> _toRoleBinding;

        internal NavigationProperty(EntityType parent, XElement element)
            :
                this(parent, element, null)
        {
            // Nothing
        }

        /// <summary>
        ///     Create a Navigation property at a specified position.
        /// </summary>
        /// <param name="parent">Property's Parent. The value is either ConceptualEntityTYpe or a ComplexType.</param>
        /// <param name="element">Property's XElement</param>
        /// <param name="insertPostion">Information where the property should be inserted to. If the parameter is null, the property will be placed as the last property of the entity.</param>
        internal NavigationProperty(EntityType parent, XElement element, InsertPropertyPosition insertPostion)
            : base(parent, element, insertPostion)
        {
            // Nothing
        }

        internal SingleItemBinding<Association> Relationship
        {
            get
            {
                if (_relationshipBinding == null)
                {
                    _relationshipBinding = new SingleItemBinding<Association>(
                        this,
                        AttributeRelationship,
                        AssociationNameNormalizer.NameNormalizer);
                }
                return _relationshipBinding;
            }
        }

        internal SingleItemBinding<AssociationEnd> FromRole
        {
            get
            {
                if (_fromRoleBinding == null)
                {
                    _fromRoleBinding = new SingleItemBinding<AssociationEnd>(
                        this,
                        AttributeFromRole,
                        NavigationPropertyRoleNameNormalizer);
                }

                return _fromRoleBinding;
            }
        }

        internal SingleItemBinding<AssociationEnd> ToRole
        {
            get
            {
                if (_toRoleBinding == null)
                {
                    _toRoleBinding = new SingleItemBinding<AssociationEnd>(
                        this,
                        AttributeToRole,
                        NavigationPropertyRoleNameNormalizer);
                }
                return _toRoleBinding;
            }
        }

#if DEBUG
        internal override ICollection<string> MyAttributeNames()
        {
            var s = base.MyAttributeNames();
            s.Add(AttributeRelationship);
            s.Add(AttributeToRole);
            s.Add(AttributeFromRole);
            return s;
        }
#endif

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
                yield return Relationship;
                yield return FromRole;
                yield return ToRole;
            }
        }

        protected override void PreParse()
        {
            Debug.Assert(State != EFElementState.Parsed, "this object should not already be in the parsed state");

            ClearEFObject(_relationshipBinding);
            _relationshipBinding = null;
            ClearEFObject(_fromRoleBinding);
            _fromRoleBinding = null;
            ClearEFObject(_toRoleBinding);
            _toRoleBinding = null;

            base.PreParse();
        }

        protected override void DoNormalize()
        {
            var normalizedName = PropertyNameNormalizer.NameNormalizer(this, LocalName.Value);
            Debug.Assert(null != normalizedName, "Null NormalizedName for refName " + LocalName.Value);
            NormalizedName = (normalizedName != null ? normalizedName.Symbol : Symbol.EmptySymbol);
            base.DoNormalize();
        }

        protected override void DoResolve(EFArtifactSet artifactSet)
        {
            if (Relationship.RefName != null)
            {
                Relationship.Rebind();
            }

            if (ToRole.RefName != null)
            {
                ToRole.Rebind();
            }

            if (FromRole.RefName != null)
            {
                FromRole.Rebind();
            }

            if (FromRole.Status == BindingStatus.Known
                && ToRole.Status == BindingStatus.Known
                && Relationship.Status == BindingStatus.Known)
            {
                State = EFElementState.Resolved;
            }
        }

        internal static NormalizedName NavigationPropertyRoleNameNormalizer(EFElement parent, string refName)
        {
            var navProperty = parent as NavigationProperty;
            NormalizedName normalizedName = null;
            if (navProperty != null)
            {
                var symbol = new Symbol(navProperty.Relationship.NormalizedName(), refName);
                normalizedName = new NormalizedName(symbol, null, null, refName);
            }

            if (normalizedName == null)
            {
                var symbol = new Symbol(refName);
                normalizedName = new NormalizedName(symbol, null, null, refName);
            }

            return normalizedName;
        }

        internal override DeleteEFElementCommand GetDeleteCommand()
        {
            var cmd = new DeleteEFElementCommand(this);
            if (cmd == null)
            {
                throw new InvalidOperationException();
            }
            return cmd;
        }
    }
}
