using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FufuLauncher.Models.Miyoushe;

public sealed record CommunityGame(int Id, string Name, string Slug)
{
    public override string ToString() => Name;
}

public sealed record CommunityForum(int Id, int GameId, string Name)
{
    public override string ToString() => Name;
}

public sealed record CommunityLink(string Id, string Name);
public sealed record CommunityPage<T>(IReadOnlyList<T> Items, string Cursor, bool IsLast);
public enum CommunityFeed { Forum, News, Search, Following, Favorites, User, Topic, Collection, Bookmarks, History }
public sealed record FeedRequest(CommunityFeed Kind, int GameId = 2, string Target = "", int Sort = 3, int ForumId = 26);

public static class CommunityUser
{
    public static bool IsValidId(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit) && id.Any(c => c != '0');

    internal static string AvatarUrl(JsonElement user)
    {
        foreach (string candidate in new[] { user.Text("avatar_url"), user.Text("avatar"), user.Get("avatar").Text("url") })
        {
            string value = candidate.Trim();
            if (value.StartsWith("//", StringComparison.Ordinal)) value = "https:" + value;
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0)
                return uri.AbsoluteUri;
        }
        return "";
    }
}

public sealed record CommunityPost
{
    public string Id { get; init; } = "";
    public int GameId { get; init; } = 2;
    public string Title { get; init; } = "";
    public string AuthorId { get; init; } = "";
    public string Author { get; init; } = "";
    public string Avatar { get; init; } = "";
    [JsonIgnore] public bool CanOpenAuthor => CommunityUser.IsValidId(AuthorId);
    public string Cover { get; init; } = "";
    [JsonIgnore] public bool HasCover => Cover.Length > 0;
    public string Summary { get; init; } = "";
    public string Meta { get; init; } = "";
    public long CreatedAt { get; init; }
    public long? ViewCount { get; init; }
    public long? LikeCount { get; init; }
    public long? ReplyCount { get; init; }
    [JsonIgnore] public bool IsLiked { get; init; }
    [JsonIgnore] public bool HasLikeState { get; init; }
    [JsonIgnore] public string Published => FormatDate(CreatedAt);
    [JsonIgnore] public string Counters => ViewCount.HasValue
        ? $"◉ {ViewCount}   ♡ {LikeCount}   ☏ {ReplyCount}"
        : Regex.Match(Meta, "◉.*").Value;
    public bool IsOfficial { get; init; }
    public IReadOnlyList<CommunityLink> Topics { get; init; } = [];
    public CommunityLink? Collection { get; init; }
    [JsonIgnore] public JsonElement Raw { get; init; }

    public override string ToString() => Title;

    private static string FormatDate(long created)
    {
        try { return DateTimeOffset.FromUnixTimeSeconds(created).LocalDateTime.ToString("MM-dd HH:mm"); }
        catch (ArgumentOutOfRangeException) { return ""; }
    }

    public static CommunityPost Parse(JsonElement full, int fallbackGame = 2)
    {
        var post = full.Get("post");
        var user = full.Get("user");
        var stat = full.Get("stat");
        var title = post.Text("subject");
        var summary = post.Text("summary");
        if (summary.Length == 0) summary = full.Text("text_summary");
        var cover = full.Get("cover").Text("url");
        if (cover.Length == 0) cover = post.Text("cover");
        if (cover.Length == 0) cover = full.Get("image_list").Items().FirstOrDefault().Text("url");
        var game = (int)post.Number("game_id");
        var collection = full.Get("collection");
        var created = post.Number("created_at");
        string date = FormatDate(created);
        var author = user.Text("nickname");
        return new CommunityPost
        {
            Id = post.Text("post_id"), GameId = game > 0 ? game : fallbackGame,
            Title = WebUtility.HtmlDecode(title.Length > 0 ? title : summary),
            AuthorId = CommunityUser.IsValidId(user.Text("uid")) ? user.Text("uid") : post.Text("uid"),
            Author = author, Avatar = CommunityUser.AvatarUrl(user), Cover = cover,
            Summary = WebUtility.HtmlDecode(summary), CreatedAt = created,
            ViewCount = stat.Number("view_num"), LikeCount = stat.Number("like_num"), ReplyCount = stat.Number("reply_num"),
            IsLiked = full.Get("self_operation").Number("attitude") == 1,
            HasLikeState = full.Get("self_operation").Get("attitude").ValueKind is JsonValueKind.Number or JsonValueKind.String,
            IsOfficial = full.Flag("is_official_master") || post.Get("post_status").Flag("is_official"),
            Meta = $"{author}   {date}   ◉ {stat.Number("view_num")}   ♡ {stat.Number("like_num")}   ☏ {stat.Number("reply_num")}".Trim(),
            Topics = full.Get("topics").Items().Select(t => new CommunityLink(t.Text("id"), t.Text("name"))).ToArray(),
            Collection = collection.Text("id") is { Length: > 0 } id ? new(id, collection.Text("title")) : null,
            Raw = full.Clone()
        };
    }
}

public sealed record CommunityReply(string Id, string FloorId, string AuthorId, string Author, string Meta,
    string Content, IReadOnlyList<string> Images, IReadOnlyList<CommunityReply> Children, int ChildCount,
    long LikeCount = 0, bool IsLiked = false)
{
    public string Avatar { get; init; } = "";

    public static CommunityReply Parse(JsonElement full)
    {
        var r = full.Get("reply");
        var u = full.Get("user");
        string content = WebUtility.HtmlDecode(r.Text("content"));
        var target = full.Get("r_user").Text("nickname");
        if (target.Length > 0) content = $"@{target}  {content}";
        var date = r.Number("created_at");
        string dateText = "";
        try { dateText = DateTimeOffset.FromUnixTimeSeconds(date).LocalDateTime.ToString("MM-dd HH:mm"); }
        catch (ArgumentOutOfRangeException) { }
        return new(r.Text("reply_id"), r.Text("floor_id"), CommunityUser.IsValidId(u.Text("uid")) ? u.Text("uid") : r.Text("uid"), u.Text("nickname"),
            $"{dateText}{(full.Flag("is_lz") ? "  · 楼主" : "")}",
            content, full.Get("images").Items().Select(i => i.Text("url")).Where(i => i.Length > 0).ToArray(),
            full.Get("sub_replies").Items().Select(Parse).ToArray(), (int)full.Number("sub_reply_count"),
            full.Get("stat").Number("like_num"), full.Get("self_operation").Number("attitude") == 1) { Avatar = CommunityUser.AvatarUrl(u) };
    }
}

internal static class CommunityJson
{
    public static JsonElement Get(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    public static string Text(this JsonElement element, string name) => element.Get(name).ValueKind switch
    {
        JsonValueKind.String => element.Get(name).GetString() ?? "",
        JsonValueKind.Number => element.Get(name).GetRawText(),
        _ => ""
    };
    public static long Number(this JsonElement element, string name) => long.TryParse(element.Text(name), out var n) ? n : 0;
    public static bool Flag(this JsonElement element, string name) => element.Get(name).ValueKind == JsonValueKind.True;
    public static IEnumerable<JsonElement> Items(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : Enumerable.Empty<JsonElement>();
}
