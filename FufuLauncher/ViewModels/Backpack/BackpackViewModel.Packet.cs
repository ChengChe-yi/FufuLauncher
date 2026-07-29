using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackViewModel
{
    private readonly SemaphoreSlim _dbWriteLock = new(1, 1);

    public event Action? DataReceived;

    public async void OnPacketReceived(object? _, (string Event, string Json) args)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var parsed = await Task.Run(() => DeserializePacket(args.Event, args.Json));
            if (parsed is null) return;

            Debug.WriteLine($"[Backpack.Perf] Parse {args.Event}: {stopwatch.ElapsedMilliseconds} ms");
            _dispatcher.TryEnqueue(() => ApplyParsed(args.Event, parsed));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backpack] 处理同步数据失败 ({args.Event}): {ex}");
        }
    }

    private static object? DeserializePacket(string evt, string json) => evt switch
    {
        "weapon" => JsonSerializer.Deserialize<WeaponBag>(json),
        "artifact" => JsonSerializer.Deserialize<ArtifactBag>(json),
        "material" => JsonSerializer.Deserialize<MaterialBag>(json),
        "prop" => JsonSerializer.Deserialize<PropBag>(json),
        _ => null
    };

    private void ApplyParsed(string evt, object payload)
    {
        var stopwatch = Stopwatch.StartNew();
        switch (evt, payload)
        {
            case ("weapon", WeaponBag bag):
                Weapons.Clear();
                foreach (var entry in bag.Weapons)
                    Weapons.Add(new WeaponViewModel(entry, _weaponMeta));
                _ = PersistAsync(evt, bag);
                break;

            case ("artifact", ArtifactBag bag):
                _artifactsLoaded = true;
                Artifacts.Clear();
                foreach (var entry in bag.Artifacts)
                    Artifacts.Add(new ArtifactViewModel(entry, _artifactMeta));
                _ = PersistAsync(evt, bag);
                break;

            case ("material", MaterialBag bag):
                foreach (var entry in bag.Materials)
                    _activeCounts[entry.Id] = entry.Count;
                if (_materialGroupsLoaded) RebuildMaterialGroups();
                if (_foodGroupsLoaded) RebuildFoodGroups();
                if (_gadgetGroupsLoaded) RebuildGadgetGroups();
                if (_assetGroupsLoaded) RebuildAssetGroups();
                _ = PersistAsync(evt, new Dictionary<uint, ulong>(_activeCounts));
                break;

            case ("prop", PropBag bag):
                foreach (var (key, value) in bag.Props)
                    _activeProps[key] = value;
                if (_assetGroupsLoaded) RebuildAssetGroups();
                _ = PersistAsync(evt, new Dictionary<uint, long>(_activeProps));
                break;

            default:
                return;
        }

        IsLaunching = false;
        StatusText = $"{BackpackLocalization.Get("StatusReceived")} · {DateTime.Now:HH:mm:ss}";
        RefreshBrowse();
        DataReceived?.Invoke();
        Debug.WriteLine($"[Backpack.Perf] Publish {evt}: {stopwatch.ElapsedMilliseconds} ms");
    }

    private async Task PersistAsync(string evt, object payload)
    {
        await _dbWriteLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                switch (evt, payload)
                {
                    case ("weapon", WeaponBag bag):
                        _db.SaveWeapons(bag.Weapons);
                        break;
                    case ("artifact", ArtifactBag bag):
                        _db.SaveArtifacts(bag.Artifacts);
                        break;
                    case ("material", Dictionary<uint, ulong> counts):
                        _db.SaveMaterials(counts);
                        break;
                    case ("prop", Dictionary<uint, long> props):
                        _db.SaveProps(props);
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backpack] 保存同步数据失败 ({evt}): {ex.Message}");
        }
        finally
        {
            _dbWriteLock.Release();
        }
    }
}
