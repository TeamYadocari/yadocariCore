#region Copyright
// /*
//  * ManageController.cs
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
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using MailKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using YadocariCore.Models;
using YadocariCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Owin;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using YadocariCore.Models.Config;

namespace YadocariCore.Controllers
{
    [Authorize(Roles = nameof(Role.Administrator))]
    public class ManageController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private Task<ApplicationUser> GetCurrentUserAsync() => _userManager.GetUserAsync(HttpContext.User);
        private readonly OneDriveService _oneDriveService;
        private readonly ApplicationConfig _config;
        private readonly OneDriveDbContext _dbContext;
        private readonly ConfigService _configService;

        public ManageController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, OneDriveService oneDriveService, IOptions<ApplicationConfig> config, OneDriveDbContext dbContext, ConfigService configService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _oneDriveService = oneDriveService;
            _config = config.Value;
            _dbContext = dbContext;
            _configService = configService;
        }

        //
        // GET: /Manage/Index
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            var userId = (await GetCurrentUserAsync()).Id;
            var model = new IndexViewModel
            {
                HasPassword = await HasPassword(),
                IsAdmin = User.IsInRole(nameof(Role.Administrator)),
                Users = await _userManager.Users.ToListAsync(),
                MSAccounts = await _dbContext.Accounts.ToListAsync()
            };
            return View(model);
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await _userManager.ChangePasswordAsync(await GetCurrentUserAsync(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await GetCurrentUserAsync();
                if (user != null)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.Success });
            }
            AddErrors(result);
            return View(model);
        }

        public ActionResult ManageUsers(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            ViewBag.Roles = Enum.GetNames(typeof(Role));
            var dict = _userManager.Users.ToDictionary(x => x.FileId, x => _dbContext.Files.Find(x.FileId)?.DocumentName);
            ViewBag.TitleDict = dict;
            return View(_userManager.Users.ToList());
        }

        public ActionResult ManageFiles(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            return View(_dbContext.Files.Where(x => !string.IsNullOrEmpty(x.OneDriveFileId)).ToArray());
        }

        #region ヘルパー
        // 外部ログインの追加時に XSRF の防止に使用します
        private const string XsrfKey = "XsrfId";

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }

        private async Task<bool> HasPassword()
        {
            var user = await _userManager.FindByIdAsync((await GetCurrentUserAsync()).Id);
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            Success,
            Error
        }

        #endregion

        public ActionResult MailConfig()
        {
            return View(new MailConfigViewModel()
            {
                MailServer = _config.MailServer,
                AccountName = _config.AccountName,
                Password = _config.Password,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MailConfigResult(MailConfigViewModel model)
        {
            throw new NotImplementedException();

            //SetConfiguration("MailServer", model.MailServer);
            //SetConfiguration("AccountName", model.AccountName);
            //if (model.Password != null)
            //{
            //    SetConfiguration("Password", model.Password);
            //}
            //return RedirectToAction("Index", new { message = ManageMessageId.Success });
        }

        public async Task<ActionResult> AddNewAccounts()
        {
            IEnumerable<MimeMessage> messages;
            var titleDict = new Dictionary<int, string>();

            using (var client = new ImapClient())
            {
                client.Connect(_config.MailServer, 993);
                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                client.Authenticate(_config.AccountName, _config.Password);

                var inbox = client.Inbox;
                inbox.Open(FolderAccess.ReadWrite);
                var uids = inbox.Search(SearchQuery.NotSeen);
                messages = uids.Select(x =>
                {
                    inbox.AddFlags(x, MessageFlags.Seen, true);
                    return inbox.GetMessage(x);
                }).ToArray();
            }

            var addedUsers = new List<ApplicationUser>();
            var titleChangedUsers = new List<ApplicationUser>();

            foreach (var message in messages.Where(x => x.Subject.Contains("発表申込完了のお知らせ")))
            {
                var body = (MultipartAlternative)message.Body;

                var r = new Regex(@"１．整理番号：(?<id>\d+)");
                if (!r.IsMatch(body.TextBody)) continue;
                var id = r.Match(body.TextBody).Groups["id"].Value;

                r = new Regex(@"２．パスワード：(?<password>[a-zA-Z0-9]+)");
                if (!r.IsMatch(body.TextBody)) continue;
                var password = r.Match(body.TextBody).Groups["password"].Value;

                r = new Regex(@"３．講演題名：(?<title>.+?)------------------------------------------------------------", RegexOptions.Singleline);
                if (!r.IsMatch(body.TextBody)) continue;
                var title = r.Match(body.TextBody).Groups["title"].Value.Replace("\r", "").Replace("\n", "");

                var fileId = UtilService.GetOrCreateFileByTitle(_dbContext, title);
                titleDict.Add(fileId, title);

                var user = new ApplicationUser { UserName = id, FileId = fileId };
                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, nameof(Role.Upload));
                    addedUsers.Add(user);
                }
            }

            foreach (var message in messages.Where(x => x.Subject.Contains("担当研究会申込者更新連絡のお知らせ")))
            {
                var body = (MultipartAlternative)message.Body;

                var r = new Regex(@"1．整理番号：(?<id>\d+)");
                if (!r.IsMatch(body.TextBody)) continue;
                var id = r.Match(body.TextBody).Groups["id"].Value;

                r = new Regex(@"2．タイトル：(?<title>.+?)------------------------------------------------------------", RegexOptions.Singleline);
                if (!r.IsMatch(body.TextBody)) continue;
                var title = r.Match(body.TextBody).Groups["title"].Value.Replace("\r", "").Replace("\n", "");

                var user = await _userManager.FindByNameAsync(id);
                if (user == null) continue;

                var file = _dbContext.Files.Find(user.FileId);
                file.DocumentName = title;
                _dbContext.SaveChanges();
                titleDict.Add(user.FileId, title);

                titleChangedUsers.Add(user);
            }

            ViewBag.TitleDict = titleDict;
            return View((addedUsers, titleChangedUsers));
        }

        public async Task<ActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return View("Error");

            var roles = await Task.WhenAll(Enum.GetNames(typeof(Role)).Select(async role => new SelectListItem
            {
                Text = role,
                Value = role,
                Selected = await _userManager.IsInRoleAsync(user, role)
            }));

            var file = _dbContext.Files.Find(user.FileId);

            return View(new EditUserViewModel
            {
                CurrentUserName = user.UserName,
                UserName = user.UserName,
                Title = file?.DocumentName,
                Roles = roles,
                Role = roles.FirstOrDefault(x => x.Selected)?.Value
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditUserResult(EditUserViewModel model)
        {
            if (model.Role == Role.Upload.ToString() && string.IsNullOrEmpty(model.Title)) throw new Exception("Uploadロールのユーザには発表題目を設定してください");

            var user = await _userManager.FindByNameAsync(model.CurrentUserName);
            if (user == null) return View("Error");

            ViewBag.UserName = user.UserName;
            user.UserName = model.UserName;

            foreach (var role in Enum.GetNames(typeof(Role)))
            {
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            var userUpdateSucceeded = (await _userManager.UpdateAsync(user)).Succeeded;
            if (userUpdateSucceeded && model.NewPassword != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                return View((await _userManager.ResetPasswordAsync(user, token, model.NewPassword)).Succeeded);
            }

            if (!string.IsNullOrEmpty(model.Title))
            {
                if (user.FileId == -1)
                {
                    user.FileId = UtilService.GetOrCreateFileByTitle(_dbContext, model.Title);
                    await _userManager.UpdateAsync(user);
                }
                else
                {
                    var file = _dbContext.Files.Find(user.FileId);
                    file.DocumentName = model.Title;
                }
                _dbContext.SaveChanges();
            }
            else
            {
                user.FileId = -1;
                await _userManager.UpdateAsync(user);
            }

            return RedirectToAction("ManageUsers", new { message = userUpdateSucceeded ? ManageMessageId.Success : ManageMessageId.Error });
        }

        //
        // GET: /Manage/AddMicrosoftAccount
        public ActionResult AddMicrosoftAccount()
        {
            var clientId = _oneDriveService.ClientId;
            var redirectUrl = _config.ServerUrl + Url.Action("AddMicrosoftAccountCallback", "Manage");

            return Redirect($"https://login.live.com/oauth20_authorize.srf?response_type=code&client_id={clientId}&scope=wl.signin%20wl.offline_access%20onedrive.readwrite%20wl.skydrive_update&redirect_uri={redirectUrl}");
        }

        //
        // GET: /Manage/AddMicrosoftAccountCallback
        public async Task<ActionResult> AddMicrosoftAccountCallback(string code)
        {
            var refreshToken = await _oneDriveService.GetRefreshTokenAsync(code);
            var info = await _oneDriveService.GetOwnerInfoAsync(refreshToken);

            if (_dbContext.Accounts.Any(x => x.OneDriveId == info.Id))
            {
                ViewBag.Success = false;
                return View();
            }
            _dbContext.Accounts.Add(new Account
            {
                Name = info.DisplayName,
                OneDriveId = info.Id,
                RefleshToken = refreshToken
            });
            _dbContext.SaveChanges();

            if (!_configService.ContainsKey("UsingMicrosoftAccountId"))
            {
                _configService.SetConfiguration("UsingMicrosoftAccountId", _dbContext.Accounts.First().Id);
            }

            ViewBag.Success = true;
            return View();
        }

        public class MicrosoftAccount
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string FreeSpace { get; set; }
            public bool Using { get; set; }
        }

        private static string GetHumanReadableSize(long bytes)
        {
            string[] sizes = { "Bytes", "KB", "MB", "GB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public async Task<ActionResult> ManageMicrosoftAccounts()
        {
            var model = new List<MicrosoftAccount>();

            foreach (var a in _dbContext.Accounts)
            {
                model.Add(new MicrosoftAccount
                {
                    Id = a.Id,
                    Name = a.Name,
                    FreeSpace = GetHumanReadableSize((await _oneDriveService.GetOwnerInfoAsync(a.RefleshToken)).FreeSpace),
                    Using = _configService.GetConfiguration<int>("UsingMicrosoftAccountId") == a.Id
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeUsingMicrosoftAccount(int id)
        {
            if (_dbContext.Accounts.Any(account => account.Id == id))
            {
                _configService.SetConfiguration("UsingMicrosoftAccountId", id);
            }
            else
            {
                return View("Error");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMicrosoftAccount(int id)
        {
            if (_configService.GetConfiguration<int>("UsingMicrosoftAccountId") == id) return View("Error");

            if (_dbContext.Accounts.Any(account => account.Id == id))
            {
                foreach (var file in _dbContext.Files.Where(file => file.MicrosoftAccountId == id))
                {
                    file.MicrosoftAccountId = -1;
                    file.OneDriveFileId = null;
                }
                _dbContext.Accounts.Remove(_dbContext.Accounts.Find(id));
                _dbContext.SaveChanges();

                //キャッシュを全削除
                HomeController.Cache.Clear();
            }
            else
            {
                return View("Error");
            }
            return View();
        }

        public ActionResult SystemConfig()
        {
            return View(new SystemConfigViewModel
            {
                ServerUrl = _config.ServerUrl,
                LinkEnableDuration = _config.LinkEnableDuration,
                CacheDuration = _config.CacheDuration,
                EnableAccountAutoChange = _config.EnableAccountAutoChange,
                ChangeThreshold = _config.ChangeThreshold,
            });
        }

        public ActionResult SystemConfigResult(SystemConfigViewModel model)
        {
            throw new NotImplementedException();

            //SetConfiguration("ServerUrl", model.ServerUrl);
            //SetConfiguration("LinkEnableDuration", model.LinkEnableDuration);
            //SetConfiguration("CacheDuration", model.CacheDuration);
            //SetConfiguration("EnableAccountAutoChange", model.EnableAccountAutoChange);
            //SetConfiguration("ChangeThreshold", model.ChangeThreshold);

            //return RedirectToAction("Index", new { message = ManageMessageId.Success });
        }
    }
}