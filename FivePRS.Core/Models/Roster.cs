using System;
using System.Collections.Generic;

namespace FivePRS.Core.Models
{
    public sealed class RosterPlayer
    {
        public string           License     { get; set; } = string.Empty;
        public string           Name        { get; set; } = string.Empty;
        public int              Rank        { get; set; }
        public string           RankName    { get; set; } = string.Empty;
        public string           Callsign    { get; set; } = string.Empty;
        public string           Agency      { get; set; } = string.Empty;
        public bool             OnDuty      { get; set; }
        public bool             Down        { get; set; }
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
        public List<AdminCall>    Calls               { get; set; } = new();
        public List<AdminAudit>   Audit               { get; set; } = new();
        public List<AdminFact>    Server              { get; set; } = new();
        public List<string>       Warnings            { get; set; } = new();
        public List<AdminAddon>   Addons              { get; set; } = new();
    }

    public sealed class AdminAddon
    {
        public string Name        { get; set; } = string.Empty;
        public string Kind        { get; set; } = string.Empty;
        public string Version     { get; set; } = string.Empty;
        public string Author      { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool   Official    { get; set; }
    }

    public sealed class AdminCall
    {
        public string       Id         { get; set; } = string.Empty;
        public string       Name       { get; set; } = string.Empty;
        public int          Code       { get; set; }
        public Department   Department { get; set; }
        public string?      Territory  { get; set; }
        public List<string> Units      { get; set; } = new();
        public bool         Emergency  { get; set; }
    }

    public sealed class AdminAudit
    {
        public DateTime CreatedAt { get; set; }
        public string   Action    { get; set; } = string.Empty;
        public string   ActorName { get; set; } = string.Empty;
        public string   Details   { get; set; } = string.Empty;
    }

    public sealed class AdminFact
    {
        public AdminFact(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }
}
