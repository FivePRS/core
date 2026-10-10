using System;
using System.Collections.Generic;

namespace FivePRS.Core.Models
{
    public sealed class RosterPlayer
    {
        public string           License     { get; set; } = string.Empty;
        public string           Name        { get; set; } = string.Empty;
        public int              Rank        { get; set; }
        public Department       Department  { get; set; }
        public bool             Online      { get; set; }
        public int?             ServerId    { get; set; }
        public DateTime         LastSeen    { get; set; }
        public List<Department> Departments { get; set; } = new();
    }

    public sealed class AdminState
    {
        public bool               RestrictDepartments { get; set; }
        public string?            Query               { get; set; }
        public List<RosterPlayer> Online              { get; set; } = new();
        public List<RosterPlayer> Results             { get; set; } = new();
    }
}
