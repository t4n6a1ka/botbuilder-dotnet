﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.3.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Adapters.Webex;
using Microsoft.Bot.Builder.Webex.Sample.Bots;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Bot.Builder.Webex.Sample
{
    public class Startup
    {
        private readonly WebexAdapter adapter;
        private SimpleWebexAdapterOptions options;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            options = new SimpleWebexAdapterOptions(configuration["AccessToken"], configuration["PublicAdress"], configuration["Secret"]);
            adapter = new WebexAdapter(options);
            //adapter.ResetWebhookSubscriptions().Wait();
            adapter.GetIdentityAsync().Wait();
            adapter.RegisterWebhookSubscription("/api/messages").Wait();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // Create the credential provider to be used with the Bot Framework Adapter.
            services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

            // Create the options for the WebexAdapter
            services.AddSingleton<IWebexAdapterOptions, ConfigurationWebexAdapterOptions>();

            // Create the Bot Framework Adapter.
            services.AddSingleton<WebexAdapter>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, EchoBot>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            //app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
