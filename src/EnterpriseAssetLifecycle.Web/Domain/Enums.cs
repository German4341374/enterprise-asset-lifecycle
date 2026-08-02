namespace EnterpriseAssetLifecycle.Domain;

public enum AssetState
{
    InStock,
    Assigned,
    InRepair,
    Retired
}

public enum AssetType
{
    Laptop,
    Desktop,
    Monitor,
    Phone,
    Peripheral,
    Server,
    Other
}

public enum MaintenanceStatus
{
    Open,
    Completed,
    Cancelled
}

public enum AssetEventType
{
    Registered,
    Imported,
    Assigned,
    Returned,
    DepartmentMoved,
    RepairStarted,
    RepairCompleted,
    Retired,
    WarrantyRecorded,
    WarrantyExpiring,
    WarrantyClaimOpened,
    WarrantyClaimClosed,
    SoftwareInstalled,
    SoftwareRemoved
}

public enum ImportStatus
{
    Processing,
    Completed,
    CompletedWithErrors,
    Failed
}

