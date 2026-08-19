using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shared.Steam;
using Shared.UGC.Steam;
using Steamworks;

namespace QoLiTea.Features.WorkshopAutoScan;

public sealed class WorkshopRecentItem
{
    public ulong FileId;
    public string Title;
    public ulong OwnerId;
    public string Author;
    public string PreviewUrl;
    public string Description;
}

/// <summary>
/// Recent Workshop publishes via community most-recent browse (30/page).
/// Steamworks all-query under-reports for Rift — not used.
/// </summary>
public static class WorkshopRecentQuery
{
    /// <summary>Community workshop browse page size.</summary>
    public const int CommunityPageSize = 30;

    /// <summary>Steam details batch limit.</summary>
    public const int DetailsBatchSize = 50;

    /// <summary>
    /// Rift Workshop host app (same default as <c>SteamWorkshopUgcTrackProvider.GetWorkshopURL</c>).
    /// </summary>
    public const uint WorkshopAppId = 2073250;

    private static readonly HttpClient Http = CreateHttp();

    private static readonly Regex FileIdRegex = new Regex(
        @"sharedfiles/filedetails/\?id=(\d+)|data-publishedfileid=[""'](\d+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Hydrate one community most-recent page (1-based).</summary>
    public static async Task<List<WorkshopRecentItem>> QueryRecentPageAsync(int page)
    {
        var results = new List<WorkshopRecentItem>();
        if (page < 1)
            return results;

        if (!SteamWorkshopUgcTrackProvider.Available)
        {
            Plugin.Logger?.LogWarning("WorkshopRecentQuery: Steam Workshop unavailable");
            return results;
        }

        AppId_t running = SteamUtils.GetAppID();
        Plugin.Logger?.LogInfo(
            $"WorkshopRecentQuery: workshopApp={WorkshopAppId} runningApp={running.m_AppId} browsePage={page} ({CommunityPageSize} max)");

        List<ulong> fileIds = await BrowseRecentFileIdsAsync(page);
        if (fileIds.Count == 0)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: community browse page {page} returned 0 ids");
            return results;
        }

        if (fileIds.Count > CommunityPageSize)
            fileIds = fileIds.GetRange(0, CommunityPageSize);

        results = await HydrateDetailsAsync(fileIds);
        Plugin.Logger?.LogInfo($"WorkshopRecentQuery: page {page} collected {results.Count} item(s)");
        return results;
    }

    public static async Task<string> FetchDescriptionAsync(ulong fileId)
    {
        if (fileId == 0 || !SteamWorkshopUgcTrackProvider.Available)
            return string.Empty;

        var batch = new PublishedFileId_t[] { new PublishedFileId_t(fileId) };
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest(batch, 1u);
        if (handle == UGCQueryHandle_t.Invalid)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: desc query handle invalid ({fileId})");
            return string.Empty;
        }

        try
        {
            SteamUGC.SetAllowCachedResponse(handle, 0u);
            SteamUGC.SetReturnLongDescription(handle, true);
            SteamUGCQueryCompleted_t completed =
                await SteamHelper.Result<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(handle));

            if (completed.m_eResult != EResult.k_EResultOK || completed.m_unNumResultsReturned == 0)
            {
                Plugin.Logger?.LogWarning(
                    $"WorkshopRecentQuery: desc failed ({fileId}): {completed.m_eResult}");
                return string.Empty;
            }

            if (!SteamUGC.GetQueryUGCResult(completed.m_handle, 0u, out SteamUGCDetails_t details))
                return string.Empty;
            if (details.m_eResult != EResult.k_EResultOK)
                return string.Empty;

            return details.m_rgchDescription ?? string.Empty;
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: desc exception ({fileId}): {e.Message}");
            return string.Empty;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static async Task<List<ulong>> BrowseRecentFileIdsAsync(int page)
    {
        var ordered = new List<ulong>();
        var seen = new HashSet<ulong>();

        string url =
            $"https://steamcommunity.com/workshop/browse/?appid={WorkshopAppId}" +
            $"&browsesort=mostrecent&section=readytouseitems&actualsort=mostrecent&p={page}";

        string html;
        try
        {
            html = await Http.GetStringAsync(url);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: browse page {page} failed: {e.Message}");
            return ordered;
        }

        foreach (Match m in FileIdRegex.Matches(html))
        {
            string raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (!ulong.TryParse(raw, out ulong id) || id == 0)
                continue;
            if (!seen.Add(id))
                continue;
            ordered.Add(id);
            if (ordered.Count >= CommunityPageSize)
                break;
        }

        Plugin.Logger?.LogInfo($"WorkshopRecentQuery: browse page {page} added {ordered.Count} ids");
        return ordered;
    }

    private static async Task<List<WorkshopRecentItem>> HydrateDetailsAsync(List<ulong> fileIds)
    {
        var results = new List<WorkshopRecentItem>();
        if (fileIds == null || fileIds.Count == 0)
            return results;

        var byId = new Dictionary<ulong, WorkshopRecentItem>();

        for (int offset = 0; offset < fileIds.Count; offset += DetailsBatchSize)
        {
            int count = Math.Min(DetailsBatchSize, fileIds.Count - offset);
            var batch = new PublishedFileId_t[count];
            for (int i = 0; i < count; i++)
                batch[i] = new PublishedFileId_t(fileIds[offset + i]);

            UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest(batch, (uint)count);
            if (handle == UGCQueryHandle_t.Invalid)
            {
                Plugin.Logger?.LogWarning("WorkshopRecentQuery: details query handle invalid");
                continue;
            }

            try
            {
                SteamUGC.SetAllowCachedResponse(handle, 0u);
                SteamUGCQueryCompleted_t completed =
                    await SteamHelper.Result<SteamUGCQueryCompleted_t>(SteamUGC.SendQueryUGCRequest(handle));

                if (completed.m_eResult != EResult.k_EResultOK)
                {
                    Plugin.Logger?.LogWarning(
                        $"WorkshopRecentQuery: details failed: {completed.m_eResult}");
                    continue;
                }

                for (uint i = 0; i < completed.m_unNumResultsReturned; i++)
                {
                    if (!SteamUGC.GetQueryUGCResult(completed.m_handle, i, out SteamUGCDetails_t details))
                        continue;
                    if (details.m_eResult != EResult.k_EResultOK)
                        continue;

                    ulong fileId = details.m_nPublishedFileId.m_PublishedFileId;
                    if (fileId == 0 || byId.ContainsKey(fileId))
                        continue;

                    string previewUrl = null;
                    if (SteamUGC.GetQueryUGCPreviewURL(completed.m_handle, i, out string url, 2048u)
                        && !string.IsNullOrEmpty(url))
                        previewUrl = url;

                    byId[fileId] = new WorkshopRecentItem
                    {
                        FileId = fileId,
                        Title = details.m_rgchTitle ?? string.Empty,
                        OwnerId = details.m_ulSteamIDOwner,
                        Author = null,
                        PreviewUrl = previewUrl,
                        Description = null,
                    };
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"WorkshopRecentQuery: details exception: {e.Message}");
            }
            finally
            {
                SteamUGC.ReleaseQueryUGCRequest(handle);
            }
        }

        foreach (ulong id in fileIds)
        {
            if (byId.TryGetValue(id, out var item))
                results.Add(item);
        }

        return results;
    }

    public static async Task ResolveAuthorsAsync(IList<WorkshopRecentItem> items)
    {
        if (items == null || items.Count == 0)
            return;

        var cache = new Dictionary<ulong, string>();
        foreach (var item in items)
        {
            if (item == null)
                continue;
            if (!string.IsNullOrEmpty(item.Author))
                continue;

            if (item.OwnerId == 0)
            {
                item.Author = "unknown";
                continue;
            }

            if (cache.TryGetValue(item.OwnerId, out string cached))
            {
                item.Author = cached;
                continue;
            }

            string author = await ResolveAuthorAsync(item.OwnerId);
            cache[item.OwnerId] = author;
            item.Author = author;
        }
    }

    public static List<ulong> GetSubscribedFileIds()
    {
        var ids = new List<ulong>();
        try
        {
            if (!SteamWorkshopUgcTrackProvider.Available)
                return ids;

            uint count = SteamUGC.GetNumSubscribedItems();
            if (count == 0)
                return ids;

            var buf = new PublishedFileId_t[count];
            uint written = SteamUGC.GetSubscribedItems(buf, count);
            for (uint i = 0; i < written; i++)
                ids.Add(buf[i].m_PublishedFileId);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: subscribed enum failed: {e.Message}");
        }

        return ids;
    }

    public static bool TrySubscribe(ulong fileId)
    {
        try
        {
            if (!SteamWorkshopUgcTrackProvider.Available || fileId == 0)
                return false;

            SteamAPICall_t call = SteamUGC.SubscribeItem(new PublishedFileId_t(fileId));
            return call != SteamAPICall_t.Invalid;
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopRecentQuery: SubscribeItem failed ({fileId}): {e.Message}");
            return false;
        }
    }

    private static async Task<string> ResolveAuthorAsync(ulong ownerId)
    {
        try
        {
            string name = await SteamHelper.TryGetUserName(ownerId, 0.75);
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        catch
        {
            // Fall through.
        }

        try
        {
            string persona = SteamFriends.GetFriendPersonaName(new CSteamID(ownerId));
            if (!string.IsNullOrEmpty(persona) &&
                !string.Equals(persona, "[unknown]", StringComparison.OrdinalIgnoreCase))
                return persona;
        }
        catch
        {
            // Fall through.
        }

        return ownerId.ToString();
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) QoLiTea-WorkshopAutoScan");
        return http;
    }
}
