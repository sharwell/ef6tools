// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Design.Model.Entity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.Data.Entity.Design.Common;
    using Microsoft.Data.Entity.Design.Model.Commands;

    internal class Association : DocumentableAnnotatableElement
    {
        internal static readonly string ElementName = "Association";

        private readonly List<AssociationEnd> _ends = new List<AssociationEnd>();
        private ReferentialConstraint _referentialConstraint;

        internal Association(EFElement parent, XElement element)
            : base(parent, element)
        {
        }

        internal void AddAssociationEnd(AssociationEnd end)
        {
            _ends.Add(end);
        }

        internal IList<AssociationEnd> AssociationEnds()
        {
            return _ends.AsReadOnly();
        }

        internal AssociationEnd End1
        {
            get { return _ends.FirstOrDefault(); }
        }

        internal AssociationEnd End2
        {
            get { return _ends.ElementAtOrDefault(1); }
        }

        internal ReferentialConstraint ReferentialConstraint
        {
            get { return _referentialConstraint; }
            set
            {
                if (_referentialConstraint != null
                    && _referentialConstraint != value)
                {
                    _referentialConstraint.Dispose();
                }
                _referentialConstraint = value;
            }
        }

        internal IEnumerable<Property> PrincipalRoleProperties
        {
            get
            {
                if (_referentialConstraint != null
                    && _referentialConstraint.Principal != null)
                {
                    return _referentialConstraint.Principal.PropertyRefs.
                        Select(propRef => propRef.Name.Target).
                        Where(prop => prop != null);
                }
                return new Property[] { };
            }
        }

        internal IEnumerable<Property> DependentRoleProperties
        {
            get
            {
                if (_referentialConstraint != null
                    && _referentialConstraint.Dependent != null)
                {
                    return _referentialConstraint.Dependent.PropertyRefs.
                        Select(propRef => propRef.Name.Target).
                        Where(prop => prop != null);
                }
                return new Property[] { };
            }
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
                foreach (var child in AssociationEnds())
                {
                    yield return child;
                }

                if (_referentialConstraint != null)
                {
                    yield return _referentialConstraint;
                }
            }
        }

        protected override void OnChildDeleted(EFContainer efContainer)
        {
            var child = efContainer as AssociationEnd;
            if (child != null)
            {
                _ends.Remove(child);
                return;
            }

            if (efContainer == _referentialConstraint)
            {
                _referentialConstraint = null;
                return;
            }

            base.OnChildDeleted(efContainer);
        }

#if DEBUG
        internal override ICollection<string> MyChildElementNames()
        {
            var s = base.MyChildElementNames();
            s.Add(AssociationEnd.ElementName);
            s.Add(ReferentialConstraint.ElementName);
            return s;
        }
#endif

        protected override void PreParse()
        {
            Debug.Assert(State != EFElementState.Parsed, "this object should not already be in the parsed state");

            ClearEFObjectCollection(_ends);
            ClearEFObject(_referentialConstraint);
            _referentialConstraint = null;

            base.PreParse();
        }

        internal override bool ParseSingleElement(ICollection<XName> unprocessedElements, XElement elem)
        {
            if (elem.Name.LocalName == AssociationEnd.ElementName)
            {
                var assocEnd = new AssociationEnd(this, elem);
                _ends.Add(assocEnd);
                assocEnd.Parse(unprocessedElements);
            }
            else if (elem.Name.LocalName == ReferentialConstraint.ElementName)
            {
                if (_referentialConstraint != null)
                {
                    var msg = String.Format(
                        CultureInfo.CurrentCulture, Resources.TOO_MANY_REFERENTIAL_CONSTRAINTS_IN_ASSOCIATION, LocalName.Value);
                    Artifact.AddParseErrorForObject(this, msg, ErrorCodes.TOO_MANY_REFERENTIAL_CONSTRAINTS_IN_ASSOCIATION);
                }
                else
                {
                    _referentialConstraint = new ReferentialConstraint(this, elem);
                    _referentialConstraint.Parse(unprocessedElements);
                }
            }
            else
            {
                return base.ParseSingleElement(unprocessedElements, elem);
            }
            return true;
        }

        protected override void DoNormalize()
        {
            var normalizedName = AssociationNameNormalizer.NameNormalizer(this, LocalName.Value);
            Debug.Assert(null != normalizedName, "Null NormalizedName for refName " + LocalName.Value);

            NormalizedName = (normalizedName != null ? normalizedName.Symbol : Symbol.EmptySymbol);
            base.DoNormalize();
        }

        /// <summary>
        ///     Returns the EntityModel that this Association is contained in.
        /// </summary>
        internal BaseEntityModel EntityModel
        {
            get
            {
                var baseModel = Parent as BaseEntityModel;
                Debug.Assert(baseModel != null, "EntityType.EntityModel: this.Parent should be a BaseEntityModel");
                return baseModel;
            }
        }

        /// <summary>
        ///     Returns the AssociationSet this Association belongs to.
        /// </summary>
        internal AssociationSet AssociationSet
        {
            get
            {
                AssociationSet associationSet = null;

                foreach (var es in GetAntiDependenciesOfType<AssociationSet>())
                {
                    associationSet = es;
                    break;
                }
                return associationSet;
            }
        }

        /// <summary>
        ///     Returns the AssociationSet this Association belongs to.
        ///     Throws if it null or unresolved, or is otherwise inconsistent
        /// </summary>
        internal AssociationSet SafeAssociationSet
        {
            get
            {
                var associationSet = AssociationSet;
                if (associationSet == null
                    || AssociationEnds().Count != 2
                    || (!ModelHelper.IsInConceptualModel(this) && ReferentialConstraint == null)
                    || (ReferentialConstraint != null
                        && (!PrincipalRoleProperties.Any() ||
                            PrincipalRoleProperties.Count() != DependentRoleProperties.Count())))
                {
                    ModelHelper.InvalidSchemaError(Resources.UnresolvedAssociationSet_0, NormalizedNameExternal);
                }
                return associationSet;
            }
        }

        internal bool IsManyToMany
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_Many, ModelConstants.Multiplicity_Many); }
        }

        internal bool IsOneToOne
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_One, ModelConstants.Multiplicity_One); }
        }

        internal bool IsZeroOrOneToZeroOrOne
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_ZeroOrOne, ModelConstants.Multiplicity_ZeroOrOne); }
        }

        internal bool IsOneToMany
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_One, ModelConstants.Multiplicity_Many); }
        }

        internal bool IsOneToZeroOrOne
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_One, ModelConstants.Multiplicity_ZeroOrOne); }
        }

        internal bool IsZeroOrOneToMany
        {
            get { return IsMultiplicity(ModelConstants.Multiplicity_ZeroOrOne, ModelConstants.Multiplicity_Many); }
        }

        internal bool IsSelfAssociation
        {
            get
            {
                return End1 != null
                       && End1.Type.Target != null
                       && End2 != null
                       && End1.Type.Target == End2.Type.Target;
            }
        }

        private bool IsMultiplicity(string multiplicity1, string multiplicity2)
        {
            Debug.Assert(AssociationEnds().Count <= 2, "Found more than two association ends for association");

            if (End1 != null
                && End2 != null)
            {
                if ((End1.Multiplicity.Value == multiplicity1 && End2.Multiplicity.Value == multiplicity2)
                    || End1.Multiplicity.Value == multiplicity2 && End2.Multiplicity.Value == multiplicity1)
                {
                    return true;
                }
            }
            return false;
        }

        #region static method

        /// <summary>
        ///     Get all associations which the entity type participates in.
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        internal static HashSet<Association> GetAssociationsForEntityType(EntityType entityType)
        {
            // Add association connectors
            var participatingAssociations = new HashSet<Association>();
            // First we find all the associations which the entitytype participates.
            foreach (var associationEnd in entityType.GetAntiDependenciesOfType<AssociationEnd>())
            {
                var association = associationEnd.Parent as Association;
                if (association != null
                    && participatingAssociations.Contains(association) == false)
                {
                    participatingAssociations.Add(association);
                }
            }
            return participatingAssociations;
        }

        #endregion

        internal AssociationEnd GetOtherEnd(AssociationEnd oneEnd)
        {
            return End1 == oneEnd ? End2 : End1;
        }

        /// <summary>
        ///     Gets the AssociationEnd that is not pointing to the given EntityType. This method should not be
        ///     called for self-associations.
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        internal AssociationEnd GetOtherEnd(EntityType entityType)
        {
            Debug.Assert(
                !IsSelfAssociation,
                "GetOtherEnd() will not work correctly for self associations as both ends point to the same entity type");

            AssociationEnd otherEnd = null;
            if (End1.Type.Target == entityType)
            {
                otherEnd = End2;
            }
            else if (End2.Type.Target == entityType)
            {
                otherEnd = End1;
            }

            return otherEnd;
        }

        internal override DeleteEFElementCommand GetDeleteCommand()
        {
            var cmd = new DeleteAssociationCommand(this);
            Debug.Assert(cmd != null, "Could not create DeleteAssociationCommand, falling back to base class delete.");
            if (cmd == null)
            {
                // shouldn't happen, just to be safe
                return base.GetDeleteCommand();
            }
            return cmd;
        }
    }
}
