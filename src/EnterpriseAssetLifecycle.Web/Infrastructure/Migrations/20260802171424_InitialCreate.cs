using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAssetLifecycle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:asset_event_type", "registered,imported,assigned,returned,department_moved,repair_started,repair_completed,retired,warranty_recorded,warranty_expiring,warranty_claim_opened,warranty_claim_closed,software_installed,software_removed")
                .Annotation("Npgsql:Enum:asset_state", "in_stock,assigned,in_repair,retired")
                .Annotation("Npgsql:Enum:asset_type", "laptop,desktop,monitor,phone,peripheral,server,other")
                .Annotation("Npgsql:Enum:import_status", "processing,completed,completed_with_errors,failed")
                .Annotation("Npgsql:Enum:maintenance_status", "open,completed,cancelled");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ImportedRows = table.Column<int>(type: "integer", nullable: false),
                    SkippedRows = table.Column<int>(type: "integer", nullable: false),
                    FailedRows = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetEvents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRecords_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UninstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareInstallations_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warranties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CoverageNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warranties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warranties_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedReturnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReturnNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetEvents_AssetId_OccurredAt",
                table: "AssetEvents",
                columns: new[] { "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetEvents_DeduplicationKey",
                table: "AssetEvents",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTag",
                table: "Assets",
                column: "AssetTag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DepartmentId_State",
                table: "Assets",
                columns: new[] { "DepartmentId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_SerialNumber",
                table: "Assets",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_State",
                table: "Assets",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_UpdatedAt",
                table: "Assets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssetId",
                table: "Assignments",
                column: "AssetId",
                unique: true,
                filter: "\"ReturnedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssetId_AssignedAt",
                table: "Assignments",
                columns: new[] { "AssetId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_EmployeeId",
                table: "Assignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId_IsActive",
                table: "Employees",
                columns: new[] { "DepartmentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_FileHash",
                table: "ImportBatches",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_IdempotencyKey",
                table: "ImportBatches",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_AssetId_Status",
                table: "MaintenanceRecords",
                columns: new[] { "AssetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstallations_AssetId_Name",
                table: "SoftwareInstallations",
                columns: new[] { "AssetId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Warranties_AssetId",
                table: "Warranties",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warranties_EndDate",
                table: "Warranties",
                column: "EndDate");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION prevent_asset_event_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'AssetEvents is append-only';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER asset_events_append_only
                BEFORE UPDATE OR DELETE ON "AssetEvents"
                FOR EACH ROW EXECUTE FUNCTION prevent_asset_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS asset_events_append_only ON "AssetEvents";
                DROP FUNCTION IF EXISTS prevent_asset_event_mutation();
                """);

            migrationBuilder.DropTable(
                name: "AssetEvents");

            migrationBuilder.DropTable(
                name: "Assignments");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "MaintenanceRecords");

            migrationBuilder.DropTable(
                name: "SoftwareInstallations");

            migrationBuilder.DropTable(
                name: "Warranties");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
