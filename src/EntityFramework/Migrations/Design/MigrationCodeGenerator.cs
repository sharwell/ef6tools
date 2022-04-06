﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace System.Data.Entity.Migrations.Design
{
    using System.Collections.Generic;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Infrastructure.Annotations;
    using System.Data.Entity.Migrations.Model;
    using System.Data.Entity.Utilities;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Base class for providers that generate code for code-based migrations.
    /// </summary>
    public abstract class MigrationCodeGenerator
    {
        private readonly IDictionary<string, Func<AnnotationCodeGenerator>> _annotationGenerators =
            new Dictionary<string, Func<AnnotationCodeGenerator>>();

        /// <summary>
        /// Generates the code that should be added to the users project.
        /// </summary>
        /// <param name="migrationId"> Unique identifier of the migration. </param>
        /// <param name="operations"> Operations to be performed by the migration. </param>
        /// <param name="sourceModel"> Source model to be stored in the migration metadata. </param>
        /// <param name="targetModel"> Target model to be stored in the migration metadata. </param>
        /// <param name="namespace"> Namespace that code should be generated in. </param>
        /// <param name="className"> Name of the class that should be generated. </param>
        /// <returns> The generated code. </returns>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "namespace")]
        public abstract ScaffoldedMigration Generate(
            string migrationId,
            IEnumerable<MigrationOperation> operations,
            string sourceModel,
            string targetModel,
            string @namespace,
            string className);

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static bool AnnotationsExist(MigrationOperation[] operations)
        {
            DebugCheck.NotNull(operations);

            return operations.OfType<IAnnotationTarget>().Any(o => o.HasAnnotations);
        }

        /// <summary>
        /// Gets the namespaces that must be output as "using" or "Imports" directives to handle
        /// the code generated by the given operations.
        /// </summary>
        /// <param name="operations"> The operations for which code is going to be generated. </param>
        /// <returns> An ordered list of namespace names. </returns>
        protected virtual IEnumerable<string> GetNamespaces(IEnumerable<MigrationOperation> operations)
        {
            Check.NotNull(operations, "operations");

            var namespaces = GetDefaultNamespaces();
            
            var operationsArray = operations.ToArray();

            if (operationsArray.OfType<AddColumnOperation>().Any(
                o => o.Column.Type == PrimitiveTypeKind.Geography || o.Column.Type == PrimitiveTypeKind.Geometry))
            {
                namespaces = namespaces.Concat(new[] { "System.Data.Entity.Spatial" });
            }

            if (AnnotationsExist(operationsArray))
            {
                namespaces = namespaces.Concat(new[] { "System.Collections.Generic", "System.Data.Entity.Infrastructure.Annotations" });
                namespaces = AnnotationGenerators.Select(a => a.Value).Where(g => g != null)
                    .Aggregate(namespaces, (c, g) => c.Concat(g().GetExtraNamespaces(AnnotationGenerators.Keys)));
            }

            return namespaces.Distinct().OrderBy(n => n);
        }

        /// <summary>
        /// Gets the default namespaces that must be output as "using" or "Imports" directives for
        /// any code generated.
        /// </summary>
        /// <param name="designer"> A value indicating if this class is being generated for a code-behind file. </param>
        /// <returns> An ordered list of namespace names. </returns>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        protected virtual IEnumerable<string> GetDefaultNamespaces(bool designer = false)
        {
            var namespaces
                = new List<string>
                    {
                        "System.Data.Entity.Migrations"
                    };

            if (designer)
            {
                namespaces.Add("System.CodeDom.Compiler");
                namespaces.Add("System.Data.Entity.Migrations.Infrastructure");
                namespaces.Add("System.Resources");
            }
            else
            {
                namespaces.Add("System");
            }

            return namespaces.OrderBy(n => n);
        }

        /// <summary>
        /// Gets the <see cref="AnnotationCodeGenerator"/> instances that are being used.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public virtual IDictionary<string, Func<AnnotationCodeGenerator>> AnnotationGenerators
        {
            get { return _annotationGenerators; }
        }
    }
}
