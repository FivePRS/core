using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Config;

namespace FivePRS.Core.Civilian
{
    public static class LicenseViews
    {
        public static List<LicenseView> Build(int characterId, LicensesConfig config, IEnumerable<LicenseInfo>? held) =>
            config.Types.Select(type =>
            {
                var license = held?.FirstOrDefault(l => string.Equals(l.Type, type.Id, StringComparison.OrdinalIgnoreCase));
                return new LicenseView
                {
                    Type        = type.Id,
                    Name        = type.Name,
                    SelfService = type.SelfService,
                    Status      = license?.Status,
                    Number      = CivilianRules.LicenseNumber(type.Id, characterId),
                    Class       = type.Class,
                    IssuedAt    = license?.IssuedAt,
                    ExpiresAt   = license is null || type.ValidDays <= 0 ? null : license.IssuedAt.AddDays(type.ValidDays),
                    Card        = type.Card,
                };
            }).ToList();
    }
}
