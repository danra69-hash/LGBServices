using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260717090000_Pg_ReminderFoundation")]
public partial class Pg_ReminderFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ClientApprovalRequestedAt",
            table: "MOIForms",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ActivatedAt",
            table: "WorkflowStepInstances",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ReminderLogs",
            columns: table => new
            {
                ReminderLogId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                TargetEntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                TargetEntityId = table.Column<int>(type: "integer", nullable: false),
                SentCount = table.Column<int>(type: "integer", nullable: false),
                LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClaimedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReminderLogs", x => x.ReminderLogId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReminderLogs_Kind_TargetEntityType_TargetEntityId",
            table: "ReminderLogs",
            columns: new[] { "Kind", "TargetEntityType", "TargetEntityId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReminderLogs");
        migrationBuilder.DropColumn(name: "ClientApprovalRequestedAt", table: "MOIForms");
        migrationBuilder.DropColumn(name: "ActivatedAt", table: "WorkflowStepInstances");
    }
}
