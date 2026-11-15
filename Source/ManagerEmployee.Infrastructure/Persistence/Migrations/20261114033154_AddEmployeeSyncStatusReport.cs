using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagerEmployee.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the read-only <c>dbo.usp_GetEmployeeSyncStatusCounts</c> report procedure. The EF model is unchanged.
/// </summary>
public partial class AddEmployeeSyncStatusReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // CREATE PROCEDURE must be the only statement in its batch, which migrationBuilder.Sql guarantees.
        migrationBuilder.Sql(
            """
            CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeSyncStatusCounts
            AS
            BEGIN
                SET NOCOUNT ON;

                SELECT [SyncStatus], COUNT(*) AS [EmployeeCount]
                FROM dbo.[Employees]
                GROUP BY [SyncStatus];
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetEmployeeSyncStatusCounts;");
    }
}
