/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System.Net.Http;
using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MigrationApp;
using Serilog;
using WebApplication.Definitions;
using WebApplication.Middleware;
using WebApplication.Processing;
using WebApplication.Services;
using WebApplication.State;
using WebApplication.Utilities;

namespace WebApplication
{
    public class Startup
    {
        private const string ForgeSectionKey = "Forge";
        private const string AppBundleZipPathsKey = "AppBundleZipPaths";
        private const string DefaultProjectsSectionKey = "DefaultProjects";
        private const string InviteOnlyModeKey = "InviteOnlyMode";
        private const string ProcessingOptionsKey = "Processing";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllersWithViews()
                .AddJsonOptions(options =>
                                {
                                    options.JsonSerializerOptions.IgnoreNullValues = true;
                                });

            services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
            });

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });

            services.AddHttpClient();

            services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = 500 * 1024 * 1024;
                x.MultipartBodyLengthLimit = 500 * 1024 * 1024; // default was 134217728, 500000000 is enough due to FDA quotas (500 MB uncompressed size)
            });

            // NOTE: eventually we might want to use `AddForgeService()`, but right now it might break existing stuff
            // https://github.com/Autodesk-Forge/forge-api-dotnet-core/blob/master/src/Autodesk.Forge.Core/ServiceCollectionExtensions.cs
            services
                .Configure<ForgeConfiguration>(Configuration.GetSection(ForgeSectionKey))
                .Configure<AppBundleZipPaths>(Configuration.GetSection(AppBundleZipPathsKey))
                .Configure<DefaultProjectsConfiguration>(Configuration.GetSection(DefaultProjectsSectionKey))
                .Configure<InviteOnlyModeConfiguration>(Configuration.GetSection(InviteOnlyModeKey))
                .Configure<ProcessingOptions>(Configuration.GetSection(ProcessingOptionsKey));

            services.AddSingleton<ResourceProvider>();
            services.AddSingleton<IPostProcessing, PostProcessing>();
            services.AddSingleton<IForgeOSS, ForgeOSS>();
            services.AddSingleton<FdaClient>();
            services.AddTransient<Initializer>();
            services.AddTransient<Arranger>();
            services.AddTransient<ProjectWork>();
            services.AddTransient<DtoGenerator>();
            services.AddSingleton<DesignAutomationClient>(provider =>
                                    {
                                        var forge = provider.GetService<IForgeOSS>();
                                        var httpMessageHandler = new ForgeHandler(Options.Create(forge.Configuration))
                                        {
                                            InnerHandler = new HttpClientHandler()
                                        };
                                        var forgeService = new ForgeService(new HttpClient(httpMessageHandler));
                                        return new DesignAutomationClient(forgeService);
                                    });
            services.AddSingleton<Publisher>();
            services.AddSingleton<BucketPrefixProvider>();
            services.AddSingleton<LocalCache>();
            services.AddSingleton<Uploads>();
            services.AddSingleton<OssBucketFactory>();

            if (Configuration.GetValue<bool>("migration"))
            {
                services.AddHostedService<MigrationApp.Worker>();
                services.AddSingleton<MigrationBucketKeyProvider>();
                services.AddSingleton<IBucketKeyProvider>(provider =>
                {
                    return provider.GetService<MigrationBucketKeyProvider>();
                });
                services.AddSingleton<UserResolver>();
                services.AddSingleton<ProfileProvider>();
                services.AddSingleton<Migration>();
                services.AddSingleton<ProjectService>();
            }
            else
            {
                services.AddScoped<IBucketKeyProvider, LoggedInUserBucketKeyProvider>();
                services.AddScoped<UserResolver>();
                services.AddScoped<ProfileProvider>();
                services.AddScoped<ProjectService>();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Initializer initializer, ILogger<Startup> logger, LocalCache localCache, IOptions<ForgeConfiguration> forgeConfiguration)
        {
            if(Configuration.GetValue<bool>("clear"))
            {
                logger.LogInformation("-- Clean up --");
                // retrieve used Forge Client Id and Client Id where it is allowed to delete user buckets
                string clientIdCanDeleteUserBuckets = Configuration.GetValue<string>("clientIdCanDeleteUserBuckets");
                string clientId = forgeConfiguration.Value.ClientId;
                // only on allowed Client Id remove the user buckets
                bool deleteUserBuckets = (clientIdCanDeleteUserBuckets == clientId);
                initializer.ClearAsync(deleteUserBuckets).Wait();
            }

            if(Configuration.GetValue<bool>("initialize"))
            {
                logger.LogInformation("-- Initialization --");
                initializer.InitializeAsync().Wait();
            }

            if(Configuration.GetValue<bool>("bundles"))
            {
                logger.LogInformation("-- Initialization of AppBundles and Activities --");
                initializer.InitializeBundlesAsync().Wait();
            }

            if (env.IsDevelopment())
            {
                logger.LogInformation("In Development environment");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // expose local cache as static files
            localCache.Serve(app);

            app.UseSpaStaticFiles();

            // Use Serilog middleware to log ASP.NET requests. To not pollute logs with requests about
            // static file the middleware registered after middleware for serving static files.
            app.UseSerilogRequestLogging();

            app.UseMiddleware<HeaderTokenHandler>();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<Controllers.JobsHub>("/signalr/connection");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}
