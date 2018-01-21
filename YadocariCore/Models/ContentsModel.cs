#region Copyright
// /*
//  * ContentsModel.cs
//  *
//  * Copyright (c) 2018 TeamYadocari
//  *
//  * You can redistribute it and/or modify it under either the terms of
//  * the AGPLv3 or YADOCARI binary code license. See the file COPYING
//  * included in the YADOCARI package for more in detail.
//  *
//  */
#endregion
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace YadocariCore.Models
{
    public class File
    {
        public File()
        {
            DocumentId = -1;
            MicrosoftAccountId = -1;
        }

        public File(string title)
        {
            DocumentName = title;
            DocumentId = -1;
            MicrosoftAccountId = -1;
        }

        public int Id { get; set; }
        //[Index(IsUnique = true)]
        public int DocumentId { get; set; }
        //[Index(IsUnique = true)]
        [StringLength(256)]
        public string DocumentName { get; set; }
        public int MicrosoftAccountId { get; set; }
        public string OneDriveFileId { get; set; }
        public int DownloadCount { get; set; }
    }

    public class Account
    {
        public int Id { get; set; }
        //[Index(IsUnique = true)]
        [StringLength(256)]
        public string OneDriveId { get; set; }
        public string Name { get; set; }
        public string RefleshToken { get; set; }
    }

    public class OneDriveDbContext : DbContext
    {
        public DbSet<File> Files { get; set; }
        public DbSet<Account> Accounts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<File>()
                .HasIndex(f => new { f.DocumentId, f.DocumentName }).IsUnique();
            modelBuilder.Entity<Account>()
                .HasIndex(a => new { a.OneDriveId }).IsUnique();

            //modelBuilder.Entity<File>()
            //    .HasAlternateKey(f => new { f.DocumentId, f.DocumentName });
            //modelBuilder.Entity<Account>()
            //    .HasAlternateKey(a => new { a.OneDriveId });
        }

        public OneDriveDbContext(DbContextOptions<OneDriveDbContext> options) : base(options)
        {
        }
    }

    public class ChangeAssosiationViewModel
    {
        [Required]
        [Display(Name = "FileId")]
        public int FileId { get; set; }

        [Display(Name = "現在の電子図書館上のID")]
        public int CurrentId { get; set; }

        [Required]
        [Display(Name = "電子図書館上のID")]
        public int NewId { get; set; }
    }
}
