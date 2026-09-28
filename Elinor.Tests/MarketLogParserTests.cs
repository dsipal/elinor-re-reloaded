using Elinor;

namespace Elinor.Tests;

public class MarketLogParserTests
{
    private static readonly ISet<long> DefaultHubs = new Profile().HubIds();

    private const string Header =
        "price,volRemaining,typeID,range,orderID,volEntered,minVolume,bid,issueDate,duration,stationID,regionID,solarSystemID,jumps,";

    private static string Row(double price, bool bid, long station, int jumps) =>
        FormattableString.Invariant(
            $"{price:0.0},1.0,34,32767,1,1,1,{bid},2026-09-28 10:00:00.000,90,{station},10000002,30000142,{jumps},");

    // One order per range tier so every range picks a different best price.
    private static readonly string RangedCsv = string.Join("\r\n",
        Header,
        Row(100, false, 60003760, 0),      // Jita hub
        Row(90, false, 1042508032148, 0),  // citadel, same system
        Row(80, false, 60004081, 1),       // 1 jump
        Row(70, false, 60004082, 2),       // 2 jumps
        Row(60, false, 60004083, 10),      // region
        Row(50, true, 60003760, 0),
        Row(55, true, 1042508032148, 0),
        Row(56, true, 60004081, 1),
        Row(57, true, 60004082, 2),
        Row(58, true, 60004083, 10),
        "");

    [Theory]
    [InlineData(Profile.Ranges.HUB, 100, 50)]
    [InlineData(Profile.Ranges.SYSTEM, 90, 55)]
    [InlineData(Profile.Ranges.ONEJUMP, 80, 56)]
    [InlineData(Profile.Ranges.TWOJUMP, 70, 57)]
    [InlineData(Profile.Ranges.REGION, 60, 58)]
    public void Picks_best_price_within_range(Profile.Ranges range, double sell, double buy)
    {
        MarketSnapshot snapshot = MarketLogParser.Parse("The Forge-Tritanium-2026.09.28 100000.txt", RangedCsv);

        Assert.Equal(sell, snapshot.BestSell(range, DefaultHubs));
        Assert.Equal(buy, snapshot.BestBuy(range, DefaultHubs));
    }

    [Fact]
    public void Custom_hubs_change_what_counts_as_hub()
    {
        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", RangedCsv);
        var citadelOnly = new HashSet<long> { 1042508032148 };

        Assert.Equal(90, snapshot.BestSell(Profile.Ranges.HUB, citadelOnly));
        Assert.Equal(55, snapshot.BestBuy(Profile.Ranges.HUB, citadelOnly));
        Assert.Equal(-1, snapshot.BestSell(Profile.Ranges.HUB, new HashSet<long>()));
    }

    [Fact]
    public void Lists_stations_by_order_count()
    {
        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", RangedCsv);

        var stations = snapshot.Stations().ToList();
        Assert.Equal(5, stations.Count);
        Assert.All(stations, s => Assert.Equal(2, s.Orders));
    }

    [Fact]
    public void Returns_minus_one_when_no_orders_in_range()
    {
        string csv = Header + "\r\n" + Row(80, false, 60004081, 1);
        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", csv);

        Assert.Equal(-1, snapshot.BestSell(Profile.Ranges.HUB, DefaultHubs));
        Assert.Equal(-1, snapshot.BestBuy(Profile.Ranges.REGION, DefaultHubs));
    }

    [Fact]
    public void Skips_malformed_rows_instead_of_throwing()
    {
        string csv = string.Join("\n",
            Header,
            "",
            "garbage",
            "abc,1.0,34,32767,1,1,1,False,x,90,60003760,1,1,0,",
            "12.5,1.0,34,32767,1,1,1,maybe,x,90,60003760,1,1,0,",
            "10,1.0,34",
            Row(42, false, 60003760, 0));

        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", csv);

        Assert.Single(snapshot.Orders);
        Assert.Equal(42, snapshot.BestSell(Profile.Ranges.HUB, DefaultHubs));
        Assert.Equal(34, snapshot.TypeId);
    }

    [Fact]
    public void Empty_file_gives_empty_snapshot()
    {
        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", "");

        Assert.Empty(snapshot.Orders);
        Assert.Equal(-1, snapshot.TypeId);
    }

    [Fact]
    public void Uses_header_to_find_columns()
    {
        // Same data with columns reordered.
        string csv = "jumps,bid,price,stationID,typeID\n0,False,123.45,60003760,587\n0,True,100.0,60003760,587";
        MarketSnapshot snapshot = MarketLogParser.Parse("x-y-z.txt", csv);

        Assert.Equal(123.45, snapshot.BestSell(Profile.Ranges.HUB, DefaultHubs));
        Assert.Equal(100.0, snapshot.BestBuy(Profile.Ranges.HUB, DefaultHubs));
        Assert.Equal(587, snapshot.TypeId);
    }

    [Theory]
    [InlineData(@"C:\Users\x\Documents\EVE\logs\Marketlogs\The Forge-Tritanium-2026.09.28 100000.txt", "Tritanium")]
    [InlineData(@"C:\Users\Jean-Luc\my-docs\The Forge-Centum C-Type Explosive Energized Membrane-2026.09.28 161855.txt",
        "Centum C-Type Explosive Energized Membrane")]
    [InlineData(@"C:\logs\weird.txt", "")]
    public void Extracts_item_name_from_file_name_only(string path, string expected)
    {
        Assert.Equal(expected, MarketLogParser.ItemNameFromFileName(path));
    }

    [Fact]
    public void Parses_real_eve_export()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "The Forge-Centum C-Type Explosive Energized Membrane-2026.09.28 161855.txt");

        MarketSnapshot snapshot = MarketLogParser.Parse(path, File.ReadAllText(path));

        Assert.Equal("Centum C-Type Explosive Energized Membrane", snapshot.ItemName);
        Assert.Equal(18839, snapshot.TypeId);
        Assert.Equal(61, snapshot.Orders.Count);
        Assert.Equal(10_750_000, snapshot.BestSell(Profile.Ranges.HUB, DefaultHubs));
        Assert.Equal(8_111_000, snapshot.BestBuy(Profile.Ranges.HUB, DefaultHubs));
    }
}
