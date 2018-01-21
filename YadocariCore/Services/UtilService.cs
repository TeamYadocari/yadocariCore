#region Copyright
// /*
//  * UtilService.cs
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
using YadocariCore.Models;

namespace YadocariCore.Services
{
    public class UtilService
    {
        public static int GetOrCreateFileByTitle(OneDriveDbContext dbContext, string title)
        {
            if (string.IsNullOrEmpty(title)) return -1;

            var file = dbContext.Files.FirstOrDefault(x => x.DocumentName == title);
            if (file != null) return file.Id;

            file = new File(title);
            dbContext.Files.Add(file);
            dbContext.SaveChanges();
            return dbContext.Files.First(x => x.DocumentName == title).Id;
        }
    }
}
