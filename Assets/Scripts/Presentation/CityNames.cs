using System.Collections.Generic;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 美版车票之旅 36 城的英文名 → 中文名对照。
    /// key 必须与 CityMarker.CityId / 领域层城市字符串完全一致。
    /// </summary>
    public static class CityNames
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["Vancouver"] = "温哥华",
            ["Seattle"] = "西雅图",
            ["Portland"] = "波特兰",
            ["San Francisco"] = "旧金山",
            ["Los Angeles"] = "洛杉矶",
            ["Las Vegas"] = "拉斯维加斯",
            ["Phoenix"] = "菲尼克斯",
            ["El Paso"] = "埃尔帕索",
            ["Houston"] = "休斯顿",
            ["Dallas"] = "达拉斯",
            ["Oklahoma City"] = "俄克拉荷马城",
            ["Kansas City"] = "堪萨斯城",
            ["Denver"] = "丹佛",
            ["Santa Fe"] = "圣达菲",
            ["Salt Lake City"] = "盐湖城",
            ["Helena"] = "海伦娜",
            ["Calgary"] = "卡尔加里",
            ["Winnipeg"] = "温尼伯",
            ["Duluth"] = "德卢斯",
            ["Omaha"] = "奥马哈",
            ["Chicago"] = "芝加哥",
            ["Sault St. Marie"] = "苏圣玛丽",
            ["Toronto"] = "多伦多",
            ["Montreal"] = "蒙特利尔",
            ["Boston"] = "波士顿",
            ["New York"] = "纽约",
            ["Washington"] = "华盛顿",
            ["Pittsburgh"] = "匹兹堡",
            ["Saint Louis"] = "圣路易斯",
            ["Nashville"] = "纳什维尔",
            ["Raleigh"] = "罗利",
            ["Atlanta"] = "亚特兰大",
            ["Charleston"] = "查尔斯顿",
            ["Miami"] = "迈阿密",
            ["New Orleans"] = "新奥尔良",
            ["Little Rock"] = "小石城",
        };

        public static bool TryGet(string cityId, out string chineseName)
        {
            if (!string.IsNullOrWhiteSpace(cityId))
            {
                return Map.TryGetValue(cityId.Trim(), out chineseName);
            }

            chineseName = null;
            return false;
        }
    }
}
