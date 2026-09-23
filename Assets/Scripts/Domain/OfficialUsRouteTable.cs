using System.Collections.Generic;

namespace TicketToRide.Domain
{
    /// <summary>
    /// 美版《Ticket to Ride》官方棋盘路线表（北美版图）。
    /// 数据来源：Days of Wonder 官方棋盘结构数据（78 个城市对，共 100 条可认领轨道；
    /// 其中 22 个城市对是平行双线，4-5 人局两侧均可认领）。
    ///
    /// 城市名与场景 CityMarker.cityId 完全一致；粉色对应枚举 TrainCarColor.Pink。
    /// 灰色(Grey)路线：可用任意一种同色车厢卡认领，不属于 8 种固定颜色。
    /// 美国版图没有隧道、渡轮和强制火车头路线。
    /// </summary>
    public static class OfficialUsRouteTable
    {
        public const int CityPairs = 78;
        public const int ClaimableTracks = 100;

        public static readonly IReadOnlyList<OfficialRouteDef> All = new[]
        {
            // —— 西北海岸 ——
            DoubleGrey("Vancouver", "Seattle", 1),
            Grey("Vancouver", "Calgary", 3),
            Grey("Calgary", "Seattle", 4),
            Grey("Calgary", "Helena", 4),
            Single("Calgary", "Winnipeg", 6, TrainCarColor.White),
            DoubleGrey("Seattle", "Portland", 1),
            Single("Seattle", "Helena", 6, TrainCarColor.Yellow),
            Double("Portland", "San Francisco", 5, TrainCarColor.Pink, TrainCarColor.Green),
            Single("Portland", "Salt Lake City", 6, TrainCarColor.Blue),

            // —— 加州 ——
            Double("San Francisco", "Los Angeles", 3, TrainCarColor.Pink, TrainCarColor.Yellow),
            Double("San Francisco", "Salt Lake City", 5, TrainCarColor.White, TrainCarColor.Orange),
            Grey("Los Angeles", "Las Vegas", 2),
            Grey("Los Angeles", "Phoenix", 3),
            Single("Los Angeles", "El Paso", 6, TrainCarColor.Black),
            Single("Las Vegas", "Salt Lake City", 3, TrainCarColor.Orange),

            // —— 西南山区 ——
            Grey("Phoenix", "El Paso", 3),
            Grey("Phoenix", "Santa Fe", 3),
            Single("Phoenix", "Denver", 5, TrainCarColor.White),
            Single("Salt Lake City", "Helena", 3, TrainCarColor.Pink),
            Double("Salt Lake City", "Denver", 3, TrainCarColor.Red, TrainCarColor.Yellow),
            Single("Helena", "Denver", 4, TrainCarColor.Green),
            Single("Helena", "Winnipeg", 4, TrainCarColor.Blue),
            Single("Helena", "Omaha", 5, TrainCarColor.Red),
            Single("Helena", "Duluth", 6, TrainCarColor.Orange),

            // —— 落基山 / 中南部 ——
            Grey("Denver", "Santa Fe", 2),
            Single("Denver", "Oklahoma City", 4, TrainCarColor.Red),
            Double("Denver", "Kansas City", 4, TrainCarColor.Black, TrainCarColor.Orange),
            Single("Denver", "Omaha", 4, TrainCarColor.Pink),
            Grey("Santa Fe", "El Paso", 2),
            Single("Santa Fe", "Oklahoma City", 3, TrainCarColor.Blue),
            Single("El Paso", "Houston", 6, TrainCarColor.Green),
            Single("El Paso", "Dallas", 4, TrainCarColor.Red),
            Single("El Paso", "Oklahoma City", 5, TrainCarColor.Yellow),

            // —— 中北部 / 五大湖 ——
            Single("Winnipeg", "Duluth", 4, TrainCarColor.Black),
            Grey("Winnipeg", "Sault St. Marie", 6),
            Grey("Duluth", "Sault St. Marie", 3),
            DoubleGrey("Duluth", "Omaha", 2),
            Single("Duluth", "Chicago", 3, TrainCarColor.Red),
            Single("Duluth", "Toronto", 6, TrainCarColor.Pink),
            Grey("Sault St. Marie", "Toronto", 2),
            Single("Sault St. Marie", "Montreal", 5, TrainCarColor.Black),
            Single("Omaha", "Chicago", 4, TrainCarColor.Blue),
            DoubleGrey("Omaha", "Kansas City", 2),
            Single("Chicago", "Toronto", 4, TrainCarColor.White),
            Double("Chicago", "Saint Louis", 2, TrainCarColor.Green, TrainCarColor.White),
            Double("Chicago", "Pittsburgh", 3, TrainCarColor.Orange, TrainCarColor.Black),

            // —— 中部 ——
            DoubleGrey("Kansas City", "Oklahoma City", 2),
            Double("Kansas City", "Saint Louis", 2, TrainCarColor.Blue, TrainCarColor.Pink),
            DoubleGrey("Oklahoma City", "Dallas", 2),
            Grey("Oklahoma City", "Little Rock", 2),
            DoubleGrey("Dallas", "Houston", 1),
            Grey("Dallas", "Little Rock", 2),
            Grey("Houston", "New Orleans", 2),
            Single("New Orleans", "Little Rock", 3, TrainCarColor.Green),
            Double("New Orleans", "Atlanta", 4, TrainCarColor.Yellow, TrainCarColor.Orange),
            Single("New Orleans", "Miami", 6, TrainCarColor.Red),

            // —— 东北 ——
            Grey("Toronto", "Pittsburgh", 2),
            Grey("Toronto", "Montreal", 3),
            DoubleGrey("Montreal", "Boston", 2),
            Single("Montreal", "New York", 3, TrainCarColor.Blue),
            Double("Boston", "New York", 2, TrainCarColor.Yellow, TrainCarColor.Red),
            Double("New York", "Pittsburgh", 2, TrainCarColor.Green, TrainCarColor.White),
            Double("New York", "Washington", 2, TrainCarColor.Black, TrainCarColor.Orange),

            // —— 东南 ——
            Grey("Pittsburgh", "Washington", 2),
            Grey("Pittsburgh", "Raleigh", 2),
            Single("Pittsburgh", "Nashville", 4, TrainCarColor.Yellow),
            Single("Pittsburgh", "Saint Louis", 5, TrainCarColor.Green),
            DoubleGrey("Washington", "Raleigh", 2),
            Grey("Raleigh", "Charleston", 2),
            Single("Raleigh", "Nashville", 3, TrainCarColor.Black),
            Grey("Atlanta", "Nashville", 1),
            Grey("Atlanta", "Charleston", 2),
            DoubleGrey("Atlanta", "Raleigh", 2),
            Single("Atlanta", "Miami", 5, TrainCarColor.Blue),
            Single("Charleston", "Miami", 4, TrainCarColor.Pink),
            Grey("Nashville", "Saint Louis", 2),
            Single("Nashville", "Little Rock", 3, TrainCarColor.White),
            Grey("Saint Louis", "Little Rock", 2),
        };

        private static OfficialRouteDef Single(string a, string b, int length, TrainCarColor color)
            => new OfficialRouteDef(a, b, length, false, color, false, TrainCarColor.Red);

        /// <summary>灰色单线：任意同色车厢卡均可认领。</summary>
        private static OfficialRouteDef Grey(string a, string b, int length)
            => new OfficialRouteDef(a, b, length, true, TrainCarColor.Red, false, TrainCarColor.Red);

        /// <summary>彩色平行双线：两轨颜色不同（4-5 人局两边都能被认领）。</summary>
        private static OfficialRouteDef Double(string a, string b, int length, TrainCarColor color1, TrainCarColor color2)
            => new OfficialRouteDef(a, b, length, false, color1, true, color2);

        /// <summary>灰色平行双线：两轨都可用任意同色卡认领。</summary>
        private static OfficialRouteDef DoubleGrey(string a, string b, int length)
            => new OfficialRouteDef(a, b, length, true, TrainCarColor.Red, true, TrainCarColor.Red);
    }

    /// <summary>
    /// 一条官方路线定义（城市对）。双线时表示一对平行轨道：
    /// IsDouble=false 只有一条（Color，IsGrey 时为灰色）；
    /// IsDouble=true 有两条（IsGrey 时两条都是灰色，否则分别为 Color 与 Color2）。
    /// </summary>
    public readonly struct OfficialRouteDef
    {
        public readonly string City1;
        public readonly string City2;
        public readonly int Length;
        public readonly bool IsGrey;
        public readonly TrainCarColor Color;
        public readonly bool IsDouble;
        public readonly TrainCarColor Color2;

        public OfficialRouteDef(
            string city1,
            string city2,
            int length,
            bool isGrey,
            TrainCarColor color,
            bool isDouble,
            TrainCarColor color2)
        {
            City1 = city1;
            City2 = city2;
            Length = length;
            IsGrey = isGrey;
            Color = color;
            IsDouble = isDouble;
            Color2 = color2;
        }

        /// <summary>该城市对包含几条可认领轨道（1 或 2）。</summary>
        public int TrackCount => IsDouble ? 2 : 1;

        /// <summary>取得第 index 条轨道（0 或 1）的颜色；灰色轨道返回值无意义，应看 IsGrey。</summary>
        public TrainCarColor GetTrackColor(int index) => index == 0 ? Color : Color2;
    }
}
