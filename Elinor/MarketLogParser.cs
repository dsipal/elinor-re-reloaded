using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Elinor
{
    internal sealed record MarketOrder(double Price, bool IsBid, long StationId, int Jumps);

    internal sealed record MarketSnapshot(string FilePath, string ItemName, int TypeId, IReadOnlyList<MarketOrder> Orders)
    {
        /// <summary>Lowest sell order in range, or -1 if there is none.</summary>
        internal double BestSell(Profile.Ranges range, ISet<long> hubs) => MarketLogParser.Best(Orders, isBid: false, range, hubs);

        /// <summary>Highest buy order in range, or -1 if there is none.</summary>
        internal double BestBuy(Profile.Ranges range, ISet<long> hubs) => MarketLogParser.Best(Orders, isBid: true, range, hubs);

        /// <summary>Stations in this export with their order count, most orders first.</summary>
        internal IEnumerable<(long StationId, int Orders)> Stations() => Orders
            .GroupBy(o => o.StationId)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(s => s.Item2);
    }

    /// <summary>
    /// Parses EVE market export files ("Region-Item Name-yyyy.MM.dd HHmmss.txt").
    /// Malformed rows are skipped instead of throwing.
    /// </summary>
    internal static class MarketLogParser
    {
        // Column positions in the export format, used if the header row is missing.
        private static readonly Dictionary<string, int> DefaultColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["price"] = 0,
            ["typeID"] = 2,
            ["bid"] = 7,
            ["stationID"] = 10,
            ["jumps"] = 13,
        };

        internal static MarketSnapshot Parse(string filePath, string csv)
        {
            var orders = new List<MarketOrder>();
            int typeId = -1;

            using var reader = new StringReader(csv);
            string? line = reader.ReadLine();
            Dictionary<string, int> columns = ResolveColumns(line);
            if (columns == DefaultColumns && line != null) ParseRow(line); // first line was data, not a header

            while ((line = reader.ReadLine()) != null) ParseRow(line);

            return new MarketSnapshot(filePath, ItemNameFromFileName(filePath), typeId, orders);

            void ParseRow(string row)
            {
                if (string.IsNullOrWhiteSpace(row)) return;
                string[] cells = row.Split(',');

                if (!TryGet(cells, columns["price"], out string price) ||
                    !TryGet(cells, columns["bid"], out string bid) ||
                    !TryGet(cells, columns["stationID"], out string station) ||
                    !TryGet(cells, columns["jumps"], out string jumps))
                    return;

                if (!double.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ||
                    !bool.TryParse(bid, out bool isBid) ||
                    !long.TryParse(station, NumberStyles.Integer, CultureInfo.InvariantCulture, out long stationId) ||
                    !int.TryParse(jumps, NumberStyles.Integer, CultureInfo.InvariantCulture, out int j))
                    return;

                if (typeId < 0 && TryGet(cells, columns["typeID"], out string type) &&
                    int.TryParse(type, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t))
                    typeId = t;

                orders.Add(new MarketOrder(p, isBid, stationId, j));
            }
        }

        private static Dictionary<string, int> ResolveColumns(string? header)
        {
            if (header == null) return DefaultColumns;

            string[] names = header.Split(',').Select(n => n.Trim()).ToArray();
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string key in DefaultColumns.Keys)
            {
                int index = Array.FindIndex(names, n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return DefaultColumns;
                columns[key] = index;
            }

            return columns;
        }

        private static bool TryGet(string[] cells, int index, out string value)
        {
            value = index < cells.Length ? cells[index].Trim() : "";
            return value.Length > 0;
        }

        internal static double Best(IEnumerable<MarketOrder> orders, bool isBid, Profile.Ranges range, ISet<long> hubs)
        {
            IEnumerable<double> prices = orders
                .Where(o => o.IsBid == isBid && InRange(o, range, hubs))
                .Select(o => o.Price);

            if (!prices.Any()) return -1.0;
            return isBid ? prices.Max() : prices.Min();
        }

        private static bool InRange(MarketOrder order, Profile.Ranges range, ISet<long> hubs)
        {
            switch (range)
            {
                case Profile.Ranges.HUB: return order.Jumps == 0 && hubs.Contains(order.StationId);
                case Profile.Ranges.SYSTEM: return order.Jumps == 0;
                case Profile.Ranges.ONEJUMP: return order.Jumps < 2;
                case Profile.Ranges.TWOJUMP: return order.Jumps < 3;
                default: return true;
            }
        }

        /// <summary>
        /// "The Forge-Centum C-Type Membrane-2026.09.28 161855.txt" -> "Centum C-Type Membrane".
        /// Only the file name is used, so dashes elsewhere in the path don't matter.
        /// </summary>
        internal static string ItemNameFromFileName(string filePath)
        {
            string[] parts = Path.GetFileNameWithoutExtension(filePath).Split('-');
            return parts.Length <= 2 ? "" : string.Join("-", parts.Skip(1).Take(parts.Length - 2));
        }
    }
}
