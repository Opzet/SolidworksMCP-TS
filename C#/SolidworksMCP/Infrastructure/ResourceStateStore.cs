namespace SolidworksMCP;

using System.Text.Json;

public sealed record StateSnapshot
{
    public string Version { get; init; } = "1.0.0";

    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("O");

    public List<ResourceState> Resources { get; init; } = new();

    public StateSnapshotMetadata Metadata { get; init; } = new();
}

public sealed record StateSnapshotMetadata
{
    public int TotalResources { get; init; }

    public Dictionary<string, int> ByType { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> ByStatus { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ResourceStateStore : IAsyncDisposable
{
    private readonly Dictionary<string, ResourceState> resources = new(StringComparer.OrdinalIgnoreCase);
    private readonly string stateFilePath;
    private readonly bool autoSave;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? autoSaveCts;
    private Task? autoSaveTask;

    public ResourceStateStore(string? stateFilePath = null, bool autoSave = true)
    {
        this.stateFilePath = string.IsNullOrWhiteSpace(stateFilePath)
            ? Path.Combine(Environment.CurrentDirectory, ".solidworks-mcp-state.json")
            : stateFilePath;
        this.autoSave = autoSave;

        if (autoSave)
        {
            StartAutoSave();
        }
    }

    public async Task SetStateAsync(string resourceId, ResourceState state, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentNullException.ThrowIfNull(state);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            resources[resourceId] = state;
        }
        finally
        {
            gate.Release();
        }

        AppLogger.Debug($"State updated for resource: {resourceId}", new { state.Type, state.Status });

        if (autoSave)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ResourceState? GetState(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        lock (resources)
        {
            return resources.TryGetValue(resourceId, out var state) ? state : null;
        }
    }

    public IReadOnlyList<ResourceState> GetAllStates()
    {
        lock (resources)
        {
            return resources.Values.ToList();
        }
    }

    public IReadOnlyList<ResourceState> GetStatesByType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return GetAllStates().Where(state => string.Equals(state.Type, type, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<ResourceState> GetStatesByStatus(ResourceStatus status)
    {
        return GetAllStates().Where(state => state.Status == status).ToList();
    }

    public async Task<bool> RemoveStateAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

        bool removed;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            removed = resources.Remove(resourceId);
        }
        finally
        {
            gate.Release();
        }

        if (removed)
        {
            AppLogger.Debug($"State removed for resource: {resourceId}");
            if (autoSave)
            {
                await SaveAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return removed;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            resources.Clear();
        }
        finally
        {
            gate.Release();
        }

        AppLogger.Info("All resource states cleared");

        if (autoSave)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = CreateSnapshot();
        var json = JsonSerializer.Serialize(snapshot, JsonHelpers.SerializerOptions);

        var directory = Path.GetDirectoryName(stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(stateFilePath, json, cancellationToken).ConfigureAwait(false);
        AppLogger.Debug($"State saved to {stateFilePath}");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(stateFilePath))
        {
            AppLogger.Debug("No state file found, starting with empty state");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(stateFilePath, cancellationToken).ConfigureAwait(false);
            var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json, JsonHelpers.SerializerOptions);
            if (snapshot is null)
            {
                return;
            }

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                resources.Clear();
                foreach (var state in snapshot.Resources)
                {
                    resources[state.Id] = state;
                }
            }
            finally
            {
                gate.Release();
            }

            AppLogger.Info($"State loaded from {stateFilePath}", new { totalResources = resources.Count });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppLogger.Warn($"Failed to load state from {stateFilePath}, starting with empty state", ex.Message);
        }
    }

    public StateSnapshot CreateSnapshot()
    {
        Dictionary<string, int> byType;
        Dictionary<string, int> byStatus;
        List<ResourceState> snapshotResources;

        lock (resources)
        {
            byType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            byStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            snapshotResources = resources.Values.ToList();
        }

        foreach (var state in snapshotResources)
        {
            byType[state.Type] = byType.TryGetValue(state.Type, out var typeCount) ? typeCount + 1 : 1;
            var status = state.Status.ToString();
            byStatus[status] = byStatus.TryGetValue(status, out var statusCount) ? statusCount + 1 : 1;
        }

        return new StateSnapshot
        {
            Resources = snapshotResources,
            Metadata = new StateSnapshotMetadata
            {
                TotalResources = snapshotResources.Count,
                ByType = byType,
                ByStatus = byStatus,
            },
        };
    }

    public Dictionary<string, object?> GetStatistics()
    {
        var snapshot = CreateSnapshot();
        return new Dictionary<string, object?>
        {
            ["totalResources"] = snapshot.Metadata.TotalResources,
            ["byType"] = snapshot.Metadata.ByType,
            ["byStatus"] = snapshot.Metadata.ByStatus,
            ["oldestResource"] = GetOldestResource(),
            ["newestResource"] = GetNewestResource(),
        };
    }

    public void StopAutoSave()
    {
        autoSaveCts?.Cancel();
        autoSaveTask = null;
        autoSaveCts?.Dispose();
        autoSaveCts = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopAutoSave();
        gate.Dispose();
        await Task.CompletedTask;
    }

    private void StartAutoSave()
    {
        autoSaveCts = new CancellationTokenSource();
        autoSaveTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(autoSaveCts.Token).ConfigureAwait(false))
            {
                if (GetAllStates().Count > 0)
                {
                    try
                    {
                        await SaveAsync(autoSaveCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("Failed to auto-save state", ex.Message);
                    }
                }
            }
        }, autoSaveCts.Token);
    }

    private ResourceState? GetOldestResource()
    {
        lock (resources)
        {
            return resources.Values.OrderBy(state => state.Metadata.CreatedAt, StringComparer.Ordinal).FirstOrDefault();
        }
    }

    private ResourceState? GetNewestResource()
    {
        lock (resources)
        {
            return resources.Values.OrderByDescending(state => state.Metadata.CreatedAt, StringComparer.Ordinal).FirstOrDefault();
        }
    }
}
