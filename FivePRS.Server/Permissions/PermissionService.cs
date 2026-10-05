using System;
using FivePRS.Core.Models;

namespace FivePRS.Server.Permissions
{
    public sealed class PermissionService
    {
        public const string AdminAce = "fiveprs.admin";

        private readonly Func<string, string, bool> _isAceAllowed;
        private readonly Func<bool>                 _restrictDepartments;

        public PermissionService(Func<string, string, bool> isAceAllowed, Func<bool> restrictDepartments)
        {
            _isAceAllowed        = isAceAllowed;
            _restrictDepartments = restrictDepartments;
        }

        public static string DepartmentAce(Department department) =>
            $"fiveprs.department.{department.ToString().ToLowerInvariant()}";

        public bool IsAdmin(string playerId) => _isAceAllowed(playerId, AdminAce);

        public bool CanJoinDepartment(string playerId, Department department)
        {
            if (department == Department.None) return false;
            if (!_restrictDepartments()) return true;

            return IsAdmin(playerId) || _isAceAllowed(playerId, DepartmentAce(department));
        }
    }
}
