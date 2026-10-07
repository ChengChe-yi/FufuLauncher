namespace FufuLauncher.Models.Miyoushe;

public sealed record CommunityCommentOptions
{
    public string ActionRoot
    {
        get;
        init;
    } = "";

    public string Title
    {
        get;
        init;
    } = "";

    public string Hot
    {
        get;
        init;
    } = "";

    public string Latest
    {
        get;
        init;
    } = "";

    public string Oldest
    {
        get;
        init;
    } = "";

    public string OnlyAuthor
    {
        get;
        init;
    } = "";

    public string Empty
    {
        get;
        init;
    } = "";

    public string More
    {
        get;
        init;
    } = "";

    public string SubReplies
    {
        get;
        init;
    } = "";

    public string Loading
    {
        get;
        init;
    } = "";

    public string Like
    {
        get;
        init;
    } = "";

    public string Liked
    {
        get;
        init;
    } = "";

    public string Reply
    {
        get;
        init;
    } = "";

    public int Order
    {
        get;
        init;
    }

    public bool OnlyAuthorEnabled
    {
        get;
        init;
    }

    public bool IsLoading
    {
        get;
        init;
    }

    public bool HasMore
    {
        get;
        init;
    }
}