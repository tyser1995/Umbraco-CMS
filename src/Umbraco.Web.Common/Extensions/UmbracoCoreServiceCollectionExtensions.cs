using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Extensions.Hosting;
using Serilog.Extensions.Logging;
using Umbraco.Configuration;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.Models;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Logging.Serilog;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Runtime;
using Umbraco.Web.Common.AspNetCore;
using Umbraco.Web.Common.Profiler;
using ConnectionStrings = Umbraco.Core.Configuration.Models.ConnectionStrings;
using CoreDebugSettings = Umbraco.Core.Configuration.Models.CoreDebugSettings;

namespace Umbraco.Extensions
{
    public static class UmbracoCoreServiceCollectionExtensions
    {
        /// <summary>
        /// Adds SqlCe support for Umbraco
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoSqlCeSupport(this IServiceCollection services)
        {
            try
            {
                var binFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (binFolder != null)
                {
                    var dllPath = Path.Combine(binFolder, "Umbraco.Persistance.SqlCe.dll");
                    var umbSqlCeAssembly = Assembly.LoadFrom(dllPath);

                    var sqlCeSyntaxProviderType = umbSqlCeAssembly.GetType("Umbraco.Persistance.SqlCe.SqlCeSyntaxProvider");
                    var sqlCeBulkSqlInsertProviderType = umbSqlCeAssembly.GetType("Umbraco.Persistance.SqlCe.SqlCeBulkSqlInsertProvider");
                    var sqlCeEmbeddedDatabaseCreatorType = umbSqlCeAssembly.GetType("Umbraco.Persistance.SqlCe.SqlCeEmbeddedDatabaseCreator");

                    if (!(sqlCeSyntaxProviderType is null || sqlCeBulkSqlInsertProviderType is null || sqlCeEmbeddedDatabaseCreatorType is null))
                    {
                        services.AddSingleton(typeof(ISqlSyntaxProvider), sqlCeSyntaxProviderType);
                        services.AddSingleton(typeof(IBulkSqlInsertProvider), sqlCeBulkSqlInsertProviderType);
                        services.AddSingleton(typeof(IEmbeddedDatabaseCreator), sqlCeEmbeddedDatabaseCreatorType);
                    }

                    var sqlCeAssembly = Assembly.LoadFrom(Path.Combine(binFolder, "System.Data.SqlServerCe.dll"));

                    var sqlCe = sqlCeAssembly.GetType("System.Data.SqlServerCe.SqlCeProviderFactory");
                    if (!(sqlCe is null))
                    {
                        DbProviderFactories.RegisterFactory(Core.Constants.DbProviderNames.SqlCe, sqlCe);
                    }
                }
            }
            catch
            {
                // Ignore if SqlCE is not available
            }

            return services;
        }

        /// <summary>
        /// Adds Sql Server support for Umbraco
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoSqlServerSupport(this IServiceCollection services)
        {
            DbProviderFactories.RegisterFactory(Core.Constants.DbProviderNames.SqlServer, SqlClientFactory.Instance);

            services.AddSingleton<ISqlSyntaxProvider, SqlServerSyntaxProvider>();
            services.AddSingleton<IBulkSqlInsertProvider, SqlServerBulkSqlInsertProvider>();
            services.AddSingleton<IEmbeddedDatabaseCreator, NoopEmbeddedDatabaseCreator>();

            return services;
        }

        /// <summary>
        /// Adds the Umbraco Configuration requirements
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            services.Configure<ActiveDirectorySettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "ActiveDirectory"));
            services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"), o => o.BindNonPublicProperties = true);
            services.Configure<ContentSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Content"));
            services.Configure<CoreDebugSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Core:Debug"));
            services.Configure<ExceptionFilterSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "ExceptionFilter"));
            services.Configure<GlobalSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Global"));
            services.Configure<HealthChecksSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "HealthChecks"));
            services.Configure<HostingSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Hosting"));
            services.Configure<ImagingSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Imaging"));
            services.Configure<IndexCreatorSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Examine"));
            services.Configure<KeepAliveSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "KeepAlive"));
            services.Configure<LoggingSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Logging"));
            services.Configure<MemberPasswordConfigurationSettings>(configuration.GetSection(Constants.Configuration.ConfigSecurityPrefix + "MemberPassword"));
            services.Configure<ModelsBuilderConfig>(configuration.GetSection(Constants.Configuration.ConfigGlobalPrefix + "ModelsBuilder"));
            services.Configure<NuCacheSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "NuCache"));
            services.Configure<RequestHandlerSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "RequestHandler"));
            services.Configure<RuntimeSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Runtime"));
            services.Configure<SecuritySettings>(configuration.GetSection(Constants.Configuration.ConfigSecurityPrefix));
            services.Configure<TourSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "Tours"));
            services.Configure<TypeFinderSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "TypeFinder"));
            services.Configure<UserPasswordConfigurationSettings>(configuration.GetSection(Constants.Configuration.ConfigSecurityPrefix + "UserPassword"));
            services.Configure<WebRoutingSettings>(configuration.GetSection(Constants.Configuration.ConfigPrefix + "WebRouting"));

            // TODO: remove this once no longer requred in Umbraco.Web.
            var configsFactory = new AspNetCoreConfigsFactory(configuration);
            var configs = configsFactory.Create();
            services.AddSingleton(configs);

            return services;
        }

        /// <summary>
        /// Adds the Umbraco Back Core requirements
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webHostEnvironment"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoCore(this IServiceCollection services, IWebHostEnvironment webHostEnvironment)
        {
            return services.AddUmbracoCore(webHostEnvironment, out _);
        }

        /// <summary>
        /// Adds the Umbraco Back Core requirements
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webHostEnvironment"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoCore(this IServiceCollection services, IWebHostEnvironment webHostEnvironment, out IFactory factory)
        {
            if (!UmbracoServiceProviderFactory.IsActive)
                throw new InvalidOperationException("Ensure to add UseUmbraco() in your Program.cs after ConfigureWebHostDefaults to enable Umbraco's service provider factory");

            var umbContainer = UmbracoServiceProviderFactory.UmbracoContainer;

            var loggingConfig = new LoggingConfiguration(
                Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", "Logs"),
                Path.Combine(webHostEnvironment.ContentRootPath, "config", "serilog.config"),
                Path.Combine(webHostEnvironment.ContentRootPath, "config", "serilog.user.config"));

            IHttpContextAccessor httpContextAccessor = new HttpContextAccessor();
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
            var requestCache = new GenericDictionaryRequestAppCache(() => httpContextAccessor.HttpContext?.Items);

            services.AddUmbracoCore(webHostEnvironment,
                umbContainer,
                Assembly.GetEntryAssembly(),
                requestCache,
                loggingConfig,
                out factory);

            return services;
        }

        /// <summary>
        /// Adds the Umbraco Back Core requirements
        /// </summary>
        /// <param name="services"></param>
        /// <param name="webHostEnvironment"></param>
        /// <param name="umbContainer"></param>
        /// <param name="entryAssembly"></param>
        /// <param name="requestCache"></param>
        /// <param name="httpContextAccessor"></param>
        /// <param name="loggingConfiguration"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static IServiceCollection AddUmbracoCore(
            this IServiceCollection services,
            IWebHostEnvironment webHostEnvironment,
            IRegister umbContainer,
            Assembly entryAssembly,
            IRequestCache requestCache,
            ILoggingConfiguration loggingConfiguration,
            out IFactory factory)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            var container = umbContainer;
            if (container is null) throw new ArgumentNullException(nameof(container));
            if (entryAssembly is null) throw new ArgumentNullException(nameof(entryAssembly));

            // Add supported databases
            services.AddUmbracoSqlCeSupport();
            services.AddUmbracoSqlServerSupport();

            services.AddSingleton<IDbProviderFactoryCreator>(x => new DbProviderFactoryCreator(
                DbProviderFactories.GetFactory,
                x.GetServices<ISqlSyntaxProvider>(),
                x.GetServices<IBulkSqlInsertProvider>(),
                x.GetServices<IEmbeddedDatabaseCreator>()
            ));

            // TODO: We want to avoid pre-resolving a container as much as possible we should not
            // be doing this any more than we are now. The ugly part about this is that the service
            // instances resolved here won't be the same instances resolved from the container
            // later once the true container is built. However! ... in the case of IDbProviderFactoryCreator
            // it will be the same instance resolved later because we are re-registering this instance back
            // into the container. This is not true for `Configs` but we should do that too, see comments in
            // `RegisterEssentials`.
            var serviceProvider = services.BuildServiceProvider();

            var globalSettings = serviceProvider.GetService<IOptions<GlobalSettings>>().Value;
            var connectionStrings = serviceProvider.GetService<IOptions<ConnectionStrings>>().Value;
            var hostingSettings = serviceProvider.GetService<IOptions<HostingSettings>>().Value;
            var typeFinderSettings = serviceProvider.GetService<IOptions<TypeFinderSettings>>().Value;

            var dbProviderFactoryCreator = serviceProvider.GetRequiredService<IDbProviderFactoryCreator>();

            CreateCompositionRoot(services,
                globalSettings,
                hostingSettings,
                webHostEnvironment,
                loggingConfiguration,
                out var logger, out var ioHelper, out var hostingEnvironment, out var backOfficeInfo, out var profiler);

            var umbracoVersion = new UmbracoVersion();
            var typeFinder = CreateTypeFinder(logger, profiler, webHostEnvironment, entryAssembly, typeFinderSettings);

            var configs = serviceProvider.GetService<Configs>();
            var coreRuntime = GetCoreRuntime(
                configs,
                globalSettings,
                connectionStrings,
                umbracoVersion,
                ioHelper,
                logger,
                profiler,
                hostingEnvironment,
                backOfficeInfo,
                typeFinder,
                requestCache,
                dbProviderFactoryCreator);

            factory = coreRuntime.Configure(container);

            return services;
        }

        private static ITypeFinder CreateTypeFinder(Core.Logging.ILogger logger, IProfiler profiler, IWebHostEnvironment webHostEnvironment, Assembly entryAssembly, TypeFinderSettings typeFinderSettings)
        {
            var runtimeHashPaths = new RuntimeHashPaths();
            runtimeHashPaths.AddFolder(new DirectoryInfo(Path.Combine(webHostEnvironment.ContentRootPath, "bin")));
            var runtimeHash = new RuntimeHash(new ProfilingLogger(logger, profiler), runtimeHashPaths);
            return new TypeFinder(logger, new DefaultUmbracoAssemblyProvider(entryAssembly), runtimeHash, new TypeFinderConfig(typeFinderSettings));
        }

        private static IRuntime GetCoreRuntime(
            Configs configs, GlobalSettings globalSettings, ConnectionStrings connectionStrings, IUmbracoVersion umbracoVersion, IIOHelper ioHelper, Core.Logging.ILogger logger,
            IProfiler profiler, Core.Hosting.IHostingEnvironment hostingEnvironment, IBackOfficeInfo backOfficeInfo,
            ITypeFinder typeFinder, IRequestCache requestCache, IDbProviderFactoryCreator dbProviderFactoryCreator)
        {
            // Determine if we should use the sql main dom or the default
            var appSettingMainDomLock = globalSettings.MainDomLock;

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var mainDomLock = appSettingMainDomLock == "SqlMainDomLock" || isWindows == false
                ? (IMainDomLock)new SqlMainDomLock(logger, globalSettings, connectionStrings, dbProviderFactoryCreator, hostingEnvironment)
                : new MainDomSemaphoreLock(logger, hostingEnvironment);

            var mainDom = new MainDom(logger, mainDomLock);

            var coreRuntime = new CoreRuntime(
                configs,
                globalSettings,
                connectionStrings,
                umbracoVersion,
                ioHelper,
                logger,
                profiler,
                new AspNetCoreBootPermissionsChecker(),
                hostingEnvironment,
                backOfficeInfo,
                dbProviderFactoryCreator,
                mainDom,
                typeFinder,
                requestCache);

            return coreRuntime;
        }

        private static IServiceCollection CreateCompositionRoot(
            IServiceCollection services,
            GlobalSettings globalSettings,
            HostingSettings hostingSettings,
            IWebHostEnvironment webHostEnvironment,
            ILoggingConfiguration loggingConfiguration,
            out Core.Logging.ILogger logger,
            out IIOHelper ioHelper,
            out Core.Hosting.IHostingEnvironment hostingEnvironment,
            out IBackOfficeInfo backOfficeInfo,
            out IProfiler profiler)
        {
            if (globalSettings == null)
                throw new InvalidOperationException($"Could not resolve type {typeof(GlobalSettings)} from the container, ensure {nameof(AddUmbracoConfiguration)} is called before calling {nameof(AddUmbracoCore)}");


            hostingEnvironment = new AspNetCoreHostingEnvironment(hostingSettings, webHostEnvironment);
            ioHelper = new IOHelper(hostingEnvironment);
            logger = AddLogger(services, hostingEnvironment, loggingConfiguration);

            backOfficeInfo = new AspNetCoreBackOfficeInfo(globalSettings);
            profiler = GetWebProfiler(hostingEnvironment);

            return services;
        }

        /// <summary>
        /// Create and configure the logger
        /// </summary>
        /// <param name="hostingEnvironment"></param>
        private static Core.Logging.ILogger AddLogger(IServiceCollection services, Core.Hosting.IHostingEnvironment hostingEnvironment, ILoggingConfiguration loggingConfiguration)
        {
            // Create a serilog logger
            var logger = SerilogLogger.CreateWithDefaultConfiguration(hostingEnvironment, loggingConfiguration);

            // Wire up all the bits that serilog needs. We need to use our own code since the Serilog ext methods don't cater to our needs since
            // we don't want to use the global serilog `Log` object and we don't have our own ILogger implementation before the HostBuilder runs which
            // is the only other option that these ext methods allow.
            // I have created a PR to make this nicer https://github.com/serilog/serilog-extensions-hosting/pull/19 but we'll need to wait for that.
            // Also see : https://github.com/serilog/serilog-extensions-hosting/blob/dev/src/Serilog.Extensions.Hosting/SerilogHostBuilderExtensions.cs

            services.AddSingleton<ILoggerFactory>(services => new SerilogLoggerFactory(logger.SerilogLog, false));

            // This won't (and shouldn't) take ownership of the logger.
            services.AddSingleton(logger.SerilogLog);

            // Registered to provide two services...
            var diagnosticContext = new DiagnosticContext(logger.SerilogLog);

            // Consumed by e.g. middleware
            services.AddSingleton(diagnosticContext);

            // Consumed by user code
            services.AddSingleton<IDiagnosticContext>(diagnosticContext);

            return logger;
        }

        private static IProfiler GetWebProfiler(Umbraco.Core.Hosting.IHostingEnvironment hostingEnvironment)
        {
            // create and start asap to profile boot
            if (!hostingEnvironment.IsDebugMode)
            {
                // should let it be null, that's how MiniProfiler is meant to work,
                // but our own IProfiler expects an instance so let's get one
                return new VoidProfiler();
            }

            var webProfiler = new WebProfiler();
            webProfiler.StartBoot();

            return webProfiler;
        }

        private class AspNetCoreBootPermissionsChecker : IUmbracoBootPermissionChecker
        {
            public void ThrowIfNotPermissions()
            {
                // nothing to check
            }
        }

    }

}
