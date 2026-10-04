using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class MercadoPagoSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PastDueSince",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingPlanId",
                table: "Subscription",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NotificationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DataId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_PendingPlanId",
                table: "Subscription",
                column: "PendingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvent_Provider_NotificationId",
                table: "PaymentWebhookEvent",
                columns: new[] { "Provider", "NotificationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscription_Plan_PendingPlanId",
                table: "Subscription",
                column: "PendingPlanId",
                principalTable: "Plan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscription_Plan_PendingPlanId",
                table: "Subscription");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvent");

            migrationBuilder.DropIndex(
                name: "IX_Subscription_PendingPlanId",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "PastDueSince",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "PendingPlanId",
                table: "Subscription");
        }
    }
}
