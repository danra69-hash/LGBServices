using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260716172000_Pg_ClientActivatedAt")]
public partial class Pg_ClientActivatedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ClientActivatedAt",
            table: "JobRequestUnits",
            type: "timestamp with time zone",
            nullable: true);

        // Backfill: in-flight / completed sessions stay visible after dormancy ships.
        migrationBuilder.Sql("""
            UPDATE "JobRequestUnits" u
            SET "ClientActivatedAt" = NOW() AT TIME ZONE 'utc'
            WHERE u."ClientActivatedAt" IS NULL
              AND (
                u."Status" <> 'Pending'
                OR COALESCE(u."InternalHandoffStatus", '') <> ''
                OR COALESCE(u."WorkflowMode", '') <> ''
                OR u."AdminBypassAt" IS NOT NULL
                OR EXISTS (
                    SELECT 1 FROM "MOIForms" m
                    WHERE m."JobRequestUnitId" = u."JobRequestUnitId")
                OR EXISTS (
                    SELECT 1 FROM "MOAForms" m
                    WHERE m."JobRequestUnitId" = u."JobRequestUnitId")
              );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientActivatedAt",
            table: "JobRequestUnits");
    }
}
