﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using RazorGenerator.Mvc;
using Telerik.Microsoft.Practices.Unity;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Abstractions.VirtualPath;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Frontend.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers.Attributes;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Routing;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Frontend.Resources.Resolvers;
using Telerik.Sitefinity.Localization;
using Telerik.Sitefinity.Modules.ControlTemplates;
using Telerik.Sitefinity.Mvc;
using Telerik.Sitefinity.Mvc.Proxy.TypeDescription;
using Telerik.Sitefinity.Mvc.Store;
using Telerik.Sitefinity.Pages;
using Telerik.Sitefinity.Services;

namespace Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers
{
    /// <summary>
    /// This class contains logic for locating and initializing controllers.
    /// </summary>
    internal class ControllerContainerInitializer : IInitializer
    {
        #region Public members

        /// <summary>
        /// Gets the controller container assemblies.
        /// </summary>
        /// <value>
        /// The controller container assemblies.
        /// </value>
        public IEnumerable<Assembly> ControllerContainerAssemblies
        {
            get
            {
                if (ControllerContainerInitializer.controllerContainerAssemblies == null)
                {
                    lock (ControllerContainerInitializer.ControllerContainerAssembliesLock)
                    {
                        if (ControllerContainerInitializer.controllerContainerAssemblies == null)
                        {
                            ControllerContainerInitializer.controllerContainerAssemblies = this.RetrieveAssemblies();
                        }
                    }
                }

                return ControllerContainerInitializer.controllerContainerAssemblies;
            }

            private set
            {
                lock (ControllerContainerInitializer.ControllerContainerAssembliesLock)
                {
                    ControllerContainerInitializer.controllerContainerAssemblies = value;
                }
            }
        }

        /// <summary>
        /// Registers the string resources.
        /// </summary>
        public void RegisterStringResources()
        {
            this.ControllerContainerAssemblies
                .SelectMany(asm => asm.GetExportedTypes().Where(FrontendManager.ControllerFactory.IsController))
                .ToList()
                .ForEach(ControllerContainerInitializer.RegisterStringResources);
        }

        /// <summary>
        /// Initializes the controllers that are available to the web application.
        /// </summary>
        public virtual void Initialize()
        {
            GlobalFilters.Filters.Add(new CacheDependentAttribute());

            this.RegisterVirtualPaths(this.ControllerContainerAssemblies);

            var controllerTypes = this.GetControllers(this.ControllerContainerAssemblies);
            this.InitializeControllers(controllerTypes);

            this.InitializeCustomRouting();

            this.RegisterPrecompiledViewEngines(this.ControllerContainerAssemblies);
        }

        /// <summary>
        /// Uninitializes the controllers that are available to the web application.
        /// </summary>
        public virtual void Uninitialize()
        {
            this.RegisterStringResources();

            this.UninitializeGlobalFilters();

            foreach (var assembly in this.ControllerContainerAssemblies)
            {
                this.UninitializeControllerContainer(assembly);
            }

            this.ControllerContainerAssemblies = null;

            // Clears all controllers
            foreach (var ctrl in ControllerStore.Controllers().ToList())
            {
                ControllerStore.RemoveController(ctrl.ControllerType);
            }
        }

        /// <summary>
        /// Gets the assemblies that are marked as controller containers with the <see cref="ControllerContainerAttribute"/> attribute.
        /// </summary>
        public virtual IEnumerable<Assembly> RetrieveAssemblies()
        {
            var assemblyFileNames = this.RetrieveAssembliesFileNames().ToArray();
            var result = new List<Assembly>();

            foreach (var assemblyFileName in assemblyFileNames)
            {
                if (this.IsControllerContainer(assemblyFileName))
                {
                    var assembly = this.LoadAssembly(assemblyFileName);
                    this.InitializeControllerContainer(assembly);

                    result.Add(assembly);
                }

                if (this.IsMarkedAssembly<ResourcePackageAttribute>(assemblyFileName))
                {
                    result.Add(this.LoadAssembly(assemblyFileName));
                }
            }

            return result;
        }

        #endregion

        #region Protected members

        /// <summary>
        /// Executes the initialization method specified in the <see cref="ControllerContainerAttribute"/> attribute.
        /// </summary>
        /// <param name="container"></param>
        protected virtual void InitializeControllerContainer(Assembly container)
        {
            if (container == null)
                throw new ArgumentNullException("container");

            var containerAttribute = container.GetCustomAttributes(false).Single(attr => attr.GetType().AssemblyQualifiedName == typeof(ControllerContainerAttribute).AssemblyQualifiedName) as ControllerContainerAttribute;

            if (containerAttribute.InitializationType == null || containerAttribute.InitializationMethod.IsNullOrWhitespace())
                return;

            var initializationMethod = containerAttribute.InitializationType.GetMethod(containerAttribute.InitializationMethod);
            initializationMethod.Invoke(null, null);
        }

        /// <summary>
        /// Executes the uninitialization method specified in the <see cref="ControllerContainerAttribute"/> attribute.
        /// </summary>
        /// <param name="container"></param>
        protected virtual void UninitializeControllerContainer(Assembly container)
        {
            if (container == null)
                throw new ArgumentNullException("container");

            var containerAttribute = container.GetCustomAttributes(false).SingleOrDefault(attr => attr.GetType().AssemblyQualifiedName == typeof(ControllerContainerAttribute).AssemblyQualifiedName) as ControllerContainerAttribute;

            if (containerAttribute == null || containerAttribute.UninitializationType == null || containerAttribute.UninitializationMethod.IsNullOrWhitespace())
                return;

            var uninitializationMethod = containerAttribute.UninitializationType.GetMethod(containerAttribute.UninitializationMethod);
            uninitializationMethod.Invoke(null, null);
        }

        /// <summary>
        /// Loads the assembly file into the current application domain.
        /// </summary>
        /// <param name="assemblyFileName">Name of the assembly file.</param>
        /// <returns>The loaded assembly</returns>
        protected virtual Assembly LoadAssembly(string assemblyFileName)
        {
            return Assembly.LoadFrom(assemblyFileName);
        }

        /// <summary>
        /// Registers virtual paths for the given <paramref name="assemblies"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies.</param>
        protected virtual void RegisterVirtualPaths(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                this.RegisterAssemblyPaths(assembly);
            }
        }

        /// <summary>
        /// Gets the controllers from the given <paramref name="assemblies"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies.</param>
        /// <returns></returns>
        protected virtual IEnumerable<Type> GetControllers(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .SelectMany(asm => asm.GetExportedTypes().Where(FrontendManager.ControllerFactory.IsController))
                .ToArray();
        }

        /// <summary>
        /// Initializes the specified <paramref name="controllers"/> by ensuring they have their proper registrations in the toolbox and that the controller factory will be able to resolve them.
        /// </summary>
        /// <param name="controllers">The controllers.</param>
        protected virtual void InitializeControllers(IEnumerable<Type> controllers)
        {
            this.RegisterTemplateableControls(controllers);
            this.RegisterControllerFactory();
            this.RemoveSitefinityViewEngine();
            this.ReplaceControllerFactory();

            foreach (var controller in controllers)
            {
                this.RegisterController(controller);
                ControllerContainerInitializer.RegisterStringResources(controller);
            }
        }

        protected virtual void UninitializeGlobalFilters()
        {
            var cacheDependentAttribute = GlobalFilters.Filters.FirstOrDefault(f => f.Instance.GetType().FullName == typeof(CacheDependentAttribute).FullName);
            if (cacheDependentAttribute != null)
                GlobalFilters.Filters.Remove(cacheDependentAttribute.Instance);
        }

        /// <summary>
        /// Registers <see cref="FrontendControllerFactory"/>
        /// </summary>
        protected virtual void RegisterControllerFactory()
        {
            ObjectFactory.Container.RegisterType<ISitefinityControllerFactory, FrontendControllerFactory>(new ContainerControlledLifetimeManager());
        }

        /// <summary>
        /// Registers the controller so its views and resources can be resolved.
        /// </summary>
        /// <param name="controller">The controller.</param>
        protected virtual void RegisterController(Type controller)
        {
            var controllerStore = new ControllerStore();
            var configManager = ConfigManager.GetManager();

            using (var modeRegion = new ElevatedConfigModeRegion())
            {
                controllerStore.AddController(controller, configManager);
            }
        }

        /// <summary>
        /// Gets the assemblies file names that will be inspected for controllers.
        /// </summary>
        protected virtual IEnumerable<string> RetrieveAssembliesFileNames()
        {
            var controllerAssemblyPath = Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "bin");
            return Directory.EnumerateFiles(controllerAssemblyPath, "*.dll", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Determines whether the given <paramref name="assemblyFileName"/> is an assembly file that is marked as <see cref="ContainerControllerAttribute"/>.
        /// </summary>
        /// <param name="assemblyFileName">Filename of the assembly.</param>
        /// <returns>True if the given file name is of an assembly file that is marked as <see cref="ContainerControllerAttribute"/>, false otherwise.</returns>
        protected virtual bool IsControllerContainer(string assemblyFileName)
        {
            return this.IsMarkedAssembly<ControllerContainerAttribute>(assemblyFileName);
        }

        /// <summary>
        /// Replaces the current controller factory used by the MVC framework with the factory that is registered by Sitefinity.
        /// </summary>
        protected virtual void ReplaceControllerFactory()
        {
            var factory = ObjectFactory.Resolve<ISitefinityControllerFactory>();
            ControllerBuilder.Current.SetControllerFactory(factory);
        }

        /// <summary>
        /// Registers types of the custom routing.
        /// </summary>
        protected virtual void InitializeCustomRouting()
        {
            ObjectFactory.Container.RegisterType<IControllerActionInvoker, FeatherActionInvoker>();
            ObjectFactory.Container.RegisterType<IRouteParamResolver, IntParamResolver>("int");
            ObjectFactory.Container.RegisterType<IRouteParamResolver, CategoryParamResolver>("category");
            ObjectFactory.Container.RegisterType<IRouteParamResolver, TagParamResolver>("tag");

            string mvcControllerProxySettingsPropertyDescriptorName = string.Format("{0}.{1}", typeof(MvcWidgetProxy).FullName, "Settings");
            ObjectFactory.Container.RegisterType<IControlPropertyDescriptor, ControllerSettingsPropertyDescriptor>(mvcControllerProxySettingsPropertyDescriptorName);

            FrontendManager.AttributeRouting.MapMvcAttributeRoutes();
        }

        #endregion

        #region Private members
        
        /// <summary>
        /// Registers the controller string resources.
        /// </summary>
        /// <param name="controller">Type of the controller.</param>
        private static void RegisterStringResources(Type controller)
        {
            var localizationAttributes = controller.GetCustomAttributes(typeof(LocalizationAttribute), true);
            foreach (var attribute in localizationAttributes)
            {
                var localAttr = (LocalizationAttribute)attribute;
                var resourceClass = localAttr.ResourceClass;
                var resourceClassId = Res.GetResourceClassId(resourceClass);

                if (!ObjectFactory.Container.IsRegistered(resourceClass, resourceClassId))
                {
                    Res.RegisterResource(resourceClass);
                }
            }
        }

        /// <summary>
        /// Determines whether the specified controller is allowed template registration
        /// </summary>
        /// <param name="controllerType">Type of the controller.</param>
        /// <returns>
        ///   <c>true</c> if specified controller is allowed template registration; otherwise, <c>false</c>.
        /// </returns>
        private bool IsTemplatableControl(Type controllerType)
        {
            ControllerMetadataAttribute attr;
            var attributes = controllerType.GetCustomAttributes(typeof(ControllerMetadataAttribute), false);

            if (attributes != null && attributes.Length > 0)
            {
                attr = (ControllerMetadataAttribute)attributes[0];
                return attr.IsTemplatableControl;
            }

            // if there is no ControllerMetaDataAttribute, by default allow template registration
            return true;
        }

        /// <summary>
        /// Determines whether the specified assembly file name is marked with a given attribute.
        /// </summary>
        /// <typeparam name="TAttribute">The type of the attribute.</typeparam>
        /// <param name="assemblyFileName">File name of the assembly.</param>
        /// <returns>True if the assembly has the given attribute.</returns>
        private bool IsMarkedAssembly<TAttribute>(string assemblyFileName)
            where TAttribute : Attribute
        {
            if (assemblyFileName == null)
                return false;

            bool result;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += this.CurrentDomain_ReflectionOnlyAssemblyResolve;
            try
            {
                try
                {
                    var reflOnlyAssembly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);

                    result = reflOnlyAssembly != null &&
                            reflOnlyAssembly.GetCustomAttributesData()
                                .Any(d => d.Constructor.DeclaringType.AssemblyQualifiedName == typeof(TAttribute).AssemblyQualifiedName);
                }
                catch (IOException)
                {
                    // We might not be able to load some .DLL files as .NET assemblies. Those files cannot contain controllers.
                    result = false;
                }
                catch (BadImageFormatException)
                {
                    result = false;
                }
            }
            finally
            {
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= this.CurrentDomain_ReflectionOnlyAssemblyResolve;
            }

            return result;
        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assWithPolicy = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

            return Assembly.ReflectionOnlyLoad(assWithPolicy);
        }

        private void RegisterAssemblyPaths(Assembly assembly)
        {
            var assemblyName = assembly.GetName();
            var virtualPath = FrontendManager.VirtualPathBuilder.GetVirtualPath(assembly);

            VirtualPathManager.AddVirtualFileResolver<ResourceResolver>(
                                                                        string.Format(CultureInfo.InvariantCulture, "~/{0}*", virtualPath),
                                                                        assemblyName.Name,
                                                                        assemblyName.CodeBase);

            SystemManager.RegisterRoute(
                                        assemblyName.Name,
                                        new Route(
                                                  virtualPath + "{*Params}",
                                                  new GenericRouteHandler<ResourceHttpHandler>(() => new ResourceHttpHandler(virtualPath))),
                                                  assemblyName.Name,
                                                  requireBasicAuthentication: false);
        }

        /// <summary>
        /// Registers MVC widgets as templatable controls
        /// </summary>
        /// <param name="controllerTypes">The controller types.</param>
        private void RegisterTemplateableControls(IEnumerable<Type> controllerTypes)
        {
            controllerTypes = controllerTypes.Where(x => this.IsTemplatableControl(x));

            foreach (Type controllerType in controllerTypes)
            {
                var widgetName = controllerType.Name.Replace("Controller", string.Empty);
                var mvcWidgetName = string.Format(CultureInfo.InvariantCulture, "{0} (MVC)", widgetName);

                ControlTemplates.RegisterTemplatableControl(controllerType, controllerType, string.Empty, widgetName, mvcWidgetName);
            }
        }

        private void RemoveSitefinityViewEngine()
        {
            var sitefinityViewEngines = ViewEngines.Engines.Where(v => v != null && v.GetType() == typeof(SitefinityViewEngine)).ToList();
            foreach (var sitefinityViewEngine in sitefinityViewEngines)
            {
                ViewEngines.Engines.Remove(sitefinityViewEngine);
            }
        }

        private static IEnumerable<Assembly> controllerContainerAssemblies;
        private static readonly object ControllerContainerAssembliesLock = new object();

        private Dictionary<string, List<PrecompiledViewAssemblyWrapper>> PrecompiledResourcePackages(IEnumerable<Assembly> assemblies)
        {
            var precompiledViewAssemblies = new Dictionary<string, List<PrecompiledViewAssemblyWrapper>>();
            foreach (var assembly in assemblies)
            {
                var package = this.AssemblyPackage(assembly);
                if (package == null)
                    continue;

                if (!precompiledViewAssemblies.ContainsKey(package))
                    precompiledViewAssemblies[package] = new List<PrecompiledViewAssemblyWrapper>();

                precompiledViewAssemblies[package].Add(new PrecompiledViewAssemblyWrapper(assembly, package));
            }

            return precompiledViewAssemblies;
        }

        private void RegisterPrecompiledViewEngines(IEnumerable<Assembly> assemblies)
        {
            var precompiledAssemblies = assemblies.Where(a => a.GetCustomAttribute<ControllerContainerAttribute>() != null).Select(a => new PrecompiledViewAssemblyWrapper(a, null)).ToArray();
            if (precompiledAssemblies.Length > 0)
            {
                ViewEngines.Engines.Insert(0, new CompositePrecompiledMvcEngineWrapper(precompiledAssemblies));
            }

            var precompiledResourcePackages = this.PrecompiledResourcePackages(assemblies);
            foreach (var package in precompiledResourcePackages.Keys)
            {
                if (package == null)
                    continue;

                var packageAssemblies = precompiledResourcePackages[package];
                if (packageAssemblies.Count > 0)
                    ViewEngines.Engines.Insert(0, new CompositePrecompiledMvcEngineWrapper(packageAssemblies, null, package));
            }
        }

        private string AssemblyPackage(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<ResourcePackageAttribute>();
            if (attribute == null)
                return null;
            else
                return attribute.Name;
        }

        #endregion
    }
}
