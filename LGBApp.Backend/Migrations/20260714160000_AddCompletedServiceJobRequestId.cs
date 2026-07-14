using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations;

/// <inheritdoc />
public partial class AddCompletedServiceJobRequestId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "JobRequestId",
            table: "CompletedServices",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CompletedServices_JobRequestId",
            table: "CompletedServices",
            column: "JobRequestId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CompletedServices_JobRequestId",
            table: "CompletedServices");

        migrationBuilder.DropColumn(
            name: "JobRequestId",
            table: "CompletedServices");
    }
}
