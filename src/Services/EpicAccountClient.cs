using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using LegendaryLibraryNS.Models;
using Playnite;
using Playnite.WebViews;

namespace LegendaryLibraryNS.Services;

public class TokenException(string message) : Exception(message);

public class ApiRedirectResponse
{
    public string? RedirectUrl { get; set; }
    public string? Sid { get; set; }
    public string? AuthorizationCode { get; set; }
}

public class EpicAccountClient
{
    private ILogger logger = LogManager.GetLogger<EpicAccountClient>();
    private IPlayniteApi api;
    private string TokensPath { get; set; }
    private readonly string loginUrl = "https://www.epicgames.com/id/login?responseType=code";
    private readonly string graphqlUrl = "https://launcher.store.epicgames.com/graphql";

    public static string AuthCodeUrl =
        "https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code";

    private readonly string oauthUrl = @"";
    private readonly string accountUrl = @"";
    private readonly string catalogUrl = @"";
    private readonly string playtimeUrl = @"";
    private readonly string libraryItemsUrl = @"";

    private const string AuthEncodedString =
        "MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=";

    private const string UserAgent =
        @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) EpicGamesLauncher/18.9.0-45233261+++Portal+Release-Live";


    private static readonly RetryHandler RetryHandler = new(new HttpClientHandler());
    private static readonly HttpClient HttpClient = new(RetryHandler);

    public EpicAccountClient(IPlayniteApi api)
    {
        this.api = api;
        TokensPath = LegendaryLauncher.TokensPath;
        const string oauthUrlMask = @"https://{0}/account/api/oauth/token";
        const string accountUrlMask = @"https://{0}/account/api/public/account/";
        const string libraryItemsUrlMask =
            @"https://{0}/library/api/public/items?includeMetadata=true&platform=Windows";
        const string catalogUrlMask = @"https://{0}/catalog/api/shared/namespace/";
        const string playtimeUrlMask = @"https://{0}/library/api/public/playtime/account/{1}/all";

        oauthUrl = string.Format(oauthUrlMask, "account-public-service-prod03.ol.epicgames.com");
        accountUrl = string.Format(accountUrlMask, "account-public-service-prod03.ol.epicgames.com");
        catalogUrl = string.Format(catalogUrlMask, "catalog-public-service-prod06.ol.epicgames.com");
        playtimeUrl = string.Format(playtimeUrlMask, "library-service.live.use1a.on.epicgames.com", "{0}");
        libraryItemsUrl = string.Format(libraryItemsUrlMask, "library-service.live.use1a.on.epicgames.com");
    }

    public async Task Login()
    {
        var loggedIn = false;
        var apiRedirectContent = string.Empty;
        var authorizationCode = "";

        using var view = api.WebView.CreateView(new WebViewSettings
        {
            WindowWidth = 580,
            WindowHeight = 700,
            // This is needed otherwise captcha won't pass
            UserAgent = UserAgent
        });
        view.LoadingChangedCallbackAsync = async e =>
        {
            var address = view.GetCurrentAddress();
            var pageText = await view.GetPageTextAsync();

            if (!pageText.IsNullOrEmpty() && pageText.Contains(@"localhost") && !e.IsLoading)
            {
                var source = await view.GetPageSourceAsync();
                var matches = Regex.Matches(source, @"localhost\/launcher\/authorized\?code=([a-zA-Z0-9]+)",
                    RegexOptions.IgnoreCase);
                if (matches.Count > 0 && matches[0].Groups.Count > 1)
                {
                    authorizationCode = matches[0].Groups[1].Value;
                    if (!authorizationCode.IsNullOrWhiteSpace())
                    {
                        loggedIn = true;
                    }
                }

                view.Close();
            }
        };

        view.WebViewInitializedCallbackAsync = async _ =>
        {
            await view.DeleteDomainCookiesAsync(".epicgames.com");
            view.Navigate(loginUrl);
        };
        await view.OpenDialogAsync();

        if (!loggedIn)
        {
            return;
        }

        await LegendaryLauncher.RemoveAllTokens();

        if (string.IsNullOrEmpty(authorizationCode))
        {
            logger.Error("Failed to get login exchange key for Epic account.");
            return;
        }

        await AuthenticateUsingAuthCode(authorizationCode);
    }

    public async Task AuthenticateUsingAuthCode(string authorizationCode)
    {
        using var content =
            new StringContent($"grant_type=authorization_code&code={authorizationCode}&token_type=eg1");
        content.Headers.Clear();
        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

        var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
        {
            Content = content
        };
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Authorization", "basic " + AuthEncodedString);

        try
        {
            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var respContent = await response.Content.ReadAsStringAsync();
            await WriteTokens(respContent);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to authenticate with the Epic Games Store");
        }
    }

    public string GetUsername()
    {
        var tokens = LoadTokens();
        var username = "";
        if (tokens != null)
        {
            if (!tokens.DisplayName.IsNullOrEmpty())
            {
                username = tokens.DisplayName;
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
            var account = await InvokeRequest<AccountResponse>(accountUrl + tokens.Account_id, tokens);
            return account.Item2.Id == tokens.Account_id;
        }
        catch (Exception ex)
        {
            if (ex is TokenException)
            {
                var renewSuccess = await RenewTokens(tokens.Refresh_token);
                if (renewSuccess)
                {
                    try
                    {
                        tokens = LoadTokens();
                        if (tokens == null)
                        {
                            return false;
                        }

                        var account = await InvokeRequest<AccountResponse>(accountUrl + tokens.Account_id, tokens);
                        return account.Item2.Id == tokens.Account_id;
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
        var tokens = LoadTokens();
        if (!await GetIsUserLoggedIn() || tokens == null)
        {
            throw new Exception("User is not authenticated.");
        }

        var assets = new List<Asset>();
        var response = await InvokeRequest<LibraryItemsResponse>(libraryItemsUrl, tokens);
        assets.AddRange(response.Item2.Records);

        var nextCursor = response.Item2.ResponseMetadata?.NextCursor;
        while (nextCursor != null)
        {
            response = await InvokeRequest<LibraryItemsResponse>(
                $"{libraryItemsUrl}&cursor={nextCursor}",
                tokens);
            assets.AddRange(response.Item2.Records);
            nextCursor = response.Item2.ResponseMetadata?.NextCursor;
        }

        var filteredAssets = assets.Where(asset => !asset.AppName.IsNullOrEmpty()
                                                   && asset.SandboxType != "PRIVATE"
                                                   && asset.Namespace != "ue")
                                   .ToList();
        return filteredAssets;
    }

    public async Task<List<PlaytimeItem>> GetPlaytimeItems()
    {
        var tokens = LoadTokens();
        if (!await GetIsUserLoggedIn() || tokens == null)
        {
            throw new Exception("User is not authenticated.");
        }

        var formattedPlaytimeUrl = string.Format(playtimeUrl, tokens.Account_id);
        var response = await InvokeRequest<List<PlaytimeItem>>(formattedPlaytimeUrl, tokens);
        return response.Item2;
    }


    public async Task<CatalogItem?> GetCatalogItem(string nameSpace, string id, string cachePath)
    {
        Dictionary<string, CatalogItem>? result = null;
        if (!cachePath.IsNullOrEmpty() && File.Exists(cachePath))
        {
            try
            {
                result = Serialization.FromJson<Dictionary<string, CatalogItem>>(
                    FileSystem.ReadStringFromFile(cachePath));
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to load Epic catalog cache.");
            }
        }

        if (result == null)
        {
            var tokens = LoadTokens();
            if (tokens != null)
            {
                var url = $"{nameSpace}/bulk/items?id={id}&country=US&locale=en-US&includeMainGameDetails=true";
                var catalogResponse =
                    await InvokeRequest<Dictionary<string, CatalogItem>>(catalogUrl + url, tokens);
                result = catalogResponse.Item2;
                FileSystem.WriteStringToFile(cachePath, catalogResponse.Item1);
            }
        }

        if (result != null && result.TryGetValue(id, out var catalogItem))
        {
            return catalogItem;
        }

        throw new Exception($"Epic catalog item for {id} {nameSpace} not found.");
    }

    private async Task<bool> RenewTokens(string refreshToken)
    {
        using var content =
            new StringContent($"grant_type=refresh_token&refresh_token={refreshToken}&token_type=eg1");
        content.Headers.Clear();
        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
        var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
        {
            Content = content
        };
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Authorization", "basic " + AuthEncodedString);

        using var response = await HttpClient.SendAsync(request);
        try
        {
            response.EnsureSuccessStatusCode();
            var respContent = await response.Content.ReadAsStringAsync();
            await WriteTokens(respContent);
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to renew tokens.");
            return false;
        }
    }

    private async Task<Tuple<string, T>> InvokeRequest<T>(string url, OauthResponse tokens) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Authorization", tokens.Token_type + " " + tokens.Access_token);

        using var response = await HttpClient.SendAsync(request);
        var str = await response.Content.ReadAsStringAsync();
        if (Serialization.TryFromJson<ErrorResponse>(str, out var error, false) && !string.IsNullOrEmpty(error?.ErrorCode))
        {
            throw new TokenException(error.ErrorCode);
        }

        try
        {
            return new Tuple<string, T>(str, Serialization.FromJson<T>(str)!);
        }
        catch
        {
            // For cases like #134, where the entire service is down and doesn't even return valid error messages.
            logger.Error(str);
            throw new Exception("Failed to get data from Epic service.");
        }
    }

    public OauthResponse? LoadTokens()
    {
        var newEncryptedTokensPath = "";
        if (!LegendaryLauncher.GetUserInfo().Account_id.IsNullOrEmpty())
        {
            newEncryptedTokensPath =
                Path.Combine(LegendaryLauncher.ConfigPath, $"{LegendaryLauncher.GetUserInfo().Account_id.GetMD5()}.enc");
        }

        try
        {
            if (File.Exists(LegendaryLauncher.OldPluginEncryptedTokensPath))
            {
                try
                {
                    logger.Debug("Migrating tokens to new encryption format...");
                    var decryptedTokens = Encryption.DecryptFromFile(LegendaryLauncher.OldPluginEncryptedTokensPath,
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User?.Value!);
                    var tokenInfo = Serialization.FromJson<OauthResponse>(decryptedTokens);
                    if (tokenInfo != null)
                    {
                        LegendaryEncryption.Encrypt(
                            Path.Combine(LegendaryLauncher.ConfigPath, $"{tokenInfo.Account_id.GetMD5()}.enc"),
                            decryptedTokens);
                    }

                    FileSystem.DeleteFileSafe(LegendaryLauncher.OldPluginEncryptedTokensPath);
                    return Serialization.FromJson<OauthResponse>(decryptedTokens);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to decrypt tokens.");
                    FileSystem.DeleteFileSafe(LegendaryLauncher.OldPluginEncryptedTokensPath);
                }
            }
            else if (newEncryptedTokensPath != "" && File.Exists(newEncryptedTokensPath))
            {
                //logger.Trace("Loading encrypted tokens...");
                var decryptedTokens = LegendaryEncryption.Decrypt(newEncryptedTokensPath);
                if (!decryptedTokens.IsNullOrEmpty())
                {
                    return Serialization.FromJson<OauthResponse>(decryptedTokens ?? "");
                }
            }
            else if (File.Exists(TokensPath))
            {
                //logger.Trace("Loading non-encrypted tokens...");
                return Serialization.FromJson<OauthResponse>(FileSystem.ReadFileAsStringSafe(TokensPath));
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to load saved tokens.");
        }

        return null;
    }

    private async Task WriteTokens(string content)
    {
        if (!content.IsNullOrEmpty())
        {
            bool useEncryptedTokens = true;
            if (LegendaryLauncher.IsInstalled)
            {
                var versionCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                          .WithArguments(["-V"])
                                          .WithEnvironmentVariables(LegendaryLauncher.GetDefaultEnvironmentVariables())
                                          .AddCommandToLog()
                                          .WithValidation(CommandResultValidation.None)
                                          .ExecuteBufferedAsync();
                if (!versionCmd.StandardOutput.Contains("hawkeye116477"))
                {
                    useEncryptedTokens = false;
                }
            }

            if (useEncryptedTokens)
            {
                var tokenInfo = Serialization.FromJson<OauthResponse>(content);
                if (tokenInfo != null)
                {
                    LegendaryEncryption.Encrypt(Path.Combine(LegendaryLauncher.ConfigPath, $"{tokenInfo.Account_id.GetMD5()}.enc"),
                        content);
                }
            }
            else
            {
                await File.WriteAllTextAsync(LegendaryLauncher.TokensPath, content);
            }
        }
    }

    private async Task<PlayerAchievementsResponse> GetPlayerAchievements(
        string productId, OauthResponse tokens, CancellationToken cancelToken)
    {
        var playerAchievementsResponse = new PlayerAchievementsResponse();

        var variables = new
        {
            EpicAccountId = tokens.Account_id,
            ProductId = productId,
        };

        var query = @"
query playerProfileAchievementsByProductId($epicAccountId: String!, $productId: String!) {
  PlayerProfile {
    playerProfile(epicAccountId: $epicAccountId) {
      productAchievements(productId: $productId) {
        ... on PlayerProductAchievementsResponseSuccess {
          data {
            playerAchievements {
              playerAchievement {
                achievementName
                unlocked
                unlockDate
              }
            }
          }
        }
      }
    }
  }
}";
        var achievementsPayload = new
        {
            query,
            variables,
        };
        var request = new HttpRequestMessage(HttpMethod.Post, graphqlUrl);
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Authorization", tokens.Token_type + " " + tokens.Access_token);
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent(Serialization.ToJson(achievementsPayload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await HttpClient.SendAsync(request, cancelToken);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync(cancelToken);
            if (!responseContent.IsNullOrEmpty())
            {
                if (Serialization.TryFromJson(responseContent, out PlayerAchievementsResponse? newPlayerAchievements))
                {
                    if (newPlayerAchievements != null)
                    {
                        playerAchievementsResponse = newPlayerAchievements;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return playerAchievementsResponse;
    }

    private async Task<AchievementsSchemaResponse> GetAchievementsSchema(
        string sandboxId, OauthResponse tokens, CancellationToken cancelToken)
    {
        var achievementsSchemaResponse = new AchievementsSchemaResponse();

        var cacheAchievementsPath = LegendaryLibrary.GetCachePath("achievements");
        Directory.CreateDirectory(cacheAchievementsPath);
        var cacheFile = Path.Combine(cacheAchievementsPath, $"schema_{sandboxId}.json");
        if (File.Exists(cacheFile))
        {
            if (File.GetLastWriteTime(cacheFile) < DateTime.Now.AddDays(-7))
            {
                File.Delete(cacheFile);
            }
        }

        var correctJson = false;
        if (File.Exists(cacheFile))
        {
            var content = FileSystem.ReadFileAsStringSafe(cacheFile);
            if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out AchievementsSchemaResponse? newSchemaResponse))
            {
                if (newSchemaResponse != null)
                {
                    achievementsSchemaResponse = newSchemaResponse;
                    correctJson = true;
                }
            }
        }

        if (correctJson)
        {
            return achievementsSchemaResponse;
        }

        var variables = new
        {
            SandboxId = sandboxId,
            Locale = "en-US"
        };

        var query = @"
query Achievement($sandboxId: String!, $locale: String!) {
  Achievement {
    productAchievementsRecordBySandbox(sandboxId: $sandboxId, locale: $locale) {
      productId
      achievementSets {
        achievementSetId
        isBase
        totalAchievements
        totalXP
      }
      platinumRarity {
        percent
      }
      achievements {
        achievement {
          name
          hidden
          isBase
          unlockedDisplayName
          lockedDisplayName
          unlockedDescription
          lockedDescription
          unlockedIconId
          lockedIconId
          XP
          flavorText
          unlockedIconLink
          lockedIconLink
          tier {
            name
            hexColor
            min
            max
          }
          rarity {
            percent
          }
        }
      }
    }
  }
}";
        var achievementsPayload = new
        {
            query,
            variables,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, graphqlUrl);
        request.Headers.Add("User-Agent", UserAgent);
        request.Headers.Add("Authorization", tokens.Token_type + " " + tokens.Access_token);
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent(Serialization.ToJson(achievementsPayload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await HttpClient.SendAsync(request, cancelToken);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync(cancelToken);
            if (!responseContent.IsNullOrEmpty())
            {
                if (Serialization.TryFromJson(responseContent, out AchievementsSchemaResponse? newAchievementsSchema))
                {
                    if (newAchievementsSchema != null)
                    {
                        achievementsSchemaResponse = newAchievementsSchema;
                        await File.WriteAllTextAsync(cacheFile, responseContent, cancelToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        return achievementsSchemaResponse;
    }

    public async Task<List<ImportableAchievement>> GetAchievements(string gameId, OauthResponse tokens, CancellationToken cancelToken)
    {
        var importableAchievements = new List<ImportableAchievement>();
        var cacheDir = LegendaryLibrary.GetCachePath("catalog");
        var cacheFile = $"{gameId}.json";
        cacheFile = Path.Combine(cacheDir, cacheFile);
        var newSandboxId = "";
        if (File.Exists(cacheFile))
        {
            var cacheContent = await File.ReadAllTextAsync(cacheFile, cancelToken);
            if (!cacheContent.IsNullOrEmpty())
            {
                if (Serialization.TryFromJson<Dictionary<string, CatalogItem>>(cacheContent, out var catalogItem))
                {
                    newSandboxId = catalogItem?.First().Value.Namespace;
                }
            }
        }

        if (newSandboxId.IsNullOrEmpty())
        {
            var assets = await GetLibraryItems();
            var chosenAssets = assets.Where(a => a.AppName == gameId).ToList();
            newSandboxId = chosenAssets.FirstOrDefault()?.Namespace;
        }

        if (newSandboxId.IsNullOrEmpty())
        {
            return importableAchievements;
        }

        var schema = await GetAchievementsSchema(newSandboxId, tokens, cancelToken);
        var productId = schema.Data.Achievement.ProductAchievementsRecordBySandbox.ProductId;
        if (productId.IsNullOrEmpty())
        {
            return importableAchievements;
        }

        var playerAchievementsResponse = await GetPlayerAchievements(productId, tokens, cancelToken);
        var playerAchievements = playerAchievementsResponse.Data.PlayerProfile.PlayerProfileInfo.ProductAchievements.Data
                                                           .PlayerAchievements;

        var unlockedMap = playerAchievements.Where(x => !string.IsNullOrWhiteSpace(x.PlayerAchievement.AchievementName))
                                            .ToDictionary(
                                                 x => x.PlayerAchievement.AchievementName,
                                                 x => x.PlayerAchievement,
                                                 StringComparer.OrdinalIgnoreCase);
        var allAchievements = schema.Data.Achievement.ProductAchievementsRecordBySandbox.Achievements;
        if (allAchievements.Count <= 0)
        {
            return importableAchievements;
        }

        foreach (var achievement in allAchievements)
        {
            var achievementData = achievement.Achievement;
            var unlockDate = "";
            double progress = 0;

            if (unlockedMap.TryGetValue(achievementData.Name, out var progressItem) && progressItem.Unlocked)
            {
                unlockDate = progressItem.UnlockDate;
                progress = progressItem.Progress;
            }

            var description = achievementData.LockedDescription;
            var displayName = achievementData.LockedDisplayName;
            if (unlockDate != "")
            {
                description = achievementData.UnlockedDescription;
                displayName = achievementData.UnlockedDisplayName;
            }

            if (achievementData.Hidden)
            {
                description = "";
                displayName = "Hidden achievement";
            }

            double rarity = 100;
            if (achievementData.Rarity != null)
            {
                rarity = achievementData.Rarity.Percent;
            }

            var newAchievement =
                new ImportableAchievement(achievementData.Name, displayName)
                {
                    Description = description,
                    Hidden = achievementData.Hidden,
                    Progress = (int)progress,
                    Rarity = rarity,
                    LockedIcon = achievementData.LockedIconLink,
                    UnlockedIcon = achievementData.UnlockedIconLink,
                    TrophyType = new ImportableGameAchievementTrophy(achievementData.Tier?.Name ?? "",
                        achievementData.Tier?.Name ?? "", null)
                };
            if (unlockDate != "")
            {
                newAchievement.UnlockedDate = DateTime.ParseExact(unlockDate, "yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal);
            }

            importableAchievements.Add(newAchievement);
        }

        return importableAchievements;
    }
}