using EnterpriseAssetLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(AssetDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Departments.AnyAsync(cancellationToken))
        {
            return;
        }

        var operations = new Department
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Code = "OPS",
            Name = "Operations"
        };
        var engineering = new Department
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Code = "ENG",
            Name = "Engineering"
        };
        var support = new Department
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Code = "SUP",
            Name = "Technical Support"
        };
        db.Departments.AddRange(operations, engineering, support);

        db.Employees.AddRange(
            new Employee
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                EmployeeNumber = "EMP-1001",
                FullName = "Alex Morgan",
                Email = "alex.morgan@example.invalid",
                DepartmentId = engineering.Id
            },
            new Employee
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                EmployeeNumber = "EMP-1002",
                FullName = "Taylor Reed",
                Email = "taylor.reed@example.invalid",
                DepartmentId = support.Id
            });

        var laptop = new Asset
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            AssetTag = "AST-1001",
            Type = AssetType.Laptop,
            Manufacturer = "Demo Systems",
            Model = "WorkBook 14",
            SerialNumber = "DEMO-SN-1001",
            DepartmentId = operations.Id,
            PurchaseDate = new DateOnly(2025, 5, 1)
        };
        laptop.Events.Add(new AssetEvent
        {
            AssetId = laptop.Id,
            Type = AssetEventType.Registered,
            Actor = "seed",
            Data = "{\"source\":\"demonstration seed\"}"
        });
        laptop.Warranty = new Warranty
        {
            AssetId = laptop.Id,
            Provider = "Demo Warranty Provider",
            StartDate = new DateOnly(2025, 5, 1),
            EndDate = new DateOnly(2028, 5, 1),
            CoverageNotes = "Development demonstration data only."
        };
        db.Assets.Add(laptop);
        await db.SaveChangesAsync(cancellationToken);
    }
}

