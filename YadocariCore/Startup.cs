#region Copyright
// /*
//  * Startup.cs
//  *
//  * Copyright (c) 2018 TeamYadocari
//  *
//  * You can redistribute it and/or modify it under either the terms of
//  * the AGPLv3 or YADOCARI binary code license. See the file COPYING
//  * included in the YADOCARI package for more in detail.
//  *
//  */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YadocariCore.Models;
using YadocariCore.Models.Config;
using YadocariCore.Services;

namespace YadocariCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    // Password settings
                    options.Password.RequireDigit = false;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequiredUniqueChars = 2;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddDbContext<OneDriveDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddDbContext<ConfigurationContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.Configure<ApplicationConfig>(Configuration.GetSection("ApplicationConfig"));
            services.Configure<OneDriveConfig>(Configuration.GetSection("OneDrive"));

            // Add application services.
            //services.AddTransient<IEmailSender, EmailSender>();
            services.AddScoped<OneDriveService>();
            services.AddScoped<ConfigService>();

            services.AddMvc();

            services.AddScoped<DbInitializer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, DbInitializer dbInitializer)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            dbInitializer.Initialize();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
