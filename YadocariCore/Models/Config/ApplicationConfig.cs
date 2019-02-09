#region Copyright
// /*
//  * ApplicationConfig.cs
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace YadocariCore.Models.Config
{
    //from json
    public class ApplicationConfig
    {
        public string MailServer { get; set; }
        public string AccountName { get; set; }
        public string Password { get; set; }
        public int LinkEnableDuration { get; set; }
        public int CacheDuration { get; set; }
        public bool EnableAccountAutoChange { get; set; }
        public int ChangeThreshold { get; set; }
        public string ServerUrl { get; set; }
    }

    public class OneDriveConfig
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    //from DB
    public class ApplicationDbConfig
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }

    public class ConfigurationContext : DbContext
    {
        public ConfigurationContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<ApplicationDbConfig> Configs { get; set; }
    }
}
