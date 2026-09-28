using Elinor;

namespace Elinor.Tests;

public class TradeCalculatorTests
{
    [Fact]
    public void Npc_broker_fee_matches_eve_formula()
    {
        var profile = new Profile { brokerRelations = 5, factionStanding = 0, corpStanding = 0 };
        Assert.Equal(0.015, TradeCalculator.NpcBroker(profile), 10);

        profile = new Profile { brokerRelations = 0, factionStanding = 10, corpStanding = 10 };
        Assert.Equal(0.025, TradeCalculator.NpcBroker(profile), 10);
    }

    [Theory]
    [InlineData(0, 0.075)]
    [InlineData(5, 0.03375)]
    public void Sales_tax_matches_eve_formula(int accounting, double expected)
    {
        Assert.Equal(expected, TradeCalculator.SalesTax(accounting), 10);
    }

    [Fact]
    public void Custom_broker_fee_overrides_npc_fee()
    {
        var profile = new Profile { useBuyCustomBroker = true, buyCustomBroker = 0.005, useSellCustomBroker = false };

        Assert.Equal(0.005, TradeCalculator.BuyBrokerFee(profile));
        Assert.Equal(TradeCalculator.NpcBroker(profile), TradeCalculator.SellBrokerFee(profile));
    }

    [Fact]
    public void Returns_null_when_a_price_is_missing()
    {
        Assert.Null(TradeCalculator.Calculate(-1, 100, new Profile()));
        Assert.Null(TradeCalculator.Calculate(100, -1, new Profile()));
    }

    [Fact]
    public void Computes_revenue_cost_and_margin()
    {
        var profile = new Profile
        {
            useBuyCustomBroker = true, buyCustomBroker = 0.01,
            useSellCustomBroker = true, sellCustomBroker = 0.01,
            accounting = 5,
        };

        TradeResult r = TradeCalculator.Calculate(110.01, 99.99, profile)!.Value;

        double tax = TradeCalculator.SalesTax(5);
        Assert.Equal(110 - 110 * 0.01 - 110 * tax, r.Revenue, 6);
        Assert.Equal(100 * 1.01, r.CostOfSales, 6);
        Assert.Equal(r.Revenue - r.CostOfSales, r.Profit, 6);
        Assert.Equal(100 * r.Profit / r.Revenue, r.Margin, 6);
        Assert.Equal(100 * r.Profit / r.CostOfSales, r.Markup, 6);
    }

    [Fact]
    public void Zero_prices_do_not_throw()
    {
        TradeResult? r = TradeCalculator.Calculate(0, 0, new Profile());
        Assert.NotNull(r);
    }
}

public class ClipboardPriceTests
{
    [Theory]
    [InlineData(10_750_000, 10_740_000)]
    [InlineData(1234.56, 1233.56)]
    [InlineData(5.00, 4.99)]
    public void Sell_price_undercuts_by_one_significant_step(double sell, double expected)
    {
        Assert.Equal(expected, ClipboardTools.GetSellPrice(sell, new Profile()), 6);
    }

    [Theory]
    [InlineData(8_111_000, 8_112_000)]
    [InlineData(5.00, 5.01)]
    public void Buy_price_outbids_by_one_significant_step(double buy, double expected)
    {
        Assert.Equal(expected, ClipboardTools.GetBuyPrice(buy, new Profile()), 6);
    }

    [Fact]
    public void Minimum_step_is_one_cent()
    {
        var profile = new Profile { priceStep = (int)Profile.PriceSteps.MINIMUM };

        Assert.Equal(10_749_999.99, ClipboardTools.GetSellPrice(10_750_000, profile), 6);
        Assert.Equal(8_111_000.01, ClipboardTools.GetBuyPrice(8_111_000, profile), 6);
    }

    [Theory]
    [InlineData(500, 10_749_500, 8_111_500)]
    [InlineData(0, 10_749_999.99, 8_111_000.01)] // never below 0.01 ISK
    public void Custom_step_uses_configured_amount(double step, double sell, double buy)
    {
        var profile = new Profile { priceStep = (int)Profile.PriceSteps.CUSTOM, customPriceStep = step };

        Assert.Equal(sell, ClipboardTools.GetSellPrice(10_750_000, profile), 6);
        Assert.Equal(buy, ClipboardTools.GetBuyPrice(8_111_000, profile), 6);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Missing_price_gives_zero_and_empty_clipboard_text(double price)
    {
        Assert.Equal(0, ClipboardTools.GetSellPrice(price, new Profile()));
        Assert.Equal(0, ClipboardTools.GetBuyPrice(price, new Profile()));
        Assert.Equal("", ClipboardTools.FormatPrice(ClipboardTools.GetSellPrice(price, new Profile())));
    }

    [Fact]
    public void Price_text_is_culture_invariant()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("1234.57", ClipboardTools.FormatPrice(1234.567));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
