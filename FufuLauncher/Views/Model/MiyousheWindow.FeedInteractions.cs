using FufuLauncher.Models.Miyoushe;
using FufuLauncher.Services.Miyoushe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class MiyousheWindow
{
    private readonly Dictionary<string, CommunityPost> _postInteractionUpdates = [];

    private CommunityPost ApplyPostInteractionState(CommunityPost post) =>
        _postInteractionUpdates.TryGetValue(post.Id, out var updated)
            ? post with
            {
                IsLiked = updated.IsLiked, HasLikeState = updated.HasLikeState, LikeCount = updated.LikeCount,
                ReplyCount = updated.ReplyCount
            }
            : post;

    private CommunityPost MergePostFromServer(CommunityPost post)
    {
        if (!_postInteractionUpdates.TryGetValue(post.Id, out var previous)) return post;
        if (!post.HasLikeState) post = post with { IsLiked = previous.IsLiked, HasLikeState = previous.HasLikeState };
        _postInteractionUpdates[post.Id] = post;
        return post;
    }

    private void RememberPostInteraction(CommunityPost post)
    {
        _postInteractionUpdates[post.Id] = post;
        for (int i = 0; i < _posts.Count; i++)
            if (_posts[i].Id == post.Id)
                _posts[i] = ApplyPostInteractionState(_posts[i]);
    }

    private static IEnumerable<MiyoushePostCounters> FeedCounters(DependencyObject root)
    {
        if (root is MiyoushePostCounters counters) yield return counters;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in FeedCounters(VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }

    private async void OnFeedPostLike(object sender, RoutedEventArgs args)
    {
        if (sender is MiyoushePostCounters { Post: { } post }) await SetFeedPostLikeAsync(post);
    }

    private async Task SetFeedPostLikeAsync(CommunityPost post, MiyousheClient? expectedClient = null,
        CancellationToken? token = null, bool? desired = null)
    {
        var client = expectedClient ?? _client;
        var ct = token ?? _session.Token;
        if (_closed || _interactionBusy || ct.IsCancellationRequested || client != _client) return;
        _interactionBusy = true;
        UpdatePostInteractionState();
        try
        {
            client.RequireInteractionAccount();
            post = ApplyPostInteractionState(post);
            if (!desired.HasValue && !post.HasLikeState) post = await client.GetPostAsync(post.Id, post.GameId, ct);
            ct.ThrowIfCancellationRequested();
            desired ??= !post.IsLiked;
            await client.SetPostLikeAsync(post.Id, desired.Value, ct);
            if (_closed || ct.IsCancellationRequested || client != _client) return;
            var updated = post with
            {
                IsLiked = desired.Value, HasLikeState = true,
                LikeCount = Math.Max(0, (post.LikeCount ?? 0) + (desired == post.IsLiked ? 0 : desired.Value ? 1 : -1))
            };
            RememberPostInteraction(updated);
            if (_post?.Id == post.Id) _post = ApplyPostInteractionState(_post);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!_closed && !ct.IsCancellationRequested && client == _client)
                ReportError(ex, () => SetFeedPostLikeAsync(post, client, ct, desired));
        }
        finally
        {
            _interactionBusy = false;
            UpdatePostInteractionState();
        }
    }

    private async void OnFeedPostComment(object sender, RoutedEventArgs args)
    {
        if (sender is MiyoushePostCounters { Post: { } post }) await OpenFeedPostCommentAsync(post);
    }

    private async Task OpenFeedPostCommentAsync(CommunityPost post)
    {
        if (_closed || _interactionBusy || _dialogOpen) return;
        var client = _client;
        try
        {
            client.RequireInteractionAccount();
        }
        catch (Exception ex)
        {
            ReportError(ex, null);
            return;
        }

        if (await OpenPostAsync(post.Id, post.GameId) && client == _client && _isPostOpen && _post?.Id == post.Id)
            await ShowCommentComposerAsync();
    }
}