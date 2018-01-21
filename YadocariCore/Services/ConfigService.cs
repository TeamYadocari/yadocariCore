#region Copyright
// /*
//  * ConfigService.cs
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
using YadocariCore.Models.Config;

namespace YadocariCore.Services
{
    public class ConfigService
    {
        private readonly ConfigurationContext _context;
        public ConfigService(ConfigurationContext context)
        {
            _context = context;
        }

        private static T Parse<T>(object obj)
        {
            if (obj == null) return default(T);
            return (T)Convert.ChangeType(obj, typeof(T));
        }

        public bool ContainsKey(string key)
        {
            return _context.Configs.Find(key) != null;
        }

        public T GetConfiguration<T>(string key)
        {
            return Parse<T>(_context.Configs.Find(key).Value);
        }

        public void SetConfiguration(string key, object value)
        {
            if (ContainsKey(key))
            {
                var config = _context.Configs.Find(key);
                config.Value = value.ToString();
                _context.Configs.Update(config);
            }
            else
            {
                _context.Configs.Add(new ApplicationDbConfig { Id = key, Value = value.ToString() });
            }
            _context.SaveChanges();
        }

    }
}
