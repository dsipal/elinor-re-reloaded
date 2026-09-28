using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Elinor
{
    public class Profile
    {
        public const string DefaultName = "Default";

        public string profileName { get; set; } = DefaultName;
        public double marginThreshold { get; set; } = .1;
        public double minimumThreshold { get; set; } = .02;
        public int accounting { get; set; } = 5;
        public int brokerRelations { get; set; } = 5;
        public double factionStanding { get; set; }
        public double corpStanding { get; set; }

        public bool useBuyCustomBroker { get; set; }
        public double buyCustomBroker { get; set; } = 0.01;
        public bool useSellCustomBroker { get; set; }
        public double sellCustomBroker { get; set; } = 0.01;

        public int buyRange { get; set; } = (int)Ranges.HUB;
        public int sellRange { get; set; } = (int)Ranges.HUB;

        /// <summary>How much auto copy undercuts / outbids. See <see cref="PriceSteps"/>.</summary>
        public int priceStep { get; set; } = (int)PriceSteps.SMART;
        public double customPriceStep { get; set; } = 1000;

        /// <summary>Stations that count as trade hubs for <see cref="Ranges.HUB"/>.</summary>
        public List<HubStation> hubs { get; set; } = HubStation.Defaults();

        public enum Ranges
        {
            [Description("Hubs (Station)")]
            HUB,
            [Description("System")]
            SYSTEM,
            [Description("1 jump")]
            ONEJUMP,
            [Description("2 jumps")]
            TWOJUMP,
            [Description("Region")]
            REGION,
        }

        public enum PriceSteps
        {
            /// <summary>Change the 4th significant digit, matching EVE's price tick rule.</summary>
            SMART,
            /// <summary>0.01 ISK.</summary>
            MINIMUM,
            /// <summary><see cref="customPriceStep"/> ISK.</summary>
            CUSTOM,
        }

        internal HashSet<long> HubIds() => hubs.Select(h => h.id).ToHashSet();

        public override string ToString()
        {
            return profileName;
        }
    }

    public class HubStation
    {
        public long id { get; set; }
        public string name { get; set; } = "";

        internal static List<HubStation> Defaults() => new List<HubStation>
        {
            new HubStation { id = 60003760, name = "Jita IV - Moon 4 - Caldari Navy Assembly Plant" },
            new HubStation { id = 60008494, name = "Amarr VIII (Oris) - Emperor Family Academy" },
            new HubStation { id = 60011866, name = "Dodixie IX - Moon 20 - Federation Navy Assembly Plant" },
            new HubStation { id = 60004588, name = "Rens VI - Moon 8 - Brutor Tribe Treasury" },
            new HubStation { id = 60005686, name = "Hek VIII - Moon 12 - Boundless Creation Factory" },
        };

        public override string ToString()
        {
            return name.Length != 0 ? name + "  (" + id + ")" : id.ToString();
        }
    }
}
