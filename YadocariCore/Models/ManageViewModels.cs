#region Copyright
// /*
//  * ManageViewModels.cs
//  *
//  * Copyright (c) 2018 TeamYadocari
//  *
//  * You can redistribute it and/or modify it under either the terms of
//  * the AGPLv3 or YADOCARI binary code license. See the file COPYING
//  * included in the YADOCARI package for more in detail.
//  *
//  */
#endregion
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace YadocariCore.Models
{
    public class IndexViewModel
    {
        public bool HasPassword { get; set; }
        public bool IsAdmin { get; set; }
        public IList<ApplicationUser> Users { get; set; }
        public IList<Account> MSAccounts { get; set; }
    }

    public class ManageLoginsViewModel
    {
        public IList<UserLoginInfo> CurrentLogins { get; set; }
        public IList<AuthenticationDescription> OtherLogins { get; set; }
    }

    public class FactorViewModel
    {
        public string Purpose { get; set; }
    }

    public class SetPasswordViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "{0} の長さは {2} 文字以上である必要があります。", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワード")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワードの確認入力")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "新しいパスワードと確認のパスワードが一致しません。")]
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "現在のパスワード")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "{0} の長さは {2} 文字以上である必要があります。", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワード")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワードの確認入力")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "新しいパスワードと確認のパスワードが一致しません。")]
        public string ConfirmPassword { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        [Display(Name = "現在のユーザー名")]
        public string CurrentUserName { get; set; }

        [Required]
        [Display(Name = "ユーザー名")]
        public string UserName { get; set; }

        [Display(Name = "ロール")]
        public IEnumerable<SelectListItem> Roles { get; set; }

        [Required]
        [Display(Name = "ロール")]
        public string Role { get; set; }

        [Display(Name = "講演題名")]
        public string Title { get; set; }

        [StringLength(100, ErrorMessage = "{0} の長さは {2} 文字以上である必要があります。", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワード")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "新しいパスワードの確認入力")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "新しいパスワードと確認のパスワードが一致しません。")]
        public string ConfirmPassword { get; set; }
    }

    public class MailConfigViewModel
    {
        [Required]
        [Display(Name = "メールサーバー(IMAP)")]
        [DataType(DataType.Url)]
        public string MailServer { get; set; }

        [Required]
        [Display(Name = "アカウント名")]
        [DataType(DataType.Text)]
        public string AccountName { get; set; }

        [Display(Name = "パスワード")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class SystemConfigViewModel
    {
        [Required]
        [Display(Name = "システムが稼働しているURL")]
        [DataType(DataType.Url)]
        public string ServerUrl { get; set; }

        [Required]
        [Display(Name = "発表資料へのリンクの有効期限（分）")]
        [DataType(DataType.Duration)]
        public int LinkEnableDuration { get; set; }

        [Required]
        [Display(Name = "電子図書館の情報のキャッシュ時間（分）")]
        [DataType(DataType.Duration)]
        public int CacheDuration { get; set; }

        [Required]
        [Display(Name = "OneDriveアカウントを自動的に切り替える")]
        public bool EnableAccountAutoChange { get; set; }

        [Required]
        [Display(Name = "残容量がこれ未満のとき（MB）")]
        [DataType(DataType.Duration)]
        public int ChangeThreshold { get; set; }
    }
}