using System;
using System.Collections.Generic;
using TicketToRide.Domain;

namespace TicketToRide.Application.Net
{
    /// <summary>
    /// RouteId → Route 的查表。联机时权威端收到客户端传来的 RouteId 后，
    /// 必须用它还原成场景里那个真实的 Route 对象（绝不能凭 Id 新建对象，
    /// 否则认领时靠引用相等的校验会静默失败）。
    /// 建表过程中同时检测重复 Id（RouteVisual.RouteId 的 Inspector 默认值是 "route"，是潜在坑）。
    /// </summary>
    public class RouteRegistry
    {
        private readonly Dictionary<string, Route> _byId = new Dictionary<string, Route>();
        private readonly List<string> _duplicateIds = new List<string>();

        public int Count => _byId.Count;

        /// <summary>建表时发现的重复 Id（正常情况下应为空）。</summary>
        public IReadOnlyList<string> DuplicateIds => _duplicateIds;

        public IReadOnlyCollection<Route> Routes => _byId.Values;

        /// <summary>登记一条路线。返回 false 表示该 Id 已存在（并记入 DuplicateIds）。</summary>
        public bool Register(Route route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (_byId.ContainsKey(route.Id))
            {
                if (!_duplicateIds.Contains(route.Id))
                {
                    _duplicateIds.Add(route.Id);
                }

                return false;
            }

            _byId.Add(route.Id, route);
            return true;
        }

        public bool TryGet(string routeId, out Route route)
        {
            if (string.IsNullOrWhiteSpace(routeId))
            {
                route = null;
                return false;
            }

            return _byId.TryGetValue(routeId, out route);
        }
    }
}