using Elinor;

namespace Elinor.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "elinor-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Reads_profile_written_by_elinor_1_12_binaryformatter()
    {
        // Fixture produced by the original .NET Framework Profile/Serializer code.
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Trader Alt.dat");

        using FileStream stream = File.OpenRead(path);
        Profile p = LegacyProfileImporter.Read(stream);

        Assert.Equal("Trader Alt", p.profileName);
        Assert.Equal(0.15, p.marginThreshold);
        Assert.Equal(0.03, p.minimumThreshold);
        Assert.Equal(4, p.accounting);
        Assert.Equal(3, p.brokerRelations);
        Assert.Equal(2.5, p.factionStanding);
        Assert.Equal(-1.25, p.corpStanding);
        Assert.True(p.useBuyCustomBroker);
        Assert.Equal(0.012, p.buyCustomBroker);
        Assert.False(p.useSellCustomBroker);
        Assert.Equal(0.02, p.sellCustomBroker);
        Assert.Equal(1, p.buyRange);
        Assert.Equal(4, p.sellRange);
    }

    [Fact]
    public void Imports_legacy_folder_once_and_skips_bad_files()
    {
        string legacy = Path.Combine(_dir, "legacy");
        Directory.CreateDirectory(legacy);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Trader Alt.dat"), Path.Combine(legacy, "Trader Alt.dat"));
        File.WriteAllText(Path.Combine(legacy, "Broken.dat"), "not a binaryformatter payload");

        var store = new ProfileStore(Path.Combine(_dir, "profiles"));

        Assert.Equal(1, LegacyProfileImporter.ImportAll(new[] { legacy, Path.Combine(_dir, "missing") }, store));
        Assert.Equal(0, LegacyProfileImporter.ImportAll(new[] { legacy }, store)); // already present

        Profile imported = Assert.Single(store.LoadAll());
        Assert.Equal("Trader Alt", imported.profileName);
        Assert.Equal(0.15, imported.marginThreshold);
    }

    [Fact]
    public void Profile_store_round_trips_and_ignores_default()
    {
        var store = new ProfileStore(_dir);

        Assert.True(store.Save(new Profile()));             // Default: no-op
        Assert.False(store.Exists(Profile.DefaultName));

        var p = new Profile { profileName = "Hauler", marginThreshold = 0.2, sellRange = (int)Profile.Ranges.REGION };
        Assert.True(store.Save(p));
        Assert.False(File.Exists(store.PathFor("Hauler") + ".tmp"));

        Profile loaded = Assert.Single(store.LoadAll());
        Assert.Equal("Hauler", loaded.profileName);
        Assert.Equal(0.2, loaded.marginThreshold);
        Assert.Equal((int)Profile.Ranges.REGION, loaded.sellRange);

        store.Delete("Hauler");
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void Imports_settings_from_legacy_user_config()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <userSettings>
                    <Elinor.Properties.Settings>
                        <setting name="logpath" serializeAs="String"><value>D:\EVE\Marketlogs</value></setting>
                        <setting name="autocopy" serializeAs="String"><value>-1</value></setting>
                        <setting name="pin" serializeAs="String"><value>True</value></setting>
                        <setting name="selectedprofile" serializeAs="String"><value>Trader Alt</value></setting>
                        <setting name="showtutorial" serializeAs="String"><value>False</value></setting>
                    </Elinor.Properties.Settings>
                </userSettings>
            </configuration>
            """;

        AppSettings s = AppSettings.FromLegacyUserConfig(xml);

        Assert.Equal(@"D:\EVE\Marketlogs", s.LogPath);
        Assert.Equal(-1, s.AutoCopy);
        Assert.True(s.Pin);
        Assert.Equal("Trader Alt", s.SelectedProfile);
        Assert.False(s.CheckForUpdates);
    }

    [Fact]
    public void Profile_saved_before_new_options_gets_their_defaults()
    {
        // Shape of a profile JSON written before price step / hubs existed.
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "Old.json"), """{ "profileName": "Old", "marginThreshold": 0.2, "sellRange": 1 }""");

        Profile p = Assert.Single(new ProfileStore(_dir).LoadAll());

        Assert.Equal(0.2, p.marginThreshold);
        Assert.Equal((int)Profile.PriceSteps.SMART, p.priceStep);
        Assert.Equal(5, p.hubs.Count);
        Assert.Contains(60003760, p.HubIds());
    }

    [Fact]
    public void Custom_hubs_and_price_step_round_trip()
    {
        var store = new ProfileStore(_dir);
        var p = new Profile { profileName = "Citadel", priceStep = (int)Profile.PriceSteps.CUSTOM, customPriceStep = 250 };
        p.hubs = new List<HubStation> { new HubStation { id = 1042508032148, name = "Perimeter keepstar" } };
        store.Save(p);

        Profile loaded = Assert.Single(store.LoadAll());
        Assert.Equal((int)Profile.PriceSteps.CUSTOM, loaded.priceStep);
        Assert.Equal(250, loaded.customPriceStep);
        HubStation hub = Assert.Single(loaded.hubs);
        Assert.Equal(1042508032148, hub.id);
        Assert.Equal("Perimeter keepstar", hub.name);
    }

    [Fact]
    public void Corrupt_profile_is_skipped_not_fatal()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "Bad.json"), "{ not json");
        var store = new ProfileStore(_dir);
        store.Save(new Profile { profileName = "Good" });

        Profile only = Assert.Single(store.LoadAll());
        Assert.Equal("Good", only.profileName);
    }
}

public sealed class MarketLogReadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "elinor-read-" + Guid.NewGuid().ToString("N"));

    public MarketLogReadTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Missing_file_returns_null_instead_of_hanging()
    {
        string? text = await MarketLogWatcher.TryReadAllTextAsync(
            Path.Combine(_dir, "gone.txt"), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task Waits_for_writer_to_finish_then_reads()
    {
        string path = Path.Combine(_dir, "export.txt");
        var writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        writer.Write("partial"u8);
        writer.Flush();

        Task<string?> read = MarketLogWatcher.TryReadAllTextAsync(path, TimeSpan.FromSeconds(5), CancellationToken.None);
        await Task.Delay(200);
        Assert.False(read.IsCompleted); // still being written

        writer.Write(" done"u8);
        writer.Dispose();

        Assert.Equal("partial done", await read);
    }

    [Fact]
    public async Task Gives_up_after_timeout_when_file_stays_locked()
    {
        string path = Path.Combine(_dir, "locked.txt");
        using var writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

        await Assert.ThrowsAnyAsync<IOException>(() =>
            MarketLogWatcher.TryReadAllTextAsync(path, TimeSpan.FromMilliseconds(300), CancellationToken.None));
    }

    [Fact]
    public async Task Coexists_with_other_readers()
    {
        // Antivirus / OneDrive often hold a read handle; the old exclusive-open check spun forever on this.
        string path = Path.Combine(_dir, "scanned.txt");
        File.WriteAllText(path, "ok");
        using var scanner = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        Assert.Equal("ok", await MarketLogWatcher.TryReadAllTextAsync(path, TimeSpan.FromSeconds(1), CancellationToken.None));
    }
}
