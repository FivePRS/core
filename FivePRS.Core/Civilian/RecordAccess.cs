using System.Linq;
using FivePRS.Core.Models;

namespace FivePRS.Core.Civilian
{
    public static class RecordAccess
    {
        public static bool CanView(Department department) =>
            department == Department.Police || department == Department.EMS || department == Department.Fire;

        public static bool SeesCriminalRecord(Department department) => department == Department.Police;

        public static bool CanIssue(Department department, RecordType type) => SeesCriminalRecord(department)
            ? type != RecordType.Medical
            : CanView(department) && type == RecordType.Medical;

        public static string? CautionFor(LookupRecord record)
        {
            var warrant = record.Records.FirstOrDefault(r => r.Type == RecordType.Warrant && r.Active);
            return warrant is null ? null : "Active warrant. Request police before approaching.";
        }

        public static LookupResult Filter(LookupResult result, Department department)
        {
            var record = result.Record;
            if (record is null) return result;

            if (SeesCriminalRecord(department))
            {
                record.Records = record.Records.Where(r => r.Type != RecordType.Medical).ToList();
                return result;
            }

            record.Caution  = CautionFor(record);
            record.Licenses = new();
            record.Vehicles = new();
            record.Records  = record.Records.Where(r => r.Type == RecordType.Medical).ToList();
            return result;
        }
    }
}
