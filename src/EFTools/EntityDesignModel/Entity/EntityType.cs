﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.Data.Entity.Design.Model.Entity
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Xml.Linq;
    using Microsoft.Data.Entity.Design.Common;
    using Microsoft.Data.Entity.Design.Model.Commands;
    using Microsoft.Data.Entity.Design.Model.Mapping;

    internal abstract class EntityType : DocumentableAnnotatableElement
    {
        internal static readonly string ElementName = "EntityType";

        private Key _key;
        private readonly List<Property> _properties = new List<Property>();

        internal EntityType(BaseEntityModel model, XElement element)
            : base(model, element)
        {
        }

        internal override string EFTypeName
        {
            get { return ElementName; }
        }

        internal Key Key
        {
            get { return _key; }
            set { _key = value; }
        }

        internal IList<Property> ResolvableKeys
        {
            get
            {
                var keys = new List<Property>();

                if (_key != null)
                {
                    foreach (var keyRef in _key.PropertyRefs)
                    {
                        if (keyRef.Name.Target != null)
                        {
                            keys.Add(keyRef.Name.Target);
                        }
                    }
                }

                return keys;
            }
        }

        internal string GetKeysAsString()
        {
            if (_key == null)
            {
                return String.Empty;
            }
            return _key.GetPropertyRefsAsString();
        }

#if false
    /// <summary>
    /// Returns (declared or inherited) key properties of this type.
    /// Throws if any property references are unresolved.
    /// </summary>
        internal IEnumerable<Property> SafeKeyProperties
        {
            get
            {
                foreach (PropertyRef propertyRef in SafeRootType.Key.PropertyRefs)
                {
                    yield return propertyRef.Name.SafeTarget;
                }
            }
        }
#endif

        internal void AddProperty(Property prop)
        {
            _properties.Add(prop);
        }

        /// <summary>
        ///     Declared properties of the type (no inherited properties)
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<Property> Properties()
        {
            foreach (var p in _properties)
            {
                yield return p;
            }
        }

        internal int PropertyCount
        {
            get { return _properties.Count; }
        }

        /// <summary>
        ///     Returns the entity set containing this type.
        ///     Throws if entity set is null or reference is unresolved.
        /// </summary>
        internal EntitySet SafeEntitySet
        {
            get
            {
                var entitySet = EntitySet;
                if (entitySet == null)
                {
                    ModelHelper.InvalidSchemaError(Resources.UnresolvedEntitySet_0, NormalizedNameExternal);
                }
                return entitySet;
            }
        }

        internal BaseEntityModel EntityModel
        {
            get
            {
                var baseModel = Parent as BaseEntityModel;
                Debug.Assert(baseModel != null, "this.Parent should be a BaseEntityModel");
                return baseModel;
            }
        }

        internal virtual EntitySet EntitySet
        {
            get
            {
                EntitySet entitySet = null;

                foreach (var es in GetAntiDependenciesOfType<EntitySet>())
                {
                    entitySet = es;
                    break;
                }
                return entitySet;
            }
        }

        /// <summary>
        ///     returns an IEnumerable of all EntitySets for this EntityType, or one of this EntityType's base types
        ///     Currently, Escher only supports a single entity-set per type, so the presence of more than one of these
        ///     indicates an error condition.
        /// </summary>
        internal virtual IEnumerable<EntitySet> AllEntitySets
        {
            get
            {
                var entitySets = new HashSet<EntitySet>();

                foreach (var es in GetAntiDependenciesOfType<EntitySet>())
                {
                    entitySets.Add(es);
                }

                return entitySets;
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

                foreach (var child in Properties())
                {
                    yield return child;
                }

                if (_key != null)
                {
                    yield return _key;
                }
            }
        }

        protected override void OnChildDeleted(EFContainer efContainer)
        {
            var child1 = efContainer as Property;
            if (child1 != null)
            {
                _properties.Remove(child1);
                return;
            }

            var child3 = efContainer as Key;
            if (child3 != null)
            {
                _key = null;
                return;
            }

            base.OnChildDeleted(efContainer);
        }

#if DEBUG
        internal override ICollection<string> MyChildElementNames()
        {
            var s = base.MyChildElementNames();
            s.Add(Property.ElementName);
            s.Add(Key.ElementName);
            return s;
        }
#endif

        protected override void PreParse()
        {
            Debug.Assert(State != EFElementState.Parsed, "this object should not already be in the parsed state");

            ClearEFObjectCollection(_properties);
            ClearEFObject(_key);
            _key = null;
            base.PreParse();
        }

        internal override bool ParseSingleElement(ICollection<XName> unprocessedElements, XElement elem)
        {
            if (elem.Name.LocalName == Key.ElementName)
            {
                if (_key != null)
                {
                    // multiple key elements
                    Artifact.AddParseErrorForObject(this, Resources.TOO_MANY_KEY_ELEMENTS, ErrorCodes.TOO_MANY_KEY_ELEMENTS);
                }
                else
                {
                    _key = new Key(this, elem);
                    _key.Parse(unprocessedElements);
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
            if (Parent is BaseEntityModel)
            {
                var normalizedName = EntityTypeNameNormalizer.NameNormalizer(this, LocalName.Value);
                Debug.Assert(null != normalizedName, "Null NormalizedName for refName " + LocalName.Value);
                NormalizedName = (normalizedName != null ? normalizedName.Symbol : Symbol.EmptySymbol);
                base.DoNormalize();
            }
        }

        internal override DeleteEFElementCommand GetDeleteCommand()
        {
            DeleteEFElementCommand cmd = new DeleteEntityTypeCommand(this);
            if (cmd == null)
            {
                // shouldn't happen, just to be safe
                throw new InvalidOperationException();
            }
            return cmd;
        }

        /// <summary>
        ///     Normally we will just the general implementation for this.  The exception is when we
        ///     are trying to bind to this from the TypeName attribute of an EntityTypeMapping.  In this
        ///     case, we need to return IsTypeOf(typeName) if the ETM needs it that way.
        ///     Simillary we need to return "Collection(typeName)" string for the ReturnType attribute of a FunctionImport.
        /// </summary>
        /// <param name="binding"></param>
        /// <returns></returns>
        internal override string GetRefNameForBinding(ItemBinding binding)
        {
            var etm = binding.Parent as EntityTypeMapping;
            if (etm != null
                && etm.Kind == EntityTypeMappingKind.IsTypeOf)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    EntityTypeMapping.IsTypeOfFormat,
                    NormalizedNameExternal);
            }

            var fi = binding.Parent as FunctionImport;
            if (fi != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    FunctionImport.CollectionFormat,
                    NormalizedNameExternal);
            }

            return base.GetRefNameForBinding(binding);
        }

        [SuppressMessage("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        internal override void GetXLinqInsertPosition(EFElement child, out XNode insertAt, out bool insertBefore)
        {
            // If the child is a property, check its InsertPosition value.
            // If the value is not null, retrieve the insertAt and insertBefore values from the property.
            var childProperty = child as PropertyBase;
            if (childProperty != null
                && childProperty.InsertPosition != null
                && childProperty.InsertPosition.InsertAtProperty != null)
            {
                try
                {
                    Debug.Assert(
                        childProperty is Property || childProperty is NavigationProperty,
                        "Unexpected property type. Type name: " + childProperty.GetType().Name);

                    if (childProperty is Property
                        || childProperty is NavigationProperty)
                    {
                        var insertAtProperty = childProperty.InsertPosition.InsertAtProperty;
                        Debug.Assert(
                            insertAtProperty is Property || insertAtProperty is NavigationProperty,
                            "Unexpected InsertAtProperty type. Type name: " + insertAtProperty.GetType().Name);

                        if (insertAtProperty is Property
                            || insertAtProperty is NavigationProperty)
                        {
                            if ((insertAtProperty is Property && childProperty is NavigationProperty)
                                || (insertAtProperty is NavigationProperty && childProperty is Property))
                            {
                                Debug.Fail(
                                    "The InsertAtProperty value is not valid. InsertAtProperty should have the same type as the Property that we try to insert");
                            }
                            else
                            {
                                // Check if the reference property (the property that will be the sibling of the to-be-created property) is one of the entity-type's property.
                                var parentOfInsertAtProperty = insertAtProperty.GetParentOfType(GetType()) as EntityType;
                                Debug.Assert(
                                    parentOfInsertAtProperty == this,
                                    "Expected sibling property's entityType: " + DisplayName + " , actual:" + parentOfInsertAtProperty
                                    == null
                                        ? "null"
                                        : parentOfInsertAtProperty.DisplayName);
                                if (parentOfInsertAtProperty == this)
                                {
                                    insertBefore = childProperty.InsertPosition.InsertBefore;
                                    insertAt = childProperty.InsertPosition.InsertAtProperty.XElement;
                                    return;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // Clear out the InsertPosition.
                    childProperty.InsertPosition = null;
                }
            }

            if (child is Key)
            {
                Documentation.GetInsertPositionForSiblingThatNeedsToBeAfterDocumentationElementButBeforeOtherElements(
                    this, out insertAt, out insertBefore);
            }
            else
            {
                base.GetXLinqInsertPosition(child, out insertAt, out insertBefore);
            }
        }
    }
}
