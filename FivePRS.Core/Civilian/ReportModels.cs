using System;
using System.Collections.Generic;
using FivePRS.Core.Models;

namespace FivePRS.Core.Civilian
{
    public enum ReportKind
    {
        Patient  = 0,
        Incident = 1,
    }

    public enum ReportStatus
    {
        Draft = 0,
        Filed = 1,
    }

    public sealed class ReportDraft
    {
        public ReportKind Kind { get; set; }
        public string CallId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Narrative { get; set; } = string.Empty;
        public int? PatientSeed { get; set; }
        public bool PatientFemale { get; set; }
        public string? Plate { get; set; }
    }

    public sealed class ReportInfo
    {
        public int          Id             { get; set; }
        public ReportKind   Kind           { get; set; }
        public Department   Department     { get; set; }
        public string       CallId         { get; set; } = string.Empty;
        public ReportStatus Status         { get; set; }
        public string       Title          { get; set; } = string.Empty;
        public string       Location       { get; set; } = string.Empty;
        public string       Narrative      { get; set; } = string.Empty;
        public int?         CharacterId    { get; set; }
        public string?      PersonName     { get; set; }
        public string?      Plate          { get; set; }
        public string       Crew           { get; set; } = string.Empty;
        public string       AuthorName     { get; set; } = string.Empty;
        public string       AuthorCallsign { get; set; } = string.Empty;
        public DateTime     CreatedAt      { get; set; }
        public DateTime?    FiledAt        { get; set; }
        public string?      FiledBy        { get; set; }
    }

    public sealed class ReportsView
    {
        public List<ReportInfo>  Drafts  { get; set; } = new();
        public List<ReportInfo>? Results { get; set; }
        public string?           Query   { get; set; }
        public string?           Date    { get; set; }
    }
}
