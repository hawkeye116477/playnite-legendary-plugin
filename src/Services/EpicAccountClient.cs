﻿using CommonPlugin;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LegendaryLibraryNS.Services
{
    public class TokenException : Exception
    {
        public TokenException(string message) : base(message)
        {
        }
    }

    public class ApiRedirectResponse
    {
        public string redirectUrl { get; set; }
        public string sid { get; set; }
        public string authorizationCode { get; set; }
    }

    public class EpicAccountClient
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI api;
        private string tokensPath;
        private readonly string loginUrl = "https://www.epicgames.com/id/login?responseType=code";
        public static string authCodeUrl = "https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code";
        private readonly string oauthUrl = @"";
        private readonly string accountUrl = @"";
        private readonly string catalogUrl = @"";
        private readonly string playtimeUrl = @"";
        private readonly string libraryItemsUrl = @"";
        private const string authEncodedString = "MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=";
        private const string userAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) EpicGamesLauncher";

        public EpicAccountClient(IPlayniteAPI api)
        {
            this.api = api;
            tokensPath = LegendaryLauncher.TokensPath;
            var oauthUrlMask = @"https://{0}/account/api/oauth/token";
            var accountUrlMask = @"https://{0}/account/api/public/account/";
            var libraryItemsUrlMask = @"https://{0}/library/api/public/items?includeMetadata=true&platform=Windows";
            var catalogUrlMask = @"https://{0}/catalog/api/shared/namespace/";
            var playtimeUrlMask = @"https://{0}/library/api/public/playtime/account/{1}/all";

            var loadedFromConfig = false;

            if (!loadedFromConfig)
            {
                oauthUrl = string.Format(oauthUrlMask, "account-public-service-prod03.ol.epicgames.com");
                accountUrl = string.Format(accountUrlMask, "account-public-service-prod03.ol.epicgames.com");
                catalogUrl = string.Format(catalogUrlMask, "catalog-public-service-prod06.ol.epicgames.com");
                playtimeUrl = string.Format(playtimeUrlMask, "library-service.live.use1a.on.epicgames.com", "{0}");
                libraryItemsUrl = string.Format(libraryItemsUrlMask, "library-service.live.use1a.on.epicgames.com");
            }
        }

        public async Task Login()
        {
            var loggedIn = false;
            var apiRedirectContent = string.Empty;
            var authorizationCode = "";

            using (var view = api.WebViews.CreateView(new WebViewSettings
            {
                WindowWidth = 580,
                WindowHeight = 700,
                // This is needed otherwise captcha won't pass
                UserAgent = userAgent,
            }))
            {
                view.LoadingChanged += async (s, e) =>
                {
                    var address = view.GetCurrentAddress();
                    var pageText = await view.GetPageTextAsync();

                    if (!pageText.IsNullOrEmpty() && pageText.Contains(@"localhost") && !e.IsLoading)
                    {
                        var source = await view.GetPageSourceAsync();
                        var matches = Regex.Matches(source, @"localhost\/launcher\/authorized\?code=([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                        if (matches.Count > 0)
                        {
                            authorizationCode = matches[0].Groups[1].Value;
                            loggedIn = true;
                        }
                        view.Close();
                    }
                };

                view.DeleteDomainCookies(".epicgames.com");
                view.Navigate(loginUrl);
                view.OpenDialog();
            }

            if (!loggedIn)
            {
                return;
            }

            FileSystem.DeleteFile(tokensPath);
            if (string.IsNullOrEmpty(authorizationCode))
            {
                logger.Error("Failed to get login exchange key for Epic account.");
                return;
            }
            await AuthenticateUsingAuthCode(authorizationCode);
        }

        public async Task AuthenticateUsingAuthCode(string authorizationCode)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "basic " + authEncodedString);
            using var content = new StringContent($"grant_type=authorization_code&code={authorizationCode}&token_type=eg1");
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            var response = await httpClient.PostAsync(oauthUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var respContent = await response.Content.ReadAsStringAsync();
                FileSystem.CreateDirectory(Path.GetDirectoryName(tokensPath));
                File.WriteAllText(tokensPath, respContent);
            }
            else
            {
                logger.Error($"Failed to authenticate with the Epic Games Store. Error: {response.ReasonPhrase}");
            }
        }

        public string GetUsername()
        {
            var tokens = LoadTokens();
            var username = "";
            if (tokens != null)
            {
                if (!tokens.displayName.IsNullOrEmpty())
                {
                    username = tokens.displayName;
                }
            }
            return username;
        }

        public async Task<bool> GetIsUserLoggedIn()
        {
            var tokens = LoadTokens();
            if (tokens == null)
            {
                return false;
            }

            try
            {
                var account = await InvokeRequest<AccountResponse>(accountUrl + tokens.account_id, tokens);
                return account.Item2.id == tokens.account_id;
            }
            catch (Exception ex)
            {
                if (ex is TokenException)
                {
                    var renewSuccess = await RenewTokens(tokens.refresh_token);
                    if (renewSuccess)
                    {
                        try
                        {
                            tokens = LoadTokens();
                            if (tokens == null)
                            {
                                return false;
                            }
                            var account = await InvokeRequest<AccountResponse>(accountUrl + tokens.account_id, tokens);
                            return account.Item2.id == tokens.account_id;
                        }
                        catch (Exception ex2)
                        {
                            logger.Error(ex2, "Failed to validation Epic authentication.");
                            return false;
                        }
                    }
                }
                logger.Error(ex, "Failed to validation Epic authentication.");
                return false;
            }
        }

        public async Task<List<Asset>> GetLibraryItems()
        {
            if (!await GetIsUserLoggedIn())
            {
                throw new Exception("User is not authenticated.");
            }

            var response = await InvokeRequest<LibraryItemsResponse>(libraryItemsUrl, LoadTokens());
            var assets = new List<Asset>();
            assets.AddRange(response.Item2.records);

            string nextCursor = response.Item2.responseMetadata?.nextCursor;
            while (nextCursor != null)
            {
                response = await InvokeRequest<LibraryItemsResponse>(
                    $"{libraryItemsUrl}&cursor={nextCursor}",
                    LoadTokens());
                assets.AddRange(response.Item2.records);
                nextCursor = response.Item2.responseMetadata.nextCursor;
            }
            var filteredAssets = assets.Where(asset => !asset.appName.IsNullOrEmpty()
                                                       && asset.sandboxType != "PRIVATE"
                                                       && asset.@namespace != "ue").ToList();
            return filteredAssets;
        }

        public async Task<List<PlaytimeItem>> GetPlaytimeItems()
        {
            if (!await GetIsUserLoggedIn())
            {
                throw new Exception("User is not authenticated.");
            }

            var tokens = LoadTokens();
            var formattedPlaytimeUrl = string.Format(playtimeUrl, tokens.account_id);
            var response = await InvokeRequest<List<PlaytimeItem>>(formattedPlaytimeUrl, tokens);
            return response.Item2;
        }

        public CatalogItem GetCatalogItem(string nameSpace, string id, string cachePath)
        {
            Dictionary<string, CatalogItem> result = null;
            if (!cachePath.IsNullOrEmpty() && FileSystem.FileExists(cachePath))
            {
                try
                {
                    result = Serialization.FromJson<Dictionary<string, CatalogItem>>(FileSystem.ReadStringFromFile(cachePath));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load Epic catalog cache.");
                }
            }

            if (result == null)
            {
                var url = string.Format("{0}/bulk/items?id={1}&country=US&locale=en-US&includeMainGameDetails=true", nameSpace, id);
                var catalogResponse = InvokeRequest<Dictionary<string, CatalogItem>>(catalogUrl + url, LoadTokens()).GetAwaiter().GetResult();
                result = catalogResponse.Item2;
                FileSystem.WriteStringToFile(cachePath, catalogResponse.Item1);
            }

            if (result.TryGetValue(id, out var catalogItem))
            {
                return catalogItem;
            }
            else
            {
                throw new Exception($"Epic catalog item for {id} {nameSpace} not found.");
            }
        }

        private async Task<bool> RenewTokens(string refreshToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", "basic " + authEncodedString);
            using var content = new StringContent($"grant_type=refresh_token&refresh_token={refreshToken}&token_type=eg1");
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            var response = await httpClient.PostAsync(oauthUrl, content);
            if (response.IsSuccessStatusCode)
            {
                var respContent = await response.Content.ReadAsStringAsync();
                FileSystem.CreateDirectory(Path.GetDirectoryName(tokensPath));
                File.WriteAllText(tokensPath, respContent);
                return true;
            }
            else
            {
                logger.Error("Failed to renew tokens.");
                return false;
            }
        }

        private async Task<Tuple<string, T>> InvokeRequest<T>(string url, OauthResponse tokens) where T : class
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", tokens.token_type + " " + tokens.access_token);
                var response = await httpClient.GetAsync(url);
                var str = await response.Content.ReadAsStringAsync();

                if (Serialization.TryFromJson<ErrorResponse>(str, out var error) && !string.IsNullOrEmpty(error.errorCode))
                {
                    throw new TokenException(error.errorCode);
                }
                else
                {
                    try
                    {
                        return new Tuple<string, T>(str, Serialization.FromJson<T>(str));
                    }
                    catch
                    {
                        // For cases like #134, where the entire service is down and doesn't even return valid error messages.
                        logger.Error(str);
                        throw new Exception("Failed to get data from Epic service.");
                    }
                }
            }
        }

        private OauthResponse LoadTokens()
        {
            if (File.Exists(tokensPath))
            {
                try
                {
                    return Serialization.FromJson<OauthResponse>(FileSystem.ReadFileAsStringSafe(tokensPath));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }

            return null;
        }

        public void UploadPlaytime(DateTime startTime, DateTime endTime, Game game, int attempts = 3)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonUploadingPlaytime, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false);
            api.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                a.IsIndeterminate = true;
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    var userLoggedIn = await GetIsUserLoggedIn();
                    if (userLoggedIn)
                    {
                        var userData = LoadTokens();
                        if (userData != null)
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", userData.token_type + " " + userData.access_token);
                            var uri = $"https://library-service.live.use1a.on.epicgames.com/library/api/public/playtime/account/{userData.account_id}";
                            PlaytimePayload playtimePayload = new PlaytimePayload
                            {
                                artifactId = game.GameId,
                                machineId = LegendaryLibrary.GetSettings().SyncPlaytimeMachineId
                            };
                            DateTime now = DateTime.UtcNow;
                            playtimePayload.endTime = endTime;
                            playtimePayload.startTime = startTime;
                            var playtimeJson = Serialization.ToJson(playtimePayload);
                            var content = new StringContent(playtimeJson, Encoding.UTF8, "application/json");
                            try
                            {
                                var response = await httpClient.PutAsync(uri, content);
                                response.EnsureSuccessStatusCode();
                            }
                            catch (HttpRequestException exception)
                            {
                                if (attempts > 1)
                                {
                                    attempts -= 1;
                                    logger.Debug($"Retrying playtime upload for {game.Name}. Attempts left: {attempts}");
                                    await Task.Delay(2000);
                                    UploadPlaytime(startTime, endTime, game, attempts);
                                }
                                else
                                {
                                    api.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonUploadPlaytimeError, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }));
                                    logger.Error($"An error occured during uploading playtime to the cloud: {exception}.");
                                }
                            }
                        }
                    }
                    else
                    {
                        logger.Error($"Can't upload playtime, because user is not authenticated.");
                        api.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyEpicNotLoggedInError));
                    }
                }
            }, globalProgressOptions);
        }
    }
}
