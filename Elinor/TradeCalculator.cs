namespace Elinor
{
    internal readonly record struct TradeResult(
        double Revenue,
        double CostOfSales,
        double BuyOrderCost,
        double SellOrderCost,
        double Margin,
        double Markup)
    {
        internal double Profit => Revenue - CostOfSales;
    }

    internal static class TradeCalculator
    {
        // Current EVE values (last updated March 2025).
        internal const double BaseBrokerFee = 3.0;           // percent
        internal const double BrokerRelationsReduction = 0.3; // percent per level
        internal const double FactionStandingReduction = 0.03;
        internal const double CorpStandingReduction = 0.02;
        internal const double BaseSalesTax = .075;
        internal const double AccountingReduction = .11;      // fraction of base per level

        internal static double BuyBrokerFee(Profile profile)
        {
            return profile.useBuyCustomBroker ? profile.buyCustomBroker : NpcBroker(profile);
        }

        internal static double SellBrokerFee(Profile profile)
        {
            return profile.useSellCustomBroker ? profile.sellCustomBroker : NpcBroker(profile);
        }

        internal static double NpcBroker(Profile profile)
        {
            return (BaseBrokerFee - (profile.brokerRelations * BrokerRelationsReduction
                                     + profile.factionStanding * FactionStandingReduction
                                     + profile.corpStanding * CorpStandingReduction)) / 100;
        }

        internal static double SalesTax(int accounting)
        {
            return BaseSalesTax * (1 - (accounting * AccountingReduction));
        }

        /// <summary>Returns null when either price is missing (negative).</summary>
        internal static TradeResult? Calculate(double sellPrice, double buyPrice, Profile profile)
        {
            if (sellPrice < 0 || buyPrice < 0) return null;

            double buyBrokerFee = BuyBrokerFee(profile);
            double sellBrokerFee = SellBrokerFee(profile);
            double salesTax = SalesTax(profile.accounting);

            double sell = sellPrice - .01;
            double buy = buyPrice + .01;

            double revenue = sell - sell * sellBrokerFee - sell * salesTax;
            double cos = buy + buy * buyBrokerFee;

            return new TradeResult(
                Revenue: revenue,
                CostOfSales: cos,
                BuyOrderCost: buyPrice * buyBrokerFee,
                SellOrderCost: sellPrice * sellBrokerFee + sellPrice * salesTax,
                Margin: 100 * (revenue - cos) / revenue,
                Markup: 100 * (revenue - cos) / cos);
        }
    }
}
