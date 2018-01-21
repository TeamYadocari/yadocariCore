#region Copyright
// /*
//  * OneDriveService.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YadocariCore.Models;

namespace YadocariCore.Services
{
    public class OneDriveService
    {
        private const string ApiEndPoint = "https://api.onedrive.com/v1.0";
        public readonly string ClientId;
        private readonly string _clientSecret;
        private readonly string _serverUrl;

        public OneDriveService(string clientId, string clientSecret, string serverUrl)
        {
            ClientId = clientId;
            _clientSecret = clientSecret;
            _serverUrl = serverUrl;
        }

        public class ShareInfo
        {
            public string PermissionId { get; set; }
            public string Url { get; set; }
        }

        public class OwnerInfo
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public long FreeSpace { get; set; }
        }

        public async Task<string> GetRefreshTokenAsync(string code)
        {
            var url = "https://login.live.com/oauth20_token.srf";
            var hc = new HttpClient();

            var param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", ClientId },
                {"client_secret", _clientSecret },
                {"code", code },
                {"grant_type", "authorization_code" },
                {"redirect_uri", $"{_serverUrl}/Manage/AddMicrosoftAccountCallback" }
            });

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
            return dynamicResult.refresh_token;
        }

        public async Task<OwnerInfo> GetOwnerInfoAsync(string refleshToken)
        {
            var token = await GetAccessTokenAsync(refleshToken);

            var url = ApiEndPoint + "/drive";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.GetAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
            return new OwnerInfo
            {
                Id = dynamicResult.owner?.user.id,
                DisplayName = dynamicResult.owner?.user.displayName,
                FreeSpace = dynamicResult.quota?.remaining
            };
        }

        private async Task<string> GetAccessTokenAsync(string refleshToken)
        {
            var url = "https://login.live.com/oauth20_token.srf";
            var hc = new HttpClient();

            var param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", ClientId },
                {"client_secret", _clientSecret },
                {"refresh_token", refleshToken },
                {"grant_type", "refresh_token" }
            });

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
            return dynamicResult.access_token;
        }

        private async Task<string> UploadAsync(string token, string filename, Stream file)
        {
            var url = ApiEndPoint + $"/drive/root:/{filename}:/content";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var streamContent = new StreamContent(file);

            var response = await hc.PutAsync(url, streamContent);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
            return dynamicResult.id;
        }

        //Uploadするファイルが100MBより大きい
        private async Task<string> UploadLargeFileAsync(string token, string filename, Stream file)
        {
            var url = ApiEndPoint + $"/drive/root:/{filename}:/upload.createSession";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.PostAsync(url, null);
            var result = await response.Content.ReadAsStringAsync();
            var uploadUrl = JsonConvert.DeserializeObject<dynamic>(result).uploadUrl;

            var fileSize = file.Length;

            var uploadedSize = 0;
            const int fragmentSize = 60 * 1024 * 1024; //60MiB
            var buffer = new byte[fragmentSize];
            while (uploadedSize < fileSize)
            {
                var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                var readSize = 0;
                if (fileSize - uploadedSize >= fragmentSize)
                {
                    while (readSize < fragmentSize) readSize += file.Read(buffer, readSize, fragmentSize);
                    request.Content = new ByteArrayContent(buffer);
                    request.Content.Headers.ContentRange = new ContentRangeHeaderValue(uploadedSize, uploadedSize + fragmentSize - 1, fileSize);
                }
                else
                {
                    buffer = new byte[fileSize - uploadedSize];
                    while (readSize < fileSize - uploadedSize) readSize += file.Read(buffer, readSize, (int)fileSize - uploadedSize);
                    request.Content = new ByteArrayContent(buffer);
                    request.Content.Headers.ContentRange = new ContentRangeHeaderValue(uploadedSize, fileSize - 1, fileSize);
                }

                response = await hc.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    uploadedSize += fragmentSize;
                }
                else
                {
                    throw new Exception("Error: " + response.ReasonPhrase);
                }
            }

            result = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<dynamic>(result).id;
        }


        private async Task<ShareInfo> CreateShareLinkAsync(string token, string fileId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}/action.createLink";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var param = new StringContent(@"{""type"": ""view""}", Encoding.UTF8, "application/json");

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
            return new ShareInfo { PermissionId = dynamicResult.id, Url = dynamicResult.link.webUrl };
        }

        private async Task DeleteShareLinkAsync(string token, string fileId, string permissionId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}/permissions/{permissionId}";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.DeleteAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
        }

        private async Task DeleteAsync(string token, string fileId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.DeleteAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = JsonConvert.DeserializeObject<dynamic>(result);
        }

        public async Task<string> UploadAsync(OneDriveDbContext dbContext, int accountNum, string filename, Stream file)
        {
            var refreshToken = dbContext.Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessTokenAsync(refreshToken);
            string fileId;

            if (file.Length < 100 * 1024 * 1024)
            {
                fileId = await UploadAsync(token, filename, file);
            }
            else
            {
                fileId = await UploadLargeFileAsync(token, filename, file);
            }

            return fileId;
        }

        public async Task<ShareInfo> CreateShareLinkAsync(OneDriveDbContext dbContext, int accountNum, string fileId)
        {
            var refreshToken = dbContext.Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessTokenAsync(refreshToken);
            var shareinfo = await CreateShareLinkAsync(token, fileId);

            return shareinfo;
        }

        public async Task DeleteShareLinkAsync(OneDriveDbContext dbContext, int accountNum, string fileId, string permissionId)
        {
            var refreshToken = dbContext.Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessTokenAsync(refreshToken);
            await DeleteShareLinkAsync(token, fileId, permissionId);
        }

        public async Task DeleteAsync(OneDriveDbContext dbContext, int accountNum, string fileId)
        {
            var refreshToken = dbContext.Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessTokenAsync(refreshToken);
            await DeleteAsync(token, fileId);

        }
    }
}