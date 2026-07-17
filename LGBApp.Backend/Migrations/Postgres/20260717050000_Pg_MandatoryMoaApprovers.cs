using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260717050000_Pg_MandatoryMoaApprovers")]
public partial class Pg_MandatoryMoaApprovers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MandatoryMoaApproversJson",
            table: "DivisionGroups",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "[]");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MandatoryMoaApproversJson",
            table: "DivisionGroups");
    }
}
