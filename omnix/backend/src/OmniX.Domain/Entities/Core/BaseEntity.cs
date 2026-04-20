namespace OmniX.Domain.Entities.Core;

/// <summary>
/// الكيان الأساسي — يرث منه كل الـ entities في المنصة
/// يشمل: Multi-tenant isolation + Soft delete + Audit fields + Optimistic concurrency
/// </summary>
public abstract class BaseEntity
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     TenantId    { get; set; }
    public bool     IsDeleted   { get; set; }
    public DateTime? DeletedAt  { get; set; }
    public Guid?    DeletedBy   { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
    public Guid?    CreatedBy   { get; set; }
    public Guid?    UpdatedBy   { get; set; }
    public int      Version     { get; set; } = 1;
}
