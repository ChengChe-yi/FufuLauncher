using System.Text;
using FufuLauncher.Models.Miyoushe;

namespace FufuLauncher.Services.Miyoushe;

public static partial class CommunityContent
{
    public static string RenderComments(IEnumerable<CommunityReply> replies, CommunityCommentOptions options)
    {
        var html = new StringBuilder("<div class=\"comments-header\"><div class=\"comments-heading\"><h2>").Append(Encode(options.Title)).Append("</h2>");
        Action(html, options, "only-author", options.OnlyAuthor, options.OnlyAuthorEnabled);
        html.Append("</div><nav aria-label=\"").Append(Encode(options.Title)).Append("\">");
        foreach (var (order, label) in new[] { (0, options.Hot), (2, options.Latest), (1, options.Oldest) })
            Action(html, options, "sort/" + order, label, options.Order == order);
        html.Append("</nav></div>");
        bool any = false;
        foreach (var reply in replies) { any = true; RenderReply(html, reply, options, false); }
        if (options.IsLoading) html.Append("<p class=\"muted\" role=\"status\">").Append(Encode(options.Loading)).Append("</p>");
        else if (!any) html.Append("<p class=\"muted\">").Append(Encode(options.Empty)).Append("</p>");
        if (options.HasMore && !options.IsLoading) Action(html, options, "more", options.More, false);
        return html.ToString();
    }

    private static void Action(StringBuilder html, CommunityCommentOptions options, string path, string label, bool selected)
    {
        if (label.Length == 0 || !TryWebUri(options.ActionRoot + path, out var uri)) return;
        html.Append("<a class=\"comment-pill").Append(selected ? " selected" : "").Append("\" href=\"").Append(Encode(uri.AbsoluteUri))
            .Append("\" aria-pressed=\"").Append(selected ? "true" : "false").Append("\">").Append(Encode(label)).Append("</a>");
    }

    private static void LikeAction(StringBuilder html, CommunityCommentOptions options, CommunityReply reply)
    {
        if (!TryWebUri(options.ActionRoot + "like/" + reply.Id, out var uri)) return;
        string label = (reply.IsLiked ? options.Liked : options.Like) + " " + reply.LikeCount;
        html.Append("<a class=\"comment-pill comment-like").Append(reply.IsLiked ? " selected" : "").Append("\" href=\"").Append(Encode(uri.AbsoluteUri))
            .Append("\" aria-pressed=\"").Append(reply.IsLiked ? "true" : "false").Append("\" aria-label=\"").Append(Encode(label)).Append("\" title=\"")
            .Append(Encode(label)).Append("\"><svg aria-hidden=\"true\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"")
            .Append(CommunityIconPaths.Like).Append("\"/></svg><span>").Append(reply.LikeCount).Append("</span></a>");
    }

    private static void RenderReply(StringBuilder html, CommunityReply reply, CommunityCommentOptions options, bool child)
    {
        html.Append("<div class=\"comment").Append(child ? " child-comment" : "").Append("\"><div class=\"comment-author\">");
        bool canOpenProfile = CommunityUser.IsValidId(reply.AuthorId);
        if (canOpenProfile) html.Append("<a class=\"comment-user\" href=\"https://www.miyoushe.com/ys/accountCenter/postList?id=").Append(Encode(reply.AuthorId)).Append("\">");
        else html.Append("<span class=\"comment-user\">");
        html.Append("<span class=\"comment-avatar\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"")
            .Append(CommunityIconPaths.Person).Append("\"/></svg>");
        if (TryWebUri(reply.Avatar, out var avatar))
            html.Append("<img loading=\"lazy\" alt=\"\" src=\"").Append(Encode(avatar.AbsoluteUri)).Append("\">");
        html.Append("</span><span class=\"comment-name\">").Append(Encode(reply.Author)).Append("</span>").Append(canOpenProfile ? "</a>" : "</span>");
        html.Append("<span class=\"muted\">").Append(Encode(reply.Meta)).Append("</span></div><p>")
            .Append(Linkify(reply.Content).Replace("\r\n", "\n").Replace("\n", "<br>")).Append("</p>");
        foreach (var image in reply.Images.Take(9))
            if (TryWebUri(image, out var uri))
                html.Append("<a href=\"").Append(Encode(uri.AbsoluteUri)).Append("\"><img class=\"comment-image\" loading=\"lazy\" src=\"")
                    .Append(Encode(uri.AbsoluteUri)).Append("\"></a>");
        if (reply.Id.Length > 0 && reply.Id.All(char.IsAsciiDigit))
        {
            html.Append("<div class=\"comment-actions\">");
            Action(html, options, "reply/" + reply.Id, options.Reply, false);
            LikeAction(html, options, reply);
            html.Append("</div>");
        }
        if (!child)
        {
            foreach (var sub in reply.Children.Take(2)) RenderReply(html, sub, options, true);
            if (reply.ChildCount > 0 && reply.FloorId.Length > 0)
                Action(html, options, "floor/" + Uri.EscapeDataString(reply.FloorId), string.Format(options.SubReplies, reply.ChildCount), false);
        }
        html.Append("</div>");
    }
}
