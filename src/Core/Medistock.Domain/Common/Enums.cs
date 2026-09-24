namespace Medistock.Domain.Common;

public enum DrugSchedule
{
    OTC = 0,
    ScheduleH = 1,
    ScheduleH1 = 2,
    ScheduleX_Narcotic = 3
}

public enum DosageForm
{
    Tablet = 0,
    Capsule = 1,
    Syrup = 2,
    Injection = 3,
    Cream = 4,
    Ointment = 5,
    Gel = 6,
    Drops = 7,
    Inhaler = 8,
    Suspension = 9,
    Powder = 10,
    Spray = 11,
    Patch = 12,
    Other = 99
}

public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    SaleReturn = 3,
    PurchaseReturn = 4,
    StockTransferOut = 5,
    StockTransferIn = 6,
    Adjustment = 7,
    Damage = 8,
    ExpiryWriteOff = 9,
    Opening = 10,
    Correction = 11
}

public enum SaleStatus
{
    Draft = 0,
    Held = 1,
    Posted = 2,
    Cancelled = 3,
    Returned = 4
}

public enum PaymentMode
{
    Cash = 1,
    Card = 2,
    Upi = 3,
    Credit = 4,
    Split = 5
}

public enum OutboxEventStatus
{
    Pending = 0,
    Synced = 1,
    Failed = 2
}

public enum ConnectivityState
{
    FullA = 1,           // Local Workstation -> Local Server -> Cloud Sync
    InternetDownB = 2,   // Local Workstation -> Local Server (Cloud Outbox Queued)
    LocalServerDownC = 3 // Local Workstation -> SQLite (LAN Outbox Queued)
}

public enum ExpiryBand
{
    Good = 0,     // > 90 days to expiry
    Warning = 1,  // 31 - 90 days to expiry
    Critical = 2, // 0 - 30 days to expiry
    Expired = 3   // < 0 days (past expiry)
}

public enum StockAdjustmentType
{
    Add = 1,
    Reduce = 2,
    QuarantineExpired = 3,
    DamageWriteOff = 4
}
