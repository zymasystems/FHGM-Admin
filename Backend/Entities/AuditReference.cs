using System.ComponentModel.DataAnnotations;

namespace FHGM.Backend.Entities;

// Independent of content/account lifetime; references must never be reused.
public sealed class AuditReference
{
    public long Id { get; set; }
    [MaxLength(120)] public string Entity { get; set; } = "";
    [MaxLength(450)] public string Identifier { get; set; } = "";
}
