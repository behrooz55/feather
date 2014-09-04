﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MbUnit.Framework;
using Telerik.Sitefinity.Frontend.TestUtilities.CommonOperations;
using Telerik.Sitefinity.Modules.Pages;
using Telerik.Sitefinity.Pages.Model;
using Telerik.Sitefinity.TestIntegration.Helpers;
using Telerik.Sitefinity.TestUtilities.CommonOperations;
using Telerik.Sitefinity.Web;
using Telerik.Sitefinity.Workflow;

namespace Telerik.Sitefinity.Frontend.TestIntegration.ResourcePackages
{
    [TestFixture]
    [Description("This is a class with tests related to layout files in feather resource packages.")]
    public class LayoutFilesTests
    {
        [Test]
        [Category(TestCategories.LayoutFiles)]
        [Author("Petya Rachina")]
        [Description("Adds new layout file to existing resource package, verifies the automatically generated page template and creates a page based on the template in order to verify the content.")]
        public void FoundationResourcePackage_AddNewLayoutFile_VerifyGeneratedTemplateAndCreatedPageContent()
        {
            int templatesCount = this.PageManager.GetTemplates().Count();

            try
            {
                string filePath = FeatherServerOperations.ResourcePackages().GetResourcePackageDestinationFilePath(Constants.PackageName, Constants.LayoutFileName);
                FeatherServerOperations.ResourcePackages().AddNewResource(Constants.LayoutFileResource, filePath);

                FeatherServerOperations.ResourcePackages().WaitForTemplatesCountToIncrease(templatesCount, 1);

                var template = this.PageManager.GetTemplates().Where(t => t.Title == Constants.TemplateTitle).FirstOrDefault();
                Assert.IsNotNull(template, "Template was not found");

                Guid pageId = FeatherServerOperations.Pages().CreatePageWithTemplate(template, Constants.PageTitle, Constants.PageUrl);

                var page = this.PageManager.GetPageNode(pageId);
                var pageUrl = page.GetFullUrl();
                pageUrl = RouteHelper.GetAbsoluteUrl(pageUrl);

                string pageContent = WebRequestHelper.GetPageWebContent(pageUrl);
                Assert.IsTrue(pageContent.Contains(Constants.LayoutTemplateText), "Layout template text was not found in the page content");
            }
            finally
            {
                ServerOperations.Pages().DeletePage(Constants.PageTitle);
                ServerOperations.Templates().DeletePageTemplate(Constants.TemplateTitle);

                string filePath = FeatherServerOperations.ResourcePackages().GetResourcePackageDestinationFilePath(Constants.PackageName, Constants.LayoutFileName);
                File.Delete(filePath);
            }
        }

        [Test]
        [Category(TestCategories.LayoutFiles)]
        [Author("Petya Rachina")]
        [Description("Adds a resource package with layout files, renames one of the layout files and verify that new template is generated.")]
        public void ResourcePackageLayoutFiles_RenameLayoutFile_VerifyGeneratedTemplate()
        {
            int templatesCount = this.PageManager.GetTemplates().Count();

            try
            {
                FeatherServerOperations.ResourcePackages().AddNewResourcePackage(Constants.PackageResource);
                FeatherServerOperations.ResourcePackages().WaitForTemplatesCountToIncrease(templatesCount, 3);

                string[] templateTitles = new string[] { Constants.TemplateTestLayout1, Constants.TemplateTestLayout2, Constants.TemplateTestLayout3 };

                foreach (var title in templateTitles)
                {
                    var template = this.PageManager.GetTemplates().Where(t => t.Title == title).FirstOrDefault();
                    Assert.IsNotNull(template, "Template was not found");
                }

                FeatherServerOperations.ResourcePackages().RenameLayoutFile(Constants.TestPackageName, Constants.TestLayoutFileName, Constants.NewTestLayoutFileName);
                FeatherServerOperations.ResourcePackages().WaitForTemplatesCountToIncrease(templatesCount, 4);

                var renamedTemplate = this.PageManager.GetTemplates().Where(t => t.Title == Constants.TemplateTestLayout1Renamed).FirstOrDefault();
                Assert.IsNotNull(renamedTemplate, "Template was not found");
            }
            finally
            {
                string[] templates = new string[] { Constants.TemplateTestLayout1, Constants.TemplateTestLayout2, Constants.TemplateTestLayout3, Constants.TemplateTestLayout1Renamed };

                foreach (var template in templates)
                {
                    ServerOperations.Templates().DeletePageTemplate(template);
                }

                string path = FeatherServerOperations.ResourcePackages().GetResourcePackagesDestination(Constants.TestPackageName);
                Directory.Delete(path, true);
            }
        }

        [Test]
        [Category(TestCategories.LayoutFiles)]
        [Author("Petya Rachina")]
        [Description("Adds a resource package with layout files, delete one of the layout files and verify that the template is no longer based on it.")]
        public void ResourcePackageLayoutFiles_DeleteLayoutFile_VerifyTemplateAndPageNotBasedToLayout()
        {
            int templatesCount = this.PageManager.GetTemplates().Count();

            try
            {
                FeatherServerOperations.ResourcePackages().AddNewResourcePackage(Constants.PackageResource);
                FeatherServerOperations.ResourcePackages().WaitForTemplatesCountToIncrease(templatesCount, 3);

                var template = this.PageManager.GetTemplates().Where(t => t.Title == Constants.TemplateTestLayout1).FirstOrDefault();

                if (template == null)
                    throw new ArgumentException("template not found");

                Guid pageId = FeatherServerOperations.Pages().CreatePageWithTemplate(template, Constants.PageTitle, Constants.PageUrl);

                var page = this.PageManager.GetPageNode(pageId);
                var pageUrl = page.GetFullUrl();
                pageUrl = RouteHelper.GetAbsoluteUrl(pageUrl);

                var pageContent = WebRequestHelper.GetPageWebContent(pageUrl);
                Assert.IsTrue(pageContent.Contains(Constants.TestLayout1TemplateText), "Layout template text was not found in the page content");

                string filePath = FeatherServerOperations.ResourcePackages().GetResourcePackageDestinationFilePath(Constants.TestPackageName, Constants.TestLayoutFileName);
                File.Delete(filePath);

                template = this.PageManager.GetTemplates().Where(t => t.Title == Constants.TemplateTestLayout1).FirstOrDefault();
                Assert.IsNotNull(template, "Template was not found after layout file was deleted.");

                this.PublishPage(page);
                pageContent = WebRequestHelper.GetPageWebContent(pageUrl);

                Assert.IsFalse(pageContent.Contains(Constants.ServerErrorMessage), "Page throws a server error message");
                Assert.IsFalse(pageContent.Contains(Constants.TestLayout1TemplateText), "Layout template text was found in the page content");              
            }
            finally
            {
                string[] templates = new string[] { Constants.TemplateTestLayout1, Constants.TemplateTestLayout2, Constants.TemplateTestLayout3, Constants.TemplateTestLayout1Renamed };

                ServerOperations.Pages().DeletePage(Constants.PageTitle);

                foreach (var template in templates)
                {
                    ServerOperations.Templates().DeletePageTemplate(template);
                }

                string path = FeatherServerOperations.ResourcePackages().GetResourcePackagesDestination(Constants.TestPackageName);
                Directory.Delete(path, true);
            }
        }

        private PageManager pageManager;

        private PageManager PageManager
        {
            get
            {
                if (this.pageManager == null)
                {
                    this.pageManager = PageManager.GetManager();
                }

                return this.pageManager;
            }
        }

        private void PublishPage(PageNode page)
        {
            var pageData = page.GetPageData();
            var master = this.PageManager.PagesLifecycle.GetMaster(pageData);
            this.PageManager.PagesLifecycle.Publish(master);
            this.PageManager.SaveChanges();
        }
    }
}