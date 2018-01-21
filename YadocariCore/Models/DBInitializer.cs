#region Copyright
// /*
//  * DBInitializer.cs
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace YadocariCore.Models
{

    public class DbInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DbInitializer(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async void Initialize()
        {
            _context.Database.EnsureCreated();

            foreach (var role in Enum.GetNames(typeof(Role)).Where(x => !_context.Roles.Any(r => r.Name == x)))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }

            var user = await _userManager.FindByNameAsync(nameof(Role.Administrator));
            if (user == null)
            {
                var userName = "Administrator";
                var password = Guid.NewGuid().ToString();
                await _userManager.CreateAsync(new ApplicationUser { UserName = userName, Email = userName, EmailConfirmed = true }, password);
                await _userManager.AddToRoleAsync(await _userManager.FindByNameAsync(userName), nameof(Role.Administrator));
                Debug.WriteLine($"Initial user created: {userName}, {password}");
            }
        }
    }
}
