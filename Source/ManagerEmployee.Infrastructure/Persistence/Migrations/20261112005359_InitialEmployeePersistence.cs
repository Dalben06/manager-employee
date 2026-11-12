using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagerEmployee.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialEmployeePersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Employees",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ProviderEmployeeId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Position = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                EmploymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                SyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Attempts = table.Column<int>(type: "int", nullable: false),
                LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                HasEverSynchronized = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Employees", x => x.Id);
                table.CheckConstraint("CK_Employees_Attempts", "[Attempts] >= 1");
                table.CheckConstraint("CK_Employees_EmploymentStatus", "[EmploymentStatus] IN ('active', 'inactive', 'terminated')");
                table.CheckConstraint("CK_Employees_Provider", "[Provider] IN ('provider1', 'provider2')");
                table.CheckConstraint("CK_Employees_SyncStatus", "[SyncStatus] IN ('pending', 'synced', 'failed')");
            });

        migrationBuilder.CreateIndex(
            name: "UX_Employees_Provider_ProviderEmployeeId",
            table: "Employees",
            columns: new[] { "Provider", "ProviderEmployeeId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Employees");
    }
}
