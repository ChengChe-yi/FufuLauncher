/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using FufuLauncher.Models.GameServer;

namespace FufuLauncher.Views;

public sealed class RemainingChunksTracker
{
    public ObservableCollection<string> Chunks { get; } = [];

    public void Reset() => Chunks.Clear();

    public void Update(GameServerConversionProgress progress)
    {
        if (progress.PendingChunks is not null)
        {
            Chunks.Clear();
            foreach (string name in progress.PendingChunks)
            {
                Chunks.Add(name);
            }
        }
        else if (progress.ChunkName is not null)
        {
            Chunks.Remove(progress.ChunkName);
        }
    }
}
