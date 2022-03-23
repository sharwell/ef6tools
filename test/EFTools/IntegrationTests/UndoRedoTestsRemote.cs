// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace EFDesigner.IntegrationTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.Model;
    using Microsoft.Data.Entity.Design.Model.Commands;
    using Microsoft.Data.Entity.Design.Model.Designer;
    using Microsoft.Data.Entity.Design.Model.Entity;
    using Microsoft.Data.Entity.Design.Model.Eventing;
    using Microsoft.Data.Entity.Design.Model.Mapping;
    using Microsoft.Data.Entity.Design.UI.Views;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;
    using Resources = Microsoft.Data.Tools.XmlDesignerBase.Resources;

    public class UndoRedoTestsRemote : AbstractIntegrationTest
    {
        private const string UndoCommand = "Edit.Undo";
        private const string RedoCommand = "Edit.Redo";

        private IEdmPackage _package;
        private readonly (string DeploymentDirectory, string unused) TestContext = (Path.GetDirectoryName(typeof(AutomaticDbContextTests).Assembly.Location), "");

        public override async Task InitializeAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            await base.InitializeAsync();

            PackageManager.LoadEDMPackage(ServiceProvider.GlobalProvider);
            _package = PackageManager.Package;
        }

        /// <summary>
        ///     Simple Entity tests:
        ///     1. Undo/Redo Creation of EntityType
        ///     2. Undo/Redo Deletion of EntityType
        ///     3. Undo/Redo Creation of Property1
        ///     4. Undo/Redo Creation of Property2 (test deletes/creations within same container)
        ///     5. Multiple successive undos/redos of Creating EntityType, Property1, Property2
        ///     6. Undo/Redo Deletion of Property1
        ///     7. Undo/Redo Change of Property2 Type (test multiple XLinq attribute creation/deletes)
        ///     8. Undo/Redo remove key on property
        /// </summary>
        [IdeFact]
        public async Task Simple_Entity()
        {
            const string typePrefix = "Simple_Entity";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "BlankModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        const string entityTypeName = typePrefix + "_EntityType";
                        const string property1Name = typePrefix + "_Property1";
                        const string property2Name = typePrefix + "_Property2";

                        // Note: we cannot populate these EFObjects as soon as we create them. Since we are undoing/redoing, 
                        // we will create new instances and need to discover them "on-demand"
                        ConceptualEntityType entityType;
                        ConceptualProperty id;
                        ConceptualProperty property1;
                        ConceptualProperty property2;

                        // CREATE AN ENTITY
                        CreateDefaultEntityType(commandProcessorContext, entityTypeName, typePrefix + "_EntitySet");

                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.True(entityType is not null, "Expected entity not present");
                        Assert.NotNull(entityType.GetProperty("Id", "String", isKeyProperty: true));

                        // Undo Redo "Create EntityType"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshConceptualEntity(entityTypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.NotNull(entityType);
                        Assert.NotNull(entityType.GetProperty("Id", "String", isKeyProperty: true));

                        new CommandProcessor(
                            commandProcessorContext,
                            entityType.GetDeleteCommand()).Invoke();

                        Assert.Equal(0, artifact.ConceptualModel().EntityTypeCount);

                        // Undo Redo "Delete EntityType"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshConceptualEntity(entityTypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(0, artifact.ConceptualModel().EntityTypeCount);

                        // Undo "Delete EntityType"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.NotNull(entityType);
                        Assert.NotNull(entityType.GetProperty("Id", "String", isKeyProperty: true));

                        // CREATE PROPERTY 1
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreatePropertyCommand(property1Name, entityType, "String", false)).Invoke();

                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName)
                                .GetProperty(property1Name, "String", isKeyProperty: false));

                        // Undo Redo "Create Property1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(
                            artifact.GetFreshConceptualEntity(entityTypeName)
                                .GetProperty(property1Name, "String", isKeyProperty: false));

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName)
                                .GetProperty(property1Name, "String", isKeyProperty: false));

                        // CREATE A PROPERTY 2, MULTIPLE UNDO/REDO
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreatePropertyCommand(property2Name, entityType, "String", false)).Invoke();

                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName)
                                .GetProperty(property2Name, "String", isKeyProperty: false));

                        // undo three times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);

                        Assert.Equal(0, artifact.ConceptualModel().EntityTypeCount);

                        // redo three times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);

                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.NotNull(entityType);
                        Assert.NotNull(entityType.GetProperty("Id", "String", isKeyProperty: true));
                        Assert.NotNull(entityType.GetProperty(property1Name, "String", isKeyProperty: false));
                        Assert.NotNull(entityType.GetProperty(property2Name, "String", isKeyProperty: false));

                        // SET PROPERTY KEY
                        property1 =
                            (ConceptualProperty)
                            entityType.SafeInheritedAndDeclaredProperties.Single(p => p.LocalName.Value == property1Name);
                        new CommandProcessor(
                            commandProcessorContext,
                            new SetKeyPropertyCommand(property1, true)).Invoke();

                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.NotNull(entityType.GetProperty(property1Name, "String", isKeyProperty: true));

                        // DELETE A PROPERTY (also tests deleting PropertyRef)
                        property1 =
                            (ConceptualProperty)
                            entityType.SafeInheritedAndDeclaredProperties.Single(p => p.LocalName.Value == property1Name);
                        new CommandProcessor(
                            commandProcessorContext,
                            property1.GetDeleteCommand()).Invoke();

                        Assert.Null(artifact.GetFreshConceptualEntity(entityTypeName).GetProperty(property1Name));

                        // Undo Redo "Delete Property1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshConceptualEntity(entityTypeName).GetProperty(property1Name));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.Null(entityType.GetProperty(property1Name));

                        // CHANGE PROPERTY 2 TYPE (moving from String to Byte will remove multiple facets/XLinq attributes)
                        property2 =
                            (ConceptualProperty)
                            entityType.SafeInheritedAndDeclaredProperties.Single(p => p.LocalName.Value == property2Name);
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangePropertyTypeCommand(property2, "Byte"))
                            .Invoke();
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName)
                                .GetProperty(property2Name, "Byte", isKeyProperty: false));

                        // Undo Redo "Change Property2's type from String -> Byte"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(entityType.GetProperty(property2Name, "String", isKeyProperty: false));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);

                        entityType = artifact.GetFreshConceptualEntity(entityTypeName);
                        Assert.NotNull(entityType.GetProperty(property2Name, "Byte", isKeyProperty: false));

                        // REMOVE PROPERTY KEY (this will remove the key element and PropertyRef)
                        id =
                            (ConceptualProperty)
                            entityType.SafeInheritedAndDeclaredProperties.Single(p => p.LocalName.Value == "Id");
                        new CommandProcessor(
                            commandProcessorContext,
                            new SetKeyPropertyCommand(id, false)).Invoke();

                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName).GetProperty("Id", "String", isKeyProperty: false));

                        // Undo Redo "Remove key on Property 'Id'"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName).GetProperty("Id", "String", isKeyProperty: true));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName).GetProperty("Id", "String", isKeyProperty: false));

                        // Undo "Remove key on Property 'Id'"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entityTypeName).GetProperty("Id", "String", isKeyProperty: true));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Simple Association tests:
        ///     1. Undo/Redo Creation of Association
        ///     2. Undo/Redo Deletion of Association
        ///     3. Undo/Redo Creation of another Association
        ///     4. Undo/Redo Change of Multiplicity
        ///     5. Undo/Redo Change of Role
        ///     6. Undo/Redo Rename of Navigation Property (AssocationSetEnd Roles should get updated)
        ///     7. Undo/Redo Rename of EntitySet (AssociationSetEnds should get updated)
        ///     8. Undo/Redo Rename of Association
        ///     9. Undo/Redo Rename of AssociationSet
        /// </summary>
        [IdeFact]
        public async Task Simple_Association()
        {
            const string typePrefix = "Simple_Association";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "BlankModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        const string entity1TypeName = typePrefix + "_EntityType1";
                        const string entity2TypeName = typePrefix + "_EntityType2";
                        const string association1TypeName = typePrefix + "_Association1";
                        const string association2TypeName = typePrefix + "_Association2";

                        // Note: we cannot populate these EFObjects as soon as we create them. Since we are undoing/redoing, 
                        // we will create new instances and need to discover them "on-demand"
                        ConceptualEntityType entityType1;
                        ConceptualEntityType entityType2;

                        // CREATE TWO ENTITIES
                        CreateDefaultEntityType(commandProcessorContext, entity1TypeName, typePrefix + "_EntitySet1");
                        CreateDefaultEntityType(commandProcessorContext, entity2TypeName, typePrefix + "_EntitySet2");

                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        entityType2 = artifact.GetFreshConceptualEntity(entity2TypeName);

                        Assert.NotNull(entityType1);
                        Assert.NotNull(entityType2);

                        // CREATE ASSOCIATION
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateConceptualAssociationCommand(
                                association1TypeName,
                                entityType1, "*", "Entity2",
                                entityType2, "1", "Entity1",
                                uniquifyNames: false,
                                createForeignKeyProperties: false)).Invoke();

                        Assert.True(artifact.GetFreshAssociation(association1TypeName) is not null, "Association not created.");

                        // Undo Redo "Create Association1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshAssociation(association1TypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);

                        Assert.True(
                            artifact.GetFreshAssociation(association1TypeName) is not null, "Association should not have disappeared.");

                        // DELETE ASSOCIATION
                        new CommandProcessor(
                            commandProcessorContext,
                            artifact.GetFreshAssociation(association1TypeName).GetDeleteCommand()).Invoke();
                        Assert.Equal(0, artifact.ConceptualModel().AssociationCount);

                        // Undo Redo "Delete Association1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshAssociation(association1TypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(0, artifact.ConceptualModel().AssociationCount);

                        // Undo "Delete Association1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.True(
                            artifact.GetFreshAssociation(association1TypeName) is not null, "Association not re-created after Undo.");

                        // CREATE ANOTHER ASSOCIATION 2 (we'll make it a self-association for good measure)
                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        entityType2 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateConceptualAssociationCommand(
                                association2TypeName,
                                entityType1, "*", "Entity1_1",
                                entityType1, "1", "Entity1_2",
                                uniquifyNames: false,
                                createForeignKeyProperties: false)).Invoke();
                        Assert.True(artifact.GetFreshAssociation(association2TypeName) is not null, "Association not created.");

                        // Undo Redo "Create Association2"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshAssociation(association2TypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.True(
                            artifact.GetFreshAssociation(association2TypeName) is not null, "Association should not have disappeared.");

                        // CHANGE MULTIPLICITY
                        var association1End1 = artifact.GetFreshAssociationEnd(association1TypeName, 0);
                        Assert.Equal("*", association1End1.Multiplicity.Value);
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeAssociationEndCommand(association1End1, "1", null)).Invoke();

                        Assert.Equal("1", artifact.GetFreshAssociationEnd(association1TypeName, 0).Multiplicity.Value);

                        //Undo Redo "Change Association1 End1 Multiplicity: * --> 1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal("*", artifact.GetFreshAssociationEnd(association1TypeName, 0).Multiplicity.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal("1", artifact.GetFreshAssociationEnd(association1TypeName, 0).Multiplicity.Value);

                        // CHANGE ROLE
                        var association2End1 = artifact.GetFreshAssociationEnd(association2TypeName, 0);
                        Assert.Equal("Simple_Association_EntityType1", association2End1.Role.Value);
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeAssociationEndCommand(association2End1, association2End1.Multiplicity.Value, "Entity2"))
                            .Invoke();
                        Assert.Equal("Entity2", artifact.GetFreshAssociationEnd(association2TypeName, 0).Role.Value);

                        // Undo Redo "Change Association2 End2 Role: Entity1 --> Entity2"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            "Simple_Association_EntityType1",
                            artifact.GetFreshAssociationEnd(association2TypeName, 0).Role.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal("Entity2", artifact.GetFreshAssociationEnd(association2TypeName, 0).Role.Value);

                        // CHANGE NAVIGATION PROPERTY NAME
                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        var entityType1NavProp1 =
                            entityType1.NavigationProperties().Single(np => np.LocalName.Value == "Entity2");
                        new CommandProcessor(
                            commandProcessorContext,
                            new EntityDesignRenameCommand(entityType1NavProp1, "Entity2_1", false)).Invoke();
                        Assert.NotNull(
                            entityType1.NavigationProperties().SingleOrDefault(np => np.LocalName.Value == "Entity2_1"));

                        // Undo Redo "Rename EntityType1 NavigationProperty1: Entity2 --> Entity2_1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entity1TypeName)
                                .NavigationProperties()
                                .SingleOrDefault(np => np.LocalName.Value == "Entity2"));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity(entity1TypeName)
                                .NavigationProperties()
                                .SingleOrDefault(np => np.LocalName.Value == "Entity2_1"));

                        // RENAME ENTITYSET (changes AssociationSetEnds)
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association1TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[1].EntitySet
                                .RefName);

                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        var entityType1Set = (ConceptualEntitySet)entityType1.EntitySet;
                        RenameCommand renameEt1set = new EntityDesignRenameCommand(entityType1Set, "Entity1Set_1", false);
                        new CommandProcessor(
                            commandProcessorContext,
                            new EntityDesignRenameCommand(entityType1Set, "Entity1Set_1", false)).Invoke();
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association1TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[1].EntitySet
                                .RefName);

                        // Undo Redo"Rename EntityType1 EntitySet: Entity1Set --> Entity1Set_1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association1TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Simple_Association_EntitySet1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[1].EntitySet
                                .RefName);

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association1TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[0].EntitySet
                                .RefName);
                        Assert.Equal(
                            "Entity1Set_1",
                            artifact.GetFreshAssociation(association2TypeName).AssociationSet.AssociationSetEnds()[1].EntitySet
                                .RefName);

                        // RENAME ASSOCIATION
                        new CommandProcessor(
                            commandProcessorContext,
                            new EntityDesignRenameCommand(
                                artifact.GetFreshAssociation(association2TypeName),
                                "Entity1Entity1_new",
                                false)).Invoke();
                        Assert.NotNull(artifact.GetFreshAssociation("Entity1Entity1_new"));

                        // Undo Redo "Rename Association2"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshAssociation(association2TypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(artifact.GetFreshAssociation("Entity1Entity1_new"));

                        // RENAME ASSOCIATION
                        new CommandProcessor(
                            commandProcessorContext,
                            new EntityDesignRenameCommand(
                                artifact.GetFreshAssociation(association1TypeName),
                                "Entity2Entity1_newSet",
                                false)).Invoke();

                        Assert.Null(artifact.GetFreshAssociation(association1TypeName));
                        Assert.NotNull(artifact.GetFreshAssociation("Entity2Entity1_newSet"));

                        // Undo Redo "Rename Association1 Set"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshAssociation(association1TypeName));
                        Assert.Null(artifact.GetFreshAssociation("Entity2Entity1_newSet"));

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshAssociation(association1TypeName));
                        Assert.NotNull(artifact.GetFreshAssociation("Entity2Entity1_newSet"));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Simple Inheritance tests:
        ///     1. Undo/Redo Creation of Inheritance
        ///     2. Undo/Redo Deletion of Inheritance
        ///     3. Undo/Redo Changing BaseType of Derived EntityType
        ///     4. Multiple successive undos/redos of creating 3-level inheritance
        /// </summary>
        [IdeFact]
        public async Task Simple_Inheritance()
        {
            const string typePrefix = "Simple_Inheritance";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "BlankModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        const string entity1TypeName = typePrefix + "_EntityType1";
                        const string entity2TypeName = typePrefix + "_EntityType2";
                        const string entity3TypeName = typePrefix + "_EntityType3";

                        // Note: we cannot populate these EFObjects as soon as we create them. Since we are undoing/redoing, 
                        // we will create new instances and need to discover them "on-demand"
                        ConceptualEntityType entityType1;
                        ConceptualEntityType entityType2;
                        ConceptualEntityType entityType3;

                        // CREATE THREE ENTITIES
                        CreateDefaultEntityType(commandProcessorContext, entity1TypeName, typePrefix + "_EntitySet1");
                        CreateDefaultEntityType(commandProcessorContext, entity2TypeName, typePrefix + "_EntitySet2");
                        CreateDefaultEntityType(commandProcessorContext, entity3TypeName, typePrefix + "_EntitySet3");

                        // CREATE INHERITANCE
                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        entityType2 = artifact.GetFreshConceptualEntity(entity2TypeName);
                        Assert.Null(entityType2.BaseType.Target);
                        new CommandProcessor(commandProcessorContext, new CreateInheritanceCommand(entityType2, entityType1))
                            .Invoke();
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // DELETE INHERITANCE
                        new CommandProcessor(
                            commandProcessorContext,
                            new DeleteInheritanceCommand(entityType2)).Invoke();
                        Assert.Null(artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // Undo Redo "Delete Inheritance1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // Undo "Delete Inheritance1"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // CHANGE BASE TYPE OF DERIVED TYPE
                        entityType3 = artifact.GetFreshConceptualEntity(entity3TypeName);
                        ViewUtils.SetBaseEntityType(commandProcessorContext, entityType2, entityType3);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity3TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // Undo Redo "Change Base Type of EntityType2: EntityType1 --> EntityType3"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity3TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);

                        // CREATE 3-LEVEL INHERITANCE, ROLLBACK ALL THE WAY
                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        entityType2 = artifact.GetFreshConceptualEntity(entity2TypeName);
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateInheritanceCommand(entityType1, entityType2)).Invoke();
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity3TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity2TypeName),
                            artifact.GetFreshConceptualEntity(entity1TypeName).BaseType.Target);

                        // undo two times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity1TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        Assert.Null(artifact.GetFreshConceptualEntity(entity1TypeName).BaseType.Target);

                        // redo two times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity3TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity2TypeName),
                            artifact.GetFreshConceptualEntity(entity1TypeName).BaseType.Target);

                        // DELETE INHERITANCE 2, DELETE ENTITY TYPE1, UNDO 2, REDO 2
                        new CommandProcessor(commandProcessorContext, new DeleteInheritanceCommand(entityType1)).Invoke();
                        entityType1 = artifact.GetFreshConceptualEntity(entity1TypeName);
                        Assert.Null(entityType1.BaseType.Target);

                        new CommandProcessor(commandProcessorContext, entityType1.GetDeleteCommand()).Invoke();
                        Assert.Null(artifact.GetFreshConceptualEntity(entity1TypeName));

                        // undo two times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshConceptualEntity(entity1TypeName));
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity3TypeName),
                            artifact.GetFreshConceptualEntity(entity2TypeName).BaseType.Target);
                        Assert.Equal(
                            artifact.GetFreshConceptualEntity(entity2TypeName),
                            artifact.GetFreshConceptualEntity(entity1TypeName).BaseType.Target);

                        // redo two times
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);

                        Assert.Null(artifact.GetFreshConceptualEntity(entity1TypeName));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Simple FunctionImport tests:
        ///     1. Create 3 function imports, change return type, sproc, undo 5 times, redo 5 times
        /// </summary>
        [IdeFact]
        public async Task Simple_FunctionImport()
        {
            const string typePrefix = "Simple_FunctionImport";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        var conceptualEntityContainer =
                            (ConceptualEntityContainer)artifact.ConceptualModel().EntityContainers().Single();
                        var entityContainerMapping = artifact.MappingModel().EntityContainerMappings().Single();

                        Action<Function, string, object> createFunctionImport = (function, functionImportName, returnType) =>
                            {
                                var functionImportCmd = new CreateFunctionImportCommand(
                                    conceptualEntityContainer, function, functionImportName, returnType);
                                var functionImportMappingCmd = new CreateFunctionImportMappingCommand(
                                    entityContainerMapping, function, functionImportCmd.Id);
                                functionImportMappingCmd.AddPreReqCommand(functionImportCmd);
                                new CommandProcessor(commandProcessorContext, functionImportCmd, functionImportMappingCmd)
                                    .Invoke();
                            };

                        // CREATE THREE FUNCTION IMPORTS (don't undo/redo yet)
                        var function1 = artifact.GetFreshStorageFunction("GetFreightCost");
                        createFunctionImport(function1, "a", Resources.NoneDisplayValueUsedForUX);
                        Assert.Equal(
                            "NorthwindModel.Store.GetFreightCost", artifact.GetFreshFunctionMapping("a").FunctionName.RefName);
                        Assert.Equal(
                            Resources.NoneDisplayValueUsedForUX,
                            ((DefaultableValue<string>)artifact.GetFreshFunctionImport("a").ReturnType).Value);

                        createFunctionImport(function1, "aa", "Int16");
                        Assert.Equal(
                            "NorthwindModel.Store.GetFreightCost", artifact.GetFreshFunctionMapping("aa").FunctionName.RefName);
                        Assert.Equal(
                            "Collection(Int16)",
                            ((DefaultableValue<string>)artifact.GetFreshFunctionImport("aa").ReturnType).Value);

                        var function2 = artifact.GetFreshStorageFunction("Sales_by_Year");
                        createFunctionImport(function2, "aaa", artifact.GetFreshConceptualEntity("Orders"));
                        Assert.Equal(
                            "NorthwindModel.Store.Sales_by_Year", artifact.GetFreshFunctionMapping("aaa").FunctionName.RefName);
                        Assert.Equal(
                            "Collection(NorthwindModel.Orders)",
                            ((SingleItemBinding<EntityType>)artifact.GetFreshFunctionImport("aaa").ReturnType).RefName);

                        // CHANGE RETURN TYPE (don't undo/redo yet)
                        var functionImport = artifact.GetFreshFunctionImport("aaa");
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeFunctionImportCommand(
                                conceptualEntityContainer,
                                functionImport,
                                function2,
                                "aaa",
                                functionImport.IsComposable.Value,
                                /* changeReturnType */ true,
                                Resources.NoneDisplayValueUsedForUX)
                            ).Invoke();

                        // Mapping must not be changed.
                        Assert.Equal(
                            "NorthwindModel.Store.Sales_by_Year", artifact.GetFreshFunctionMapping("aaa").FunctionName.RefName);
                        Assert.Equal(
                            Resources.NoneDisplayValueUsedForUX,
                            ((DefaultableValue<string>)artifact.GetFreshFunctionImport("aaa").ReturnType).Value);

                        // CHANGE STORED PROCEDURE NAME (don't undo/redo yet)
                        functionImport = artifact.GetFreshFunctionImport("aaa");
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeFunctionImportCommand(
                                conceptualEntityContainer,
                                functionImport,
                                null,
                                "aaa",
                                functionImport.IsComposable.Value,
                                false,
                                null)).Invoke();

                        Assert.NotNull(artifact.GetFreshFunctionImport("aaa"));
                        Assert.Equal(
                            Resources.NoneDisplayValueUsedForUX,
                            ((DefaultableValue<string>)artifact.GetFreshFunctionImport("aaa").ReturnType).Value);
                        Assert.True(artifact.GetFreshFunctionMapping("aaa") is null, "Mapping should have been deleted.");
                        Assert.True(
                            artifact.GetFreshStorageFunction("Sales_by_Year") is not null, "Function should not have been deleted.");

                        // UNDO/REDO 5 LEVELS 
                        // undo five times (Undo Change Sproc, Change Return Type, Create FI3, Create FI2, Create FI1)
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);

                        Assert.False(conceptualEntityContainer.FunctionImports().Any());
                        Assert.False(entityContainerMapping.FunctionImportMappings().Any());

                        // redo five times (Redo Create FI1, FI2, FI3, Change Return Type, Change Sproc)
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);

                        Assert.Equal(3, conceptualEntityContainer.FunctionImports().Count());
                        Assert.Equal(2, entityContainerMapping.FunctionImportMappings().Count());
                        Assert.Equal(
                            "NorthwindModel.Store.GetFreightCost", artifact.GetFreshFunctionMapping("a").FunctionName.RefName);
                        Assert.Equal(
                            "NorthwindModel.Store.GetFreightCost", artifact.GetFreshFunctionMapping("aa").FunctionName.RefName);
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Various condition tests:
        ///     1. Undo/Redo Creation of Condition
        ///     2. Undo/Redo Creation of Condition on derived class
        ///     3. Undo/Redo Change Condition predicate
        ///     4. Delete condition
        /// </summary>
        [IdeFact]
        public async Task Mapping_Conditions()
        {
            const string typePrefix = "Mapping_Conditions";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        // CREATE CONDITION
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateFragmentConditionCommand(
                                artifact.GetFreshConceptualEntity("Customers"),
                                (Property)artifact.GetFreshStorageEntity("Customers").GetFirstNamedChildByLocalName("City"),
                                null,
                                "Redmond")).Invoke();

                        Assert.Equal("Redmond", artifact.GetFreshCondition("Customers", "City").Value.Value);

                        // Undo Redo "Create Condition: Customers.City == Redmond"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshCondition("Customers", "Redmond"));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal("Redmond", artifact.GetFreshCondition("Customers", "City").Value.Value);

                        // CREATE CONDITION ON DERIVED TYPE
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateFragmentConditionCommand(
                                artifact.GetFreshConceptualEntity("Order_Details_Extended"),
                                (Property)
                                artifact.GetFreshStorageEntity("Order Details Extended")
                                    .GetFirstNamedChildByLocalName("ProductName"),
                                false,
                                String.Empty)).Invoke();

                        Assert.Equal("false", artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);

                        // Undo Redo "Create Condition on Derived ET: Order_Details_Extended.ProductName IsNull == false"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName"));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal("false", artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);

                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            new ChangeConditionPredicateCommand(
                                artifact.GetFreshCondition("Order_Details", "ProductName"),
                                null,
                                "Escher"));

                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);
                        Assert.Equal("Escher", artifact.GetFreshCondition("Order_Details", "ProductName").Value.Value);

                        // Undo redo "Change Condition predicate: Order_Details_Extended.ProductName IsNull == false --> ProductName Value == Escher"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName").Value.Value);
                        Assert.Equal("false", artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);
                        Assert.Equal("Escher", artifact.GetFreshCondition("Order_Details", "ProductName").Value.Value);

                        new CommandProcessor(
                            commandProcessorContext,
                            artifact.GetFreshCondition("Order_Details", "ProductName").GetDeleteCommand()).Invoke();

                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName"));

                        // Undo redo "Delete Condition on Order_Details_Extended"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);
                        Assert.Equal("Escher", artifact.GetFreshCondition("Order_Details", "ProductName").Value.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName"));

                        // Undo "Delete Condition on Order_Details_Extended"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshCondition("Order_Details", "ProductName").IsNull.Value);
                        Assert.Equal("Escher", artifact.GetFreshCondition("Order_Details", "ProductName").Value.Value);
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     General mapping tests on blank model
        ///     1. Undo/Redo Creation of EntitySetMapping on blank model (should create EntityContainerMapping)
        ///     2. Undo/Redo Create two Entity Type Mappings within same Entity Set Mapping (TPH)
        /// </summary>
        [IdeFact]
        public async Task Mapping_General_Blank()
        {
            const string typePrefix = "Mapping_General_Blank";
            const string testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "BlankModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        const string entityType1Name = typePrefix + "_EntityType1";
                        const string entityType2Name = typePrefix + "_EntityType2";
                        const string entitySetName = typePrefix + "_EntitySet";

                        CreateDefaultEntityType(commandProcessorContext, entityType1Name, entitySetName);
                        CreateEntityTypeCommand.CreateConceptualEntityTypeAndEntitySetAndProperty(
                            commandProcessorContext,
                            entityType2Name,
                            entitySetName,
                            /*createKeyProperty*/ false,
                            null,
                            null,
                            null,
                            /*uniquifyNames*/ true);

                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateInheritanceCommand(
                                artifact.GetFreshConceptualEntity(entityType2Name),
                                artifact.GetFreshConceptualEntity(entityType1Name))).Invoke();

                        // CREATE TWO ENTITY TYPE MAPPINGS WITHIN THE SAME ENTITY SET MAPPING
                        var commandProcessor = new CommandProcessor(commandProcessorContext);
                        commandProcessor.EnqueueCommand(
                            new CreateEntityTypeMappingCommand(artifact.GetFreshConceptualEntity(entityType1Name)));
                        commandProcessor.EnqueueCommand(
                            new CreateEntityTypeMappingCommand(artifact.GetFreshConceptualEntity(entityType2Name)));
                        commandProcessor.Invoke();

                        Assert.Equal(2, artifact.GetFreshEntitySetMapping(entitySetName).EntityTypeMappings().Count);

                        // Undo redo "Create two EntityTypeMappings within same EntitySetMapping"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshEntitySetMapping(entitySetName));
                        Assert.NotNull(artifact.GetFreshConceptualEntity(entityType1Name));
                        Assert.NotNull(artifact.GetFreshConceptualEntity(entityType2Name));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Equal(2, artifact.GetFreshEntitySetMapping(entitySetName).EntityTypeMappings().Count);
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     General mapping tests on generated Northwind model (Function mapping, table mapping, association mapping, ResultBindings, function imports)
        ///     1. Undo/Redo Creation of FunctionMapping
        ///     2. Undo/Redo Deletion of AssociationSetMapping
        ///     3. Undo/Redo Creation of AssociationSetMapping
        ///     4. Undo/Redo Change EntityType to Abstract (should remove any function mappings)
        ///     5. Undo/Redo Deletion of ScalarProperty when we have ETMs
        ///     5. Undo/Redo Create of ScalarProperty when we have ETMs
        ///     6. Undo/Redo Creation of ResultBindings
        ///     7. Undo/Redo Change of FunctionScalarProperty
        ///     8. Undo/Redo Deletion of FunctionMapping
        ///     9. Undo/Redo Creation of Inheritance (will move EntityTypeMapping within ESMs)
        /// </summary>
        [IdeFact]
        public async Task Mapping_General_DB()
        {
            var typePrefix = "Mapping_General_DB";
            var testName = "UndoRedo." + typePrefix;

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        // CREATE FUNCTION MAPPING
                        const string orderDetailsEntityTypeName = "Order_Details";
                        Assert.Null(
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName));

                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateFunctionMappingCommand(
                                artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName),
                                artifact.GetFreshStorageFunction("GetFreightCost"),
                                null,
                                ModificationFunctionType.Insert)).Invoke();

                        // Undo redo "Create Function Mapping"
                        Assert.NotNull(
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                .ModificationFunctionMapping.InsertFunction);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                .ModificationFunctionMapping.InsertFunction);

                        // DELETE ASSOCIATION SET MAPPING
                        const string fkOrdersCustomersAssociationSetName = "FK_Orders_Customers";
                        Assert.NotNull(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));

                        new CommandProcessor(
                            commandProcessorContext,
                            artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName).GetDeleteCommand())
                            .Invoke();

                        // Undo redo "Delete Association Set Mapping"
                        Assert.Null(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));

                        // CREATE ASSOCIATION SET MAPPING (just re-create what we deleted)                   
                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            new CreateAssociationSetMappingCommand(
                                artifact.GetFreshAssociation(fkOrdersCustomersAssociationSetName),
                                artifact.GetFreshStorageEntity("Orders"))
                            );
                        Assert.NotNull(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));

                        // Undo redo "Create Association Set Mapping"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(artifact.GetFreshAssociationSetMapping(fkOrdersCustomersAssociationSetName));

                        // CHANGE ENTITY TYPE TO ABSTRACT (will remove any function mappings)
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeEntityTypeAbstractCommand(
                                artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName),
                                true)).Invoke();

                        Assert.True(artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName).Abstract.Value);

                        // Undo Redo "Change Entity Type Abstract: Order_Details:false --> true"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.False(artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName).Abstract.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.True(artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName).Abstract.Value);

                        // Undo "Change Entity Type Abstract: Order_Details:false --> true"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.False(artifact.GetFreshConceptualEntity(orderDetailsEntityTypeName).Abstract.Value);

                        // DELETION OF SCALAR PROPERTY IN AN ENTITY TYPE MAPPING                  
                        const string customersEntitySetName = "Customers";
                        const string customersTypeMappingName = "IsTypeOf(NorthwindModel.Customers)";
                        const string addressPropertyName = "Address";
                        new CommandProcessor(
                            commandProcessorContext,
                            artifact.GetFreshScalarProperty("Customers", "IsTypeOf(NorthwindModel.Customers)", "Address")
                                .GetDeleteCommand()).Invoke();
                        Assert.Null(
                            artifact.GetFreshScalarProperty(
                                customersEntitySetName, customersTypeMappingName, addressPropertyName));

                        // Undo redo "Delete Scalar Property in EntityTypeMapping"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(
                            artifact.GetFreshScalarProperty(
                                customersEntitySetName, customersTypeMappingName, addressPropertyName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(
                            artifact.GetFreshScalarProperty(
                                customersEntitySetName, customersTypeMappingName, addressPropertyName));

                        // CREATE SCALAR PROPERTY IN AN ENTITY TYPE MAPPING (just create what we just deleted)
                        var customersEntityType = artifact.GetFreshConceptualEntity("Customers");
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateFragmentScalarPropertyCommand(
                                customersEntityType,
                                customersEntityType.Properties().Single(p => p.LocalName.Value == addressPropertyName),
                                artifact.GetFreshStorageEntity("Customers")
                                    .Properties()
                                    .Single(p => p.LocalName.Value == addressPropertyName))).Invoke();

                        var scalarPropertyMapping = artifact.GetFreshScalarProperty(
                            customersEntitySetName, customersTypeMappingName, addressPropertyName);
                        Assert.NotNull(scalarPropertyMapping);
                        Assert.Equal(addressPropertyName, scalarPropertyMapping.ColumnName.Target.LocalName.Value);

                        // Undo Redo "Create Scalar Property in EntityTypeMapping"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(
                            artifact.GetFreshScalarProperty(
                                customersEntitySetName, customersTypeMappingName, addressPropertyName));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(
                            artifact.GetFreshScalarProperty(
                                customersEntitySetName, customersTypeMappingName, addressPropertyName));

                        // CREATE RESULT BINDING
                        var orderDetailsEntityType = artifact.GetFreshConceptualEntity("Order_Details");
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateResultBindingCommand(
                                orderDetailsEntityType,
                                artifact.GetFreshStorageFunction("GetFreightCost"),
                                ModificationFunctionType.Insert,
                                orderDetailsEntityType.Properties().Single(p => p.LocalName.Value == "UnitPrice"),
                                "UnitPrice")).Invoke();

                        //UndoRedo "Create ResultBinding"
                        var insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.Equal("UnitPrice", insertFunctionMapping.ResultBindings().Single().ColumnName.Value);
                        Assert.Equal(
                            "UnitPrice", insertFunctionMapping.ResultBindings().Single().Name.Target.LocalName.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Equal(
                            0,
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                .ModificationFunctionMapping.InsertFunction.ResultBindings()
                                .Count);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.Equal("UnitPrice", insertFunctionMapping.ResultBindings().Single().ColumnName.Value);
                        Assert.Equal(
                            "UnitPrice", insertFunctionMapping.ResultBindings().Single().Name.Target.LocalName.Value);

                        // CHANGE FUNCTION SCALAR PROPERTY (we have to create one first though)
                        // create one pointing through navigation property
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateFunctionScalarPropertyCommand(
                                artifact.GetFreshEntityTypeMapping(
                                    "Order_Details", "NorthwindModel." + orderDetailsEntityTypeName)
                                    .ModificationFunctionMapping.InsertFunction,
                                artifact.GetFreshConceptualEntity("Orders")
                                    .Properties()
                                    .Single(p => p.LocalName.Value == "Freight"),
                                artifact.GetFreshConceptualEntity("Order_Details")
                                    .NavigationProperties()
                                    .Single(nv => nv.LocalName.Value == "Orders"),
                                artifact.GetFreshStorageFunction("GetFreightCost")
                                    .Parameters()
                                    .Single(param => param.LocalName.Value == "Freight"),
                                null)).Invoke();

                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.NotNull(insertFunctionMapping);
                        Assert.Single(insertFunctionMapping.AssociationEnds());
                        Assert.Single(insertFunctionMapping.AssociationEnds().Single().ScalarProperties());
                        Assert.Equal(
                            "Freight",
                            insertFunctionMapping.AssociationEnds()
                                .Single()
                                .ScalarProperties()
                                .Single()
                                .Name.Target.LocalName.Value);

                        // change it to one on owning entity type
                        new CommandProcessor(
                            commandProcessorContext,
                            new ChangeFunctionScalarPropertyCommand(
                                artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                    .ModificationFunctionMapping.InsertFunction.AssociationEnds()
                                    .Single()
                                    .ScalarProperties()
                                    .Single(),
                                artifact.GetFreshConceptualEntity("Order_Details")
                                    .Properties()
                                    .Where(p => p.LocalName.Value == "UnitPrice")
                                    .ToList(),
                                null,
                                artifact.GetFreshStorageFunction("GetFreightCost")
                                    .Parameters()
                                    .Single(param => param.LocalName.Value == "Freight"),
                                null)).Invoke();

                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.Equal(0, insertFunctionMapping.AssociationEnds().Count);
                        Assert.Equal(1, insertFunctionMapping.ScalarProperties().Count);
                        Assert.Equal(
                            "UnitPrice", insertFunctionMapping.ScalarProperties().Single().Name.Target.LocalName.Value);

                        // Undo Redo "Change FunctionScalarProperty: Order_Details.Orders.Freight --> Order_Details.UnitPrice"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.NotNull(insertFunctionMapping);
                        Assert.Single(insertFunctionMapping.AssociationEnds());
                        Assert.Single(insertFunctionMapping.AssociationEnds().Single().ScalarProperties());
                        Assert.Equal(
                            "Freight",
                            insertFunctionMapping.AssociationEnds()
                                .Single()
                                .ScalarProperties()
                                .Single()
                                .Name.Target.LocalName.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.Equal(0, insertFunctionMapping.AssociationEnds().Count);
                        Assert.Equal(1, insertFunctionMapping.ScalarProperties().Count);
                        Assert.Equal(
                            "UnitPrice", insertFunctionMapping.ScalarProperties().Single().Name.Target.LocalName.Value);

                        // DELETE FUNCTION MAPPING
                        new CommandProcessor(
                            commandProcessorContext,
                            ModificationFunction.GetDeleteCommand(
                                artifact.GetFreshConceptualEntity("Order_Details"),
                                artifact.GetFreshStorageFunction("GetFreightCost"),
                                ModificationFunctionType.Insert
                                )).Invoke();

                        Assert.Null(artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details"));
                        // Undo Redo "Delete Function Mapping"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        insertFunctionMapping =
                            artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details")
                                .ModificationFunctionMapping.InsertFunction;
                        Assert.Equal(0, insertFunctionMapping.AssociationEnds().Count);
                        Assert.Equal(1, insertFunctionMapping.ScalarProperties().Count);
                        Assert.Equal(
                            "UnitPrice", insertFunctionMapping.ScalarProperties().Single().Name.Target.LocalName.Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshEntityTypeMapping("Order_Details", "NorthwindModel.Order_Details"));

                        // CREATE INHERITANCE
                        new CommandProcessor(
                            commandProcessorContext,
                            new CreateInheritanceCommand(
                                artifact.GetFreshConceptualEntity("Customers"),
                                artifact.GetFreshConceptualEntity("CustomerHistory"))).Invoke();

                        Assert.Null(artifact.GetFreshConceptualEntitySet("Customers"));
                        Assert.Equal(
                            "CustomerHistory", artifact.GetFreshConceptualEntity("Customers").BaseType.Target.Name.Value);
                        Assert.Null(artifact.GetFreshEntitySetMapping("Customers"));
                        Assert.Equal(2, artifact.GetFreshEntitySetMapping("CustomerHistory").EntityTypeMappings().Count());
                        Assert.NotNull(
                            artifact.GetFreshEntityTypeMapping("CustomerHistory", "IsTypeOf(NorthwindModel.Customers)"));

                        // Undo redo "Create Inheritance"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshConceptualEntitySet("Customers"));
                        Assert.Null(artifact.GetFreshConceptualEntity("Customers").BaseType.Target);
                        Assert.NotNull(artifact.GetFreshEntitySetMapping("Customers"));
                        Assert.Single(artifact.GetFreshEntitySetMapping("CustomerHistory").EntityTypeMappings());
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshConceptualEntitySet("Customers"));
                        Assert.Equal(
                            "CustomerHistory", artifact.GetFreshConceptualEntity("Customers").BaseType.Target.Name.Value);
                        Assert.Null(artifact.GetFreshEntitySetMapping("Customers"));
                        Assert.Equal(2, artifact.GetFreshEntitySetMapping("CustomerHistory").EntityTypeMappings().Count());
                        Assert.NotNull(
                            artifact.GetFreshEntityTypeMapping("CustomerHistory", "IsTypeOf(NorthwindModel.Customers)"));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Delete "Orders" EntityType from Northwind model and undo - all NavigationProperties should be resolved
        /// </summary>
        [IdeFact]
        public async Task TestBug634186()
        {
            const string testName = "UndoRedo.TestBug634186";

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            artifact.GetFreshConceptualEntity("Orders").GetDeleteCommand());

                        Assert.Null(artifact.GetFreshConceptualEntitySet("Orders"));
                        Assert.Null(artifact.GetFreshConceptualEntity("Orders"));
                        Assert.True(
                            !artifact.ConceptualModel()
                                    .EntityTypes().SelectMany(e => ((ConceptualEntityType)e).NavigationProperties())
                                    .Any(p => p.Name.Value == "Orders"));
                        Assert.True(
                            !artifact.ConceptualModel()
                                    .EntityContainers().Single()
                                    .AssociationSets()
                                    .SelectMany(a => a.AssociationSetEnds())
                                    .Any(e => e.EntitySet.RefName == "Orders"));

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);

                        var props = artifact.ConceptualModel().EntityTypes().SelectMany(e => e.Properties());

                        Assert.NotNull(artifact.GetFreshConceptualEntitySet("Orders"));
                        Assert.NotNull(artifact.GetFreshConceptualEntity("Orders"));
                        Assert.Contains(
                            artifact.ConceptualModel()
                                .EntityTypes().SelectMany(e => ((ConceptualEntityType)e).NavigationProperties()),
                            p => p.Name.Value == "Orders");
                        Assert.Contains(
                            artifact.ConceptualModel()
                                .EntityContainers().Single()
                                .AssociationSets()
                                .SelectMany(a => a.AssociationSetEnds()),
                            e => e.EntitySet.RefName == "Orders");

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshConceptualEntitySet("Orders"));
                        Assert.Null(artifact.GetFreshConceptualEntity("Orders"));
                        Assert.True(
                            !artifact.ConceptualModel()
                                    .EntityTypes().SelectMany(e => ((ConceptualEntityType)e).NavigationProperties())
                                    .Any(p => p.Name.Value == "Orders"));
                        Assert.True(
                            !artifact.ConceptualModel()
                                    .EntityContainers().Single()
                                    .AssociationSets()
                                    .SelectMany(a => a.AssociationSetEnds())
                                    .Any(e => e.EntitySet.RefName == "Orders"));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Perform an action, then undo. The document should not be dirty
        /// </summary>
        [IdeFact]
        public async Task Bug567795_TestDirty()
        {
            const string testName = "UndoRedo.Bug567795_TestDirty";

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        new CommandProcessor(
                            commandProcessorContext,
                            artifact.GetFreshConceptualEntity("Orders").GetDeleteCommand()).Invoke();

                        Assert.True(dte.IsDocumentDirty(artifact.Uri.LocalPath));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.False(dte.IsDocumentDirty(artifact.Uri.LocalPath));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Create a function import returning a complex type then undo.
        /// </summary>
        [IdeFact]
        public async Task FunctionImportReturnComplexType()
        {
            const string testName = "UndoRedo.FunctionImportReturnComplexType";

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        var commandProcessor = new CommandProcessor(commandProcessorContext);
                        var createComplexTypeCmd = new CreateComplexTypeCommand("Sales_by_year_result", true);
                        commandProcessor.EnqueueCommand(createComplexTypeCmd);
                        commandProcessor.EnqueueCommand(
                            new CreateComplexTypePropertyCommand("Column1", createComplexTypeCmd, "Int32", false));
                        commandProcessor.EnqueueCommand(
                            new CreateFunctionImportCommand(
                                artifact.GetFreshConceptualEntityContainer("NorthwindEntities"),
                                artifact.GetFreshStorageFunction("Sales_by_Year"),
                                "myfunctionimport",
                                createComplexTypeCmd));

                        commandProcessor.Invoke();

                        Assert.NotNull(artifact.GetFreshComplexType("Sales_by_year_result"));
                        Assert.NotNull(artifact.GetFreshComplexType("Sales_by_year_result"));
                        Assert.NotNull(artifact.GetFreshFunctionImport("myfunctionimport"));
                        Assert.Equal(
                            "Collection(NorthwindModel.Sales_by_year_result)",
                            ((SingleItemBinding<ComplexType>)artifact.GetFreshFunctionImport("myfunctionimport").ReturnType)
                                .RefName);

                        //Undo Redo Create FunctionImport
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshComplexType("Sales_by_year_result"));
                        Assert.Null(artifact.GetFreshFunctionImport("myfunctionimport"));
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.NotNull(artifact.GetFreshComplexType("Sales_by_year_result"));
                        Assert.NotNull(artifact.GetFreshFunctionImport("myfunctionimport"));
                        Assert.Equal(
                            "Collection(NorthwindModel.Sales_by_year_result)",
                            ((SingleItemBinding<ComplexType>)artifact.GetFreshFunctionImport("myfunctionimport").ReturnType)
                                .RefName);

                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            new CreateFunctionImportCommand(
                                artifact.GetFreshConceptualEntityContainer("NorthwindEntities"),
                                artifact.GetFreshStorageFunction("GetFreightCost"),
                                "myfunctionimport2",
                                "String"));

                        Assert.NotNull(artifact.GetFreshFunctionImport("myfunctionimport2"));
                        Assert.Equal(
                            "Collection(String)",
                            ((DefaultableValue<String>)artifact.GetFreshFunctionImport("myfunctionimport2").ReturnType).Value);

                        createComplexTypeCmd = new CreateComplexTypeCommand("GetFreightCost_result", true);
                        commandProcessor.EnqueueCommand(createComplexTypeCmd);
                        commandProcessor.EnqueueCommand(
                            new ChangeFunctionImportCommand(
                                artifact.GetFreshConceptualEntityContainer("NorthwindEntities"),
                                artifact.GetFreshFunctionImport("myfunctionimport2"),
                                artifact.GetFreshStorageFunction("GetFreightCost"),
                                "test123",
                                /* isComposable */ BoolOrNone.FalseValue,
                                createComplexTypeCmd));

                        commandProcessor.Invoke();
                        Assert.Null(artifact.GetFreshFunctionImport("myfunctionimport2"));
                        Assert.NotNull(artifact.GetFreshFunctionImport("test123"));
                        Assert.Equal(
                            "Collection(NorthwindModel.GetFreightCost_result)",
                            ((SingleItemBinding<ComplexType>)artifact.GetFreshFunctionImport("test123").ReturnType).RefName);
                        Assert.NotNull(artifact.GetFreshComplexType("GetFreightCost_result"));

                        // Undo redo "Change FunctionImport"
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.Null(artifact.GetFreshFunctionImport("test123"));
                        Assert.NotNull(artifact.GetFreshFunctionImport("myfunctionimport2"));
                        Assert.Equal(
                            "Collection(String)",
                            ((DefaultableValue<String>)artifact.GetFreshFunctionImport("myfunctionimport2").ReturnType).Value);
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, RedoCommand);
                        Assert.Null(artifact.GetFreshFunctionImport("myfunctionimport2"));
                        Assert.NotNull(artifact.GetFreshFunctionImport("test123"));
                        Assert.Equal(
                            "Collection(NorthwindModel.GetFreightCost_result)",
                            ((SingleItemBinding<ComplexType>)artifact.GetFreshFunctionImport("test123").ReturnType).RefName);
                        Assert.NotNull(artifact.GetFreshComplexType("GetFreightCost_result"));
                    },
                HangMitigatingCancellationToken);
        }

        /// <summary>
        ///     Delete a complex type which is referenced by a ComplexProperty then undo and redo.
        ///     Check that the ComplexProperty's Type parameter is resolved at the appropriate time
        /// </summary>
        [IdeFact]
        public async Task DeleteComplexType()
        {
            const string testName = "UndoRedo.DeleteComplexType";

            await ExecuteUndoRedoTestAsync(
                testName, "NorthwindModel.edmx", async (dte, commandProcessorContext, artifact, cancellationToken) =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            new CreateComplexTypeFromPropertiesCommand(
                                artifact.GetFreshConceptualEntity("Orders"),
                                artifact.GetFreshConceptualEntity("Orders")
                                    .Properties()
                                    .Where(p => p.Name.Value == "ShipName" || p.Name.Value == "ShipAddress")
                                    .ToList()));

                        var complexProperty = (ComplexConceptualProperty)artifact.GetFreshConceptualEntity("Orders")
                                                                                .Properties()
                                                                                .SingleOrDefault(
                                                                                    p => p.Name.Value == "ComplexProperty");
                        Assert.True(complexProperty is not null, "Complex property not created");
                        Assert.Equal("ComplexType1", complexProperty.ComplexType.Target.Name.Value);

                        // Delete the complex type
                        CommandProcessor.InvokeSingleCommand(
                            commandProcessorContext,
                            new DeleteComplexTypeCommand(artifact.GetFreshComplexType("ComplexType1")));

                        Assert.Null(artifact.GetFreshComplexType("ComplexType1"));
                        Assert.NotNull(
                            artifact.GetFreshConceptualEntity("Orders")
                                .Properties()
                                .SingleOrDefault(p => p.Name.Value == "ComplexProperty"));
                        complexProperty = (ComplexConceptualProperty)artifact.GetFreshConceptualEntity("Orders")
                                                                            .Properties()
                                                                            .SingleOrDefault(
                                                                                p => p.Name.Value == "ComplexProperty");
                        Assert.Null(complexProperty.ComplexType.Target);

                        // Undo and test that the ComplexConceptualProperty's ComplexType attribute is re-resolved
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, UndoCommand);
                        Assert.NotNull(artifact.GetFreshComplexType("ComplexType1"));
                        complexProperty = (ComplexConceptualProperty)artifact.GetFreshConceptualEntity("Orders")
                                                                            .Properties()
                                                                            .SingleOrDefault(
                                                                                p => p.Name.Value == "ComplexProperty");
                        Assert.Equal("ComplexType1", complexProperty.ComplexType.Target.Name.Value);
                    },
                HangMitigatingCancellationToken);
        }

        private async Task ExecuteUndoRedoTestAsync(string testName, string modelName, Func<EnvDTE.DTE, CommandProcessorContext, EFArtifact, CancellationToken, Task> runTestAsync, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solnFilePath = Path.Combine(TestContext.DeploymentDirectory, @"TestData\InProc\UndoRedo\UndoRedo.sln");
            var modelFilePath = Path.Combine(TestContext.DeploymentDirectory, @"TestData\InProc\UndoRedo\UndoRedo", modelName);
            var artifactUri = TestUtils.FileName2Uri(modelFilePath);

            var envDTE = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            Assert.NotNull(envDTE);

            envDTE.OpenSolution(solnFilePath);
            envDTE.OpenFile(artifactUri.LocalPath);

            var artifactHelper = new EFArtifactHelper(EFArtifactHelper.GetEntityDesignModelManager(ServiceProvider.GlobalProvider));
            var entityDesignArtifact = (EntityDesignArtifact)artifactHelper.GetNewOrExistingArtifact(artifactUri);

            try
            {
                var editingContext = _package.DocumentFrameMgr.EditingContextManager.GetNewOrExistingContext(entityDesignArtifact.Uri);

                // Add DiagramId information in transactioncontext, so the correct diagram item will be created when Escher model is mutated.
                var transactionContext = new EfiTransactionContext();
                transactionContext.Add(
                    EfiTransactionOriginator.TransactionOriginatorDiagramId,
                    new DiagramContextItem(entityDesignArtifact.DesignerInfo.Diagrams.FirstDiagram.Id.Value));

                await runTestAsync(
                    envDTE,
                    new CommandProcessorContext(editingContext, testName, testName + "Txn", entityDesignArtifact, transactionContext),
                    entityDesignArtifact,
                    cancellationToken);
            }
            finally
            {
                envDTE.CloseDocument(modelFilePath, false);

                if (entityDesignArtifact != null)
                {
                    entityDesignArtifact.Dispose();
                }

                envDTE.CloseSolution(false);
            }
        }

        private static void CreateDefaultEntityType(
            CommandProcessorContext commandProcessorContext, string entityTypeName, string entitySetName)
        {
            CreateEntityTypeCommand.CreateConceptualEntityTypeAndEntitySetAndProperty(
                commandProcessorContext,
                entityTypeName,
                entitySetName,
                /*createKeyProperty*/ true,
                "Id",
                "String",
                ModelConstants.StoreGeneratedPattern_Identity,
                /*uniquifyNames*/ true);
        }
    }
}
