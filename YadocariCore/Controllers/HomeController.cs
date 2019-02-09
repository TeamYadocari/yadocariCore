#region Copyright
// /*
//  * HomeController.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YadocariCore.Models;
using YadocariCore.Services;
using Microsoft.AspNetCore.Owin;
using Microsoft.CodeAnalysis.Options;
using File = YadocariCore.Models.File;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YadocariCore.Models.Config;

namespace YadocariCore.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly OneDriveService _oneDriveService;
        private readonly ILogger _logger;
        private readonly ApplicationConfig _config;
        private readonly OneDriveDbContext _dbContext;
        private readonly ConfigService _configService;

        public HomeController(UserManager<ApplicationUser> userManager, OneDriveService oneDrvieService, IOptionsSnapshot<ApplicationConfig> config, OneDriveDbContext dbContext, ConfigService configService)
        {
            _userManager = userManager;
            _oneDriveService = oneDrvieService;
            _config = config.Value;
            _dbContext = dbContext;
            _configService = configService;
        }

        private async Task DeleteShareLinkTask(int accountNum, string fileId, string permissionId)
        {
            await Task.Delay(TimeSpan.FromMinutes(_config.LinkEnableDuration));
            await _oneDriveService.DeleteShareLinkAsync(_dbContext, accountNum, fileId, permissionId);
        }

        private static List<Task> _tasks = new List<Task>(); //GCに消されないように保持しておく

        [AllowAnonymous]
        public ActionResult About()
        {
            ViewBag.Message = "本システムの一般的な利用法など.";

            return View();
        }

        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = "本システムに関する連絡先．";

            return View();
        }

        public new class Content
        {
            public string Title { get; set; }
            public int Count { get; set; } //-1→コンテンツ
            public string Url { get; set; }
            public int Id { get; set; }
            public int FileId { get; set; }
            public int DownloadCount { get; set; }
            public bool Uploaded { get; set; }

            public Content(OneDriveDbContext dbContext, string title, int count, string url, int id)
            {
                Title = title;
                Count = count;
                Url = url;
                Id = id;

                //アップロード済みファイルとの関連付け
                var file = dbContext.Files.FirstOrDefault(x => x.DocumentName == title);
                if (file != null && file.DocumentId == -1)
                {
                    file.DocumentId = id;
                    dbContext.SaveChanges();
                }

                if (count == -1 && dbContext.Files.Any(x => x.DocumentId == id))
                {
                    file = dbContext.Files.First(x => x.DocumentId == id);
                    Uploaded = !string.IsNullOrEmpty(file.OneDriveFileId);
                    DownloadCount = file.DownloadCount;
                    FileId = file.Id;
                }
            }
        }

        public class ContentsCache
        {
            public DateTime CachedAt { get; set; }
            public Content[] Contents { get; set; }
        }

        public static readonly Dictionary<int, ContentsCache> Cache = new Dictionary<int, ContentsCache>();

        [UploadUserRedirect]
        public ActionResult Index()
        {
            return View();
        }

        [UploadUserRedirect]
        public ActionResult Contents(int id = -1)
        {
            if (id == -1) id = 4088; //IOTトップ

            //キャッシュを利用する
            if (Cache.ContainsKey(id) && DateTime.Now - Cache[id].CachedAt <= TimeSpan.FromMinutes(_config.CacheDuration))
            {
                Debug.WriteLine("キャッシュ利用");
                return View(Cache[id].Contents);
            }

            var url = $"https://ipsj.ixsq.nii.ac.jp/ej/?action=repository_opensearch&index_id={id}&count=100&order=7";
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            var str = wc.DownloadString(url);
            var regFolder = new Regex(
                @"<td class=""pl10 pt10 vat"" width=""100%""><span><a href=""(?<url>https://ipsj\.ixsq\.nii\.ac\.jp/ej/\?action=repository_opensearch&amp;index_id=(?<indexId>\d+)&amp;count=100&amp;order=7&amp;pn=1)"">(?<title>.+)</a><wbr /><span class=""text_color"">&nbsp;\[(?<count>\d+)件</span><span class=""text_color"">\]&nbsp;</span></span></td>");
            var results = regFolder.Matches(str);
            var contents = (from Match result in results select new Content(_dbContext, result.Groups["title"].Value, int.Parse(result.Groups["count"].Value), result.Groups["url"].Value, int.Parse(result.Groups["indexId"].Value)));

            if (!contents.Any()) //フォルダが一つも無い→コンテンツ
            {
                var regContents = new Regex(
                    @"<div class=""item_title pl55"">\s+<a href=""(?<url>https://ipsj\.ixsq\.nii\.ac\.jp/ej/\?action=repository_uri&item_id=(?<itemId>\d+))"">\s+(?<title>.+)\s+</a>\s+</div>");
                results = regContents.Matches(str);

                contents = from Match result in results select new Content(_dbContext, result.Groups["title"].Value, -1, result.Groups["url"].Value, int.Parse(result.Groups["itemId"].Value));
            }

            //キャッシュに登録
            if (Cache.ContainsKey(id))
            {
                Cache.Remove(id);
            }
            Debug.WriteLine("キャッシュ登録");
            Cache.Add(id, new ContentsCache { CachedAt = DateTime.Now, Contents = contents.ToArray() });
            return View(contents);
        }

        public class UnregisteredContent
        {
            public string Title { get; set; }
            public int FileId { get; set; }
            public int DownloadCount { get; set; }
        }

        [UploadUserRedirect]
        public ActionResult UnregisteredContents()
        {
            var files = from File file in _dbContext.Files.Where(file => file.DocumentId == -1 && !string.IsNullOrEmpty(file.OneDriveFileId)) select file;
            var list = new List<UnregisteredContent>();
            foreach (var file in files)
            {
                list.Add(new UnregisteredContent
                {
                    Title = file.DocumentName,
                    DownloadCount = file.DownloadCount,
                    FileId = file.Id
                });
            }
            return View(list);
        }

        private static string GetTitleById(int id)
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            var str = wc.DownloadString($"https://ipsj.ixsq.nii.ac.jp/ej/?action=repository_uri&item_id={id}");
            var reg = new Regex(@"<meta name=""citation_title"" content=""(?<title>.+?)""/>");
            return reg.Match(str).Groups["title"].Value;
        }

        [Authorize(Roles = nameof(Role.Administrator) + ", " + nameof(Role.Upload))]
        public async Task<ActionResult> AddFile(int id = -1)
        {
            ViewBag.AlreadyExisting = false;

            File file;
            if (User.IsInRole(nameof(Role.Upload)))
            {
                var fileId = (await _userManager.FindByNameAsync(User.Identity.Name)).FileId;
                if (fileId == -1) return View("Error");
                file = _dbContext.Files.Find(fileId);

                ViewBag.FileName = file.DocumentName;
                ViewBag.fileId = file.Id;
                ViewBag.AlreadyExisting = !string.IsNullOrEmpty(file.OneDriveFileId);

                return View();
            }

            var title = GetTitleById(id);

            file = _dbContext.Files.FirstOrDefault(x => x.DocumentId == id);
            if (file == null)
            {
                var fileId = UtilService.GetOrCreateFileByTitle(_dbContext, title);
                file = _dbContext.Files.Find(fileId);
            }

            ViewBag.fileId = file.Id;
            ViewBag.FileName = title;
            ViewBag.AlreadyExisting = !string.IsNullOrEmpty(file.OneDriveFileId);

            return View();
        }

        [Authorize(Roles = nameof(Role.Administrator))]
        public ActionResult ChangeAssosiation(int fileId)
        {
            throw new NotImplementedException();

            var file = _dbContext.Files.Find(fileId);
            ViewBag.Title = file.DocumentName;
            var model = new ChangeAssosiationViewModel { FileId = file.Id, CurrentId = file.DocumentId, NewId = file.DocumentId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.Administrator))]
        public ActionResult ChangeAssosiationResult(ChangeAssosiationViewModel model)
        {
            throw new NotImplementedException();

            var file = _dbContext.Files.Find(model.FileId);
            if (file.DocumentId != model.CurrentId) return View(false);
            file.DocumentId = model.NewId;
            _dbContext.SaveChanges();

            //キャッシュをクリアする
            var cache = Cache.Where(x => x.Value.Contents.Any(y => y.FileId == model.FileId)).ToArray();
            foreach (var c in cache)
            {
                Cache.Remove(c.Key);
            }

            return View(true);
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private static long GetUnixTime(DateTime targetTime)
        {
            targetTime = targetTime.ToUniversalTime();
            var elapsedTime = targetTime - UnixEpoch;
            return (long)elapsedTime.TotalSeconds;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.Administrator) + ", " + nameof(Role.Upload))]
        public async Task<ActionResult> Upload(int fileId)
        {
            //許可された拡張子
            var allowedExtentions = new[]
            {
                ".pptx",
                ".ppt",
                ".pptm",
                ".ppsx",
                ".pps",
                ".ppsm",
                ".pdf",
                ".zip"
            };
            ViewBag.success = false;

            //ファイルが無い
            if (Request.Form.Files.Count <= 0) return View("Error");
            var fileData = Request.Form.Files[0];
            if (fileData?.FileName == null || fileData.Length <= 0) return View("Error");

            //許可された拡張子ではない
            if (allowedExtentions.All(ext => Path.GetExtension(fileData.FileName).ToLower() != ext))
            {
                return View("UploadFinished");
            }

            //ファイル名は"UnixTime_元のファイル名"
            var fileName = $"{GetUnixTime(DateTime.Now)}_{Path.GetFileName(fileData.FileName)}";
            var tempFileName = Path.GetTempFileName();
            using (var inputStream = new FileStream(tempFileName, FileMode.Create))
            {
                await fileData.CopyToAsync(inputStream);
            }
            var stream = new FileStream(tempFileName, FileMode.Open, FileAccess.Read, FileShare.None, 8, FileOptions.DeleteOnClose);

            try
            {
                var accountId = _configService.GetConfiguration<int>("UsingMicrosoftAccountId");
                var onedriveFileId = await _oneDriveService.UploadAsync(_dbContext, accountId, fileName, stream);

                if (_config.EnableAccountAutoChange)
                {
                    var currentAccount = await _oneDriveService.GetOwnerInfoAsync(_dbContext.Accounts.Find(accountId).RefleshToken);
                    var threshold = _config.ChangeThreshold;
                    if ((double)currentAccount.FreeSpace / 1024 / 1024 < threshold)
                    {
                        var preload = _dbContext.Accounts.Select(x => _oneDriveService.GetOwnerInfoAsync(x.RefleshToken));
                        await Task.WhenAll(preload);
                        var nextAccount = preload.Select(x => x.Result).FirstOrDefault(x => (double)x.FreeSpace / 1024 / 1024 >= threshold);
                        if (nextAccount != null) _configService.SetConfiguration("UsingMicrosoftAccountId", nextAccount.Id);
                    }
                }

                if (string.IsNullOrWhiteSpace(onedriveFileId))
                {
                    ViewBag.success = false;
                    return View("UploadFinished");
                }

                if (User.IsInRole(nameof(Role.Upload)))
                {
                    var user = await _userManager.GetUserAsync(User);
                    fileId = user.FileId;
                }

                var file = _dbContext.Files.Find(fileId);
                //既に登録済みのタイトルである
                if (!string.IsNullOrEmpty(file.OneDriveFileId)) return View("Error");

                file.MicrosoftAccountId = accountId;
                file.OneDriveFileId = onedriveFileId;
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception)
            {
                return View("Error");
            }

            ViewBag.success = true;

            //キャッシュをクリアする
            Cache.Clear();

            return View("UploadFinished");
        }

        public async Task<ActionResult> ShowDocument(int fileId)
        {
            if (User.IsInRole(nameof(Role.Upload)))
            {
                fileId = (await _userManager.FindByNameAsync(User.Identity.Name)).FileId;
            }
            var file = _dbContext.Files.Find(fileId);

            file.DownloadCount++;
            _dbContext.SaveChanges();
            var shareInfo = await _oneDriveService.CreateShareLinkAsync(_dbContext, file.MicrosoftAccountId, file.OneDriveFileId);
            _tasks.Add(DeleteShareLinkTask(file.MicrosoftAccountId, file.OneDriveFileId, shareInfo.PermissionId));
            _tasks = _tasks.Where(x => !x.IsCompleted || !x.IsCanceled || !x.IsFaulted).ToList(); //完了済みのものを取り除く
            return Redirect(shareInfo.Url);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.Administrator) + ", " + nameof(Role.Upload))]
        public async Task<ActionResult> DeleteDocument(int fileId)
        {
            if (User.IsInRole(nameof(Role.Upload)))
            {
                var user = await _userManager.GetUserAsync(User);
                fileId = user.FileId;
            }

            var file = _dbContext.Files.Find(fileId);

            if (string.IsNullOrEmpty(file.OneDriveFileId)) return View("Error");

            await _oneDriveService.DeleteAsync(_dbContext, file.MicrosoftAccountId, file.OneDriveFileId);
            file.MicrosoftAccountId = -1;
            file.OneDriveFileId = null;
            await _dbContext.SaveChangesAsync();

            //キャッシュをクリアする
            var cache = Cache.Where(x => x.Value.Contents.Any(y => y.FileId == fileId)).ToArray();
            foreach (var c in cache)
            {
                Cache.Remove(c.Key);
            }

            return View();
        }
    }

    /// <summary>
    /// Uploadユーザをリダイレクトさせる
    /// </summary>
    public class UploadUserRedirectAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.IsInRole(nameof(Role.Upload)))
            {
                context.Result = new RedirectResult("/Home/AddFile");
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}