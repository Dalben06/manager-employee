using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagerEmployee.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RefineEmployeeIdentityAndSynchronizationModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Employees",
            table: "Employees");

        migrationBuilder.DropIndex(
            name: "UX_Employees_Provider_ProviderEmployeeId",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "Id",
            table: "Employees");

        migrationBuilder.RenameColumn(
            name: "HasEverSynchronized",
            table: "Employees",
            newName: "HasBeenCreatedInTrackTik");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Employees",
            table: "Employees",
            columns: new[] { "Provider", "ProviderEmployeeId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Employees",
            table: "Employees");

        migrationBuilder.RenameColumn(
            name: "HasBeenCreatedInTrackTik",
            table: "Employees",
            newName: "HasEverSynchronized");

        migrationBuilder.AddColumn<long>(
            name: "Id",
            table: "Employees",
            type: "bigint",
            nullable: false,
            defaultValue: 0L)
            .Annotation("SqlServer:Identity", "1, 1");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Employees",
            table: "Employees",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "UX_Employees_Provider_ProviderEmployeeId",
            table: "Employees",
            columns: new[] { "Provider", "ProviderEmployeeId" },
            unique: true);
    }
}
