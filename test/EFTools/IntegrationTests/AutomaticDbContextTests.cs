namespace EFDesigner.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Entity.Design.Package;
    using Microsoft.Data.Entity.Design.VisualStudio.ModelWizard;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Extensibility.Testing;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;

    public class AutomaticDbContextTests : AbstractIntegrationTest
    {
        private string ModelEdmxFilePath
        {
            get
            {
                return Path.Combine(TestContext.DeploymentDirectory, @"TestData\Model\v3\Simple.edmx");
            }
        }

        [IdeFact]
        public async Task AddDbContextTemplates_does_not_add_the_template_items_when_the_templates_are_not_installed()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateProjectAsync("NoDbContextTemplates", "3.5", "VisualBasic", HangMitigatingCancellationToken);
            project.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            var edmxItem = project.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.edmx";
                });

            new DbContextCodeGenerator("FakeDbCtx{0}{1}EF5.zip").AddDbContextTemplates(edmxItem);

            Assert.DoesNotContain(
                project.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.tt";
                });
            Assert.DoesNotContain(
                project.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.Context.tt";
                });
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_returns_null_for_project_targeting_dotNET3_5()
        {
            Assert.Null(new DbContextCodeGenerator().FindDbContextTemplate(await CreateProjectAsync("Net35", "3.5", "CSharp", HangMitigatingCancellationToken)));
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_CSharp_template_when_targeting_dotNET4_with_CSharp()
        {
            var proj = await CreateProjectAsync("DbContextCSharpNet40", "4", "CSharp", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxCSEF5\DbContext_CS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_CSharp_template_when_targeting_dotNET4_5_with_CSharp()
        {
            var proj = await CreateProjectAsync("DbContextCSharpNet45", "4.5", "CSharp", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxCSEF5\DbContext_CS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_VB_template_when_targeting_dotNET4_with_VB()
        {
            var proj = await CreateProjectAsync("DbContextVBNet40", "4", "VisualBasic", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxVBEF5\DbContext_VB_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_VB_template_when_targeting_dotNET4_5_with_VB()
        {
            var proj = await CreateProjectAsync("DbContextVBNet45", "4.5", "VisualBasic", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxVBEF5\DbContext_VB_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_CSharp_web_site_template_when_targeting_dotNET4_web_site_with_CSharp()
        {
            var proj = await CreateWebSiteProjectAsync("DbContextCSharpNet40Web", "4", "CSharp", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxCSWSEF5\DbContext_CS_WS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_CSharp_web_site_template_when_targeting_dotNET4_5_web_site_with_CSharp()
        {
            var proj = await CreateWebSiteProjectAsync("DbContextCSharpNet45Web", "4.5", "CSharp", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxCSWSEF5\DbContext_CS_WS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_VB_web_site_template_when_targeting_dotNET4_web_site_with_VB()
        {
            var proj = await CreateWebSiteProjectAsync("DbContextVBNet40Web", "4", "VisualBasic", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxVBWSEF5\DbContext_VB_WS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF5_VB_web_site_template_when_targeting_dotNET4_5_web_site_with_VB()
        {
            var proj = await CreateWebSiteProjectAsync("DbContextVBNet45Web", "4.5", "VisualBasic", HangMitigatingCancellationToken);
            var dbCtxGenerator = new DbContextCodeGenerator();
            var ctxTemplate = dbCtxGenerator.FindDbContextTemplate(proj);
            Assert.EndsWith(@"DbCtxVBWSEF5\DbContext_VB_WS_V5.0.vstemplate", ctxTemplate);
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF6_CSharp_template()
        {
            var project = await CreateProjectAsync("DbContextCSharpNet45EF6", "4.5", "CSharp", HangMitigatingCancellationToken);
            var generator = new DbContextCodeGenerator();

            var template = generator.FindDbContextTemplate(project, useLegacyTemplate: false);

            Assert.EndsWith(@"DbCtxCSEF6\DbContext_CS_V6.0.vstemplate", template);
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF6_VB_template()
        {
            var project = await CreateProjectAsync("DbContextVBNet45EF6", "4.5", "VisualBasic", HangMitigatingCancellationToken);
            var generator = new DbContextCodeGenerator();

            var template = generator.FindDbContextTemplate(project, useLegacyTemplate: false);

            Assert.EndsWith(@"DbCtxVBEF6\DbContext_VB_V6.0.vstemplate", template);
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF6_CSharp_web_site_template()
        {
            var project = await CreateWebSiteProjectAsync("DbContextCSharpNet45WebEF6", "4.5", "CSharp", HangMitigatingCancellationToken);
            var generator = new DbContextCodeGenerator();

            var template = generator.FindDbContextTemplate(project, useLegacyTemplate: false);

            Assert.EndsWith(@"DbCtxCSWSEF6\DbContext_CS_WS_V6.0.vstemplate", template);
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task FindDbContextTemplate_finds_the_EF6_VB_web_site_template()
        {
            var project = await CreateWebSiteProjectAsync("DbContextVBNet45WebEF6", "4.5", "VisualBasic", HangMitigatingCancellationToken);
            var generator = new DbContextCodeGenerator();

            var template = generator.FindDbContextTemplate(project, useLegacyTemplate: false);

            Assert.EndsWith(@"DbCtxVBWSEF6\DbContext_VB_WS_V6.0.vstemplate", template);
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_does_not_add_the_template_items_to_the_item_collection_when_targeting_dotNET3_5()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateProjectAsync("TemplatesNet35", "3.5", "VisualBasic", HangMitigatingCancellationToken);
            var edmxItem = project.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem);

            Assert.DoesNotContain(
                project.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.tt";
                });
            Assert.DoesNotContain(
                project.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.Context.tt";
                });
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_adds_the_template_items_nested_under_the_EDMX_item()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateProjectAsync("TemplatesNet40", "4", "CSharp", HangMitigatingCancellationToken);
            var edmxItem = project.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            edmxItem.Open();

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem);

            var typesT4 = edmxItem.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.tt";
                });
            Assert.EndsWith(@"TemplatesNet40\Simple.tt", typesT4.get_FileNames(1));

            var contextT4 = edmxItem.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.Context.tt";
                });
            Assert.EndsWith(@"TemplatesNet40\Simple.Context.tt", contextT4.get_FileNames(1));
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_does_not_nest_existing_tt_files_or_non_tt_files_added_at_the_same_time_as_the_template_items()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(HangMitigatingCancellationToken);
            var project = await CreateProjectAsync("TemplatesNet40_Nesting", "4", "CSharp", HangMitigatingCancellationToken);
            project.ProjectItems.AddFromTemplate(
                ((EnvDTE80.Solution2)dte.Solution).GetProjectItemTemplate("XMLFile.zip", "CSharp"), "another.tt");
            var edmxItem = project.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            edmxItem.Open();

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem);

            Assert.DoesNotContain(
                edmxItem.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "another.tt";
                });
            Assert.DoesNotContain(
                edmxItem.ProjectItems.OfType<EnvDTE.ProjectItem>(),
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "packages.config";
                });

            var additional = project.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "packages.config";
                });
            Assert.EndsWith(@"TemplatesNet40_Nesting\packages.config", additional.get_FileNames(1));
        }

        // This test requires the EF 5.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_adds_the_template_items_to_the_item_collection_for_a_website_project()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateWebSiteProjectAsync("TemplatesNet45Web", "4.5", "CSharp", HangMitigatingCancellationToken);
            var appCode = project.ProjectItems.AddFolder("App_Code");
            var edmxItem = appCode.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            edmxItem.Open();

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem);

            var typesT4 = appCode.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.tt";
                });
            Assert.EndsWith(@"TemplatesNet45Web\App_Code\Simple.tt", typesT4.get_FileNames(1));

            var contextT4 = appCode.ProjectItems.OfType<EnvDTE.ProjectItem>().Single(
                i =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return i.Name == "Simple.Context.tt";
                });
            Assert.EndsWith(@"TemplatesNet45Web\App_Code\Simple.Context.tt", contextT4.get_FileNames(1));
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_is_noop_when_called_more_than_once()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateProjectAsync("TemplatesNet45Twice", "4.5", "CSharp", HangMitigatingCancellationToken);
            var edmxItem = project.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            edmxItem.Open();

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem, useLegacyTemplate: false);
            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem, useLegacyTemplate: false);

            Assert.Subset(
                new HashSet<string> { "Simple.tt", "Simple.Context.tt" },
                (edmxItem.ProjectItems ?? edmxItem.Collection).Cast<EnvDTE.ProjectItem>()
                    .Select(
                        i =>
                        {
                            ThreadHelper.ThrowIfNotOnUIThread();
                            return i.Name;
                        })
                    .ToHashSet());
        }

        // This test requires the EF 6.x DbContext item templates to be installed.
        [IdeFact]
        public async Task AddDbContextTemplates_is_noop_when_called_more_than_once_and_website()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var project = await CreateWebSiteProjectAsync("TemplatesNet45TwiceWeb", "4.5", "CSharp", HangMitigatingCancellationToken);
            var appCode = project.ProjectItems.AddFolder("App_Code");
            var edmxItem = appCode.ProjectItems.AddFromFileCopy(ModelEdmxFilePath);
            edmxItem.Open();

            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem, useLegacyTemplate: false);
            new DbContextCodeGenerator().AddDbContextTemplates(edmxItem, useLegacyTemplate: false);

            Assert.Subset(
                new HashSet<string> { "Simple.tt", "Simple.Context.tt" },
                appCode.ProjectItems.Cast<EnvDTE.ProjectItem>()
                    .Select(
                        i =>
                        {
                            ThreadHelper.ThrowIfNotOnUIThread();
                            return i.Name;
                        })
                    .ToHashSet());
        }

        [IdeFact]
        public void AddAndNestCodeGenTemplates_does_not_fail_if_EDMX_project_item_is_null()
        {
            var run = false;
            DbContextCodeGenerator.AddAndNestCodeGenTemplates(null, () => run = true);
            Assert.True(run);
        }

        [IdeFact]
        public async Task AddNewItemDialogFilter_only_accepts_items_with_file_names_for_well_known_template_types()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(HangMitigatingCancellationToken);

            var _ = new Guid();
            int filterResult;
            var filter = new MicrosoftDataEntityDesignCommandSet.AddNewItemDialogFilter();

            new List<string>
                {
                    @"C:\Some Templates\ADONETArtifactGenerator_OldSchool.vstemplate",
                    @"C:\Some Templates\DbContext_InTheBox.vstemplate",
                }.ForEach(
                    f =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        Assert.Equal(VSConstants.S_OK, filter.FilterListItemByTemplateFile(ref _, f, out filterResult));
                        Assert.Equal(0, filterResult);
                    });

            Assert.Equal(
                VSConstants.S_OK,
                filter.FilterListItemByTemplateFile(ref _, @"C:\Some Templates\Not An EF Template.vstemplate", out filterResult));
            Assert.Equal(1, filterResult);
        }

        private Task<EnvDTE.Project> CreateProjectAsync(string projectName, string targetFramework, string projectLanguage, CancellationToken cancellationToken)
        {
            return CreateProjectAsync(
                projectName,
                dte =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return ((EnvDTE80.Solution2)dte.Solution).GetProjectTemplate("ConsoleApplication.zip|FrameworkVersion=" + targetFramework, projectLanguage);
                },
                cancellationToken);
        }

        private Task<EnvDTE.Project> CreateWebSiteProjectAsync(string projectName, string targetFramework, string projectLanguage, CancellationToken cancellationToken)
        {
            return CreateProjectAsync(
                projectName,
                dte =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return ((EnvDTE80.Solution2)dte.Solution).GetProjectTemplate("EmptyWeb.zip|FrameworkVersion=" + targetFramework, @"Web\" + projectLanguage);
                },
                cancellationToken);
        }

        private async Task<EnvDTE.Project> CreateProjectAsync(string projectName, Func<EnvDTE.DTE, string> getTemplate, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var solutionPath = Directory.GetParent(TestContext.DeploymentDirectory).FullName;

            if (!await TestServices.SolutionExplorer.IsSolutionOpenAsync(cancellationToken))
            {
                dte.Solution.Create(solutionPath, "AutomaticDbContextTests");
            }

            var projectDir = Path.Combine(solutionPath, projectName);
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, true);
            }

            dte.Solution.AddFromTemplate(getTemplate(dte), projectDir, projectName, false);

            return dte.Solution.Projects.OfType<EnvDTE.Project>().First(
                p =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return p.Name.Contains(projectName);
                });
        }
    }
}
