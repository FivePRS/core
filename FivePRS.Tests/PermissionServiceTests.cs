using System.Collections.Generic;
using FivePRS.Core.Models;
using FivePRS.Server.Permissions;
using Xunit;

namespace FivePRS.Tests
{
    public class PermissionServiceTests
    {
        private readonly HashSet<string> _grants = new();
        private readonly HashSet<string> _roster = new();
        private bool _restrict;

        private PermissionService Create() =>
            new((player, ace) => _grants.Contains($"{player}:{ace}"), () => _restrict,
                (player, department) => _roster.Contains($"{player}:{department}"));

        [Fact]
        public void CanJoinDepartment_Restricted_AllowsRosterMembers()
        {
            _restrict = true;
            _roster.Add("1:EMS");

            var permissions = Create();

            Assert.True(permissions.CanJoinDepartment("1", Department.EMS));
            Assert.False(permissions.CanJoinDepartment("1", Department.Police));
            Assert.False(permissions.CanJoinDepartment("2", Department.EMS));
        }

        [Fact]
        public void CanJoinDepartment_Unrestricted_AllowsEveryone()
        {
            Assert.True(Create().CanJoinDepartment("1", Department.Police));
        }

        [Fact]
        public void CanJoinDepartment_None_IsNeverAllowed()
        {
            Assert.False(Create().CanJoinDepartment("1", Department.None));
        }

        [Fact]
        public void CanJoinDepartment_Restricted_RequiresDepartmentAce()
        {
            _restrict = true;
            _grants.Add("1:fiveprs.department.police");

            var permissions = Create();

            Assert.True(permissions.CanJoinDepartment("1", Department.Police));
            Assert.False(permissions.CanJoinDepartment("1", Department.EMS));
            Assert.False(permissions.CanJoinDepartment("2", Department.Police));
        }

        [Fact]
        public void CanJoinDepartment_Restricted_AdminBypasses()
        {
            _restrict = true;
            _grants.Add($"1:{PermissionService.AdminAce}");

            Assert.True(Create().CanJoinDepartment("1", Department.Fire));
        }

        [Fact]
        public void IsAdmin_RequiresAdminAce()
        {
            _grants.Add($"1:{PermissionService.AdminAce}");

            var permissions = Create();

            Assert.True(permissions.IsAdmin("1"));
            Assert.False(permissions.IsAdmin("2"));
        }
    }
}
