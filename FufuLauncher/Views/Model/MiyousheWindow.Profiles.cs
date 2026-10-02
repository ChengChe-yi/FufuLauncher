using FufuLauncher.Models.Miyoushe;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Views;

public sealed partial class MiyousheWindow
{
    private sealed record ProfileTarget(string Id, string Name, int Game);
    private ProfileTarget? _queuedProfile;

    private async void OnPostAuthorAvatar(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: CommunityPost post })
            await OpenAuthorProfileAsync(post.AuthorId, post.Author, post.GameId);
    }

    private async Task OpenAuthorProfileAsync(string id, string name, int game)
    {
        if (_closed || !CommunityUser.IsValidId(id)) return;
        if (_replyDialog != null)
        {
            _queuedReply = null; _queuedProfile = new(id, name, game);
            _replyDialog.Hide();
            return;
        }
        await NavigateAsync(CommunityFeed.User, id, name.Length > 0 ? name : null, game);
    }
}
