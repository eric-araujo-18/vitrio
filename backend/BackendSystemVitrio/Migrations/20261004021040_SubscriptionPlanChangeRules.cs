using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPlanChangeRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingGatewaySubscriptionId",
                table: "Subscription",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledPlanId",
                table: "Subscription",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_PendingGatewaySubscriptionId",
                table: "Subscription",
                column: "PendingGatewaySubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_ScheduledPlanId",
                table: "Subscription",
                column: "ScheduledPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscription_Plan_ScheduledPlanId",
                table: "Subscription",
                column: "ScheduledPlanId",
                principalTable: "Plan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscription_Plan_ScheduledPlanId",
                table: "Subscription");

            migrationBuilder.DropIndex(
                name: "IX_Subscription_PendingGatewaySubscriptionId",
                table: "Subscription");

            migrationBuilder.DropIndex(
                name: "IX_Subscription_ScheduledPlanId",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "PendingGatewaySubscriptionId",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "ScheduledPlanId",
                table: "Subscription");
        }
    }
}
