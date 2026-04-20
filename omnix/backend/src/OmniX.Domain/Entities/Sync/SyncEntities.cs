using OmniX.Domain.Entities.Core;
using OmniX.Domain.Enums;

namespace OmniX.Domain.Entities.Sync;

// ════════════════════════════════════════════════════════════════════════════
//  OmniX Sync Entities — من MesterX Pro (الميزة الفريدة)
//  Offline-First Sync: يسمح للـ POS بالعمل بدون إنترنت ومزامنة لاحقاً
//  محسّن: يرث الآن من BaseEntity بدل كيان مستقل
// ════════════════════════════════════════════════════════════════════════════

public class SyncQueue : BaseEntity
{
    public Guid          BranchId    { get; set; }
    public required string DeviceId  { get; set; }
    public SyncEntityType EntityType { get; set; }
    public Guid          LocalId     { get; set; }
    public Guid?         ServerId    { get; set; }
    public SyncOperation Operation   { get; set; }
    public required string Payload   { get; set; }   // JSON snapshot
    public SyncStatus    Status      { get; set; } = SyncStatus.Pending;
    public string?       ConflictData { get; set; }  // JSON conflict details
    public int           RetryCount  { get; set; }
    public DateTime?     SyncedAt    { get; set; }
    public ConflictStrategy ConflictStrategy { get; set; } = ConflictStrategy.ServerWins;

    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Branch Branch { get; set; } = null!;
}

public class SyncSession : BaseEntity
{
    public Guid          BranchId       { get; set; }
    public required string DeviceId     { get; set; }
    public string?       DeviceName     { get; set; }
    public string?       AppVersion     { get; set; }
    public DateTime?     LastSyncAt     { get; set; }
    public int           RecordsPushed  { get; set; }
    public int           RecordsPulled  { get; set; }
    public int           Conflicts      { get; set; }
    public string        Status         { get; set; } = "Idle";   // Idle | Syncing | Error

    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Branch Branch { get; set; } = null!;
}

public class SyncLog : BaseEntity
{
    public Guid          SessionId     { get; set; }
    public Guid          BranchId      { get; set; }
    public required string DeviceId   { get; set; }
    public SyncEntityType EntityType  { get; set; }
    public Guid          EntityId      { get; set; }
    public SyncOperation Operation    { get; set; }
    public SyncStatus    Result        { get; set; }
    public string?       ErrorMessage  { get; set; }
    public long          DurationMs    { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
