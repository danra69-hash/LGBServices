using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations
{
    /// <inheritdoc />
    public partial class D1_WorkflowMode_AdminBypass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminBypassAt",
                table: "JobRequestUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdminBypassByUserId",
                table: "JobRequestUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminBypassNote",
                table: "JobRequestUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowMode",
                table: "JobRequestUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminBypassAt",
                table: "JobRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdminBypassByUserId",
                table: "JobRequests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminBypassNote",
                table: "JobRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowMode",
                table: "JobRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminBypassAt",
                table: "JobRequestUnits");

            migrationBuilder.DropColumn(
                name: "AdminBypassByUserId",
                table: "JobRequestUnits");

            migrationBuilder.DropColumn(
                name: "AdminBypassNote",
                table: "JobRequestUnits");

            migrationBuilder.DropColumn(
                name: "WorkflowMode",
                table: "JobRequestUnits");

            migrationBuilder.DropColumn(
                name: "AdminBypassAt",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "AdminBypassByUserId",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "AdminBypassNote",
                table: "JobRequests");

            migrationBuilder.DropColumn(
                name: "WorkflowMode",
                table: "JobRequests");
        }
    }
}
