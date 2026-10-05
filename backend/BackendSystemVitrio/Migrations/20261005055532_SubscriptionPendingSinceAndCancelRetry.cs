using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionPendingSinceAndCancelRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewaySubscriptionIdToCancel",
                table: "Subscription",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingSince",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: true);

            // Checkouts já abertos: a melhor estimativa de quando foram abertos é a última atualização.
            migrationBuilder.Sql(
                "UPDATE \"Subscription\" SET \"PendingSince\" = COALESCE(\"UpdatedDate\", \"CreationDate\") " +
                "WHERE \"PendingGatewaySubscriptionId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewaySubscriptionIdToCancel",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "PendingSince",
                table: "Subscription");
        }
    }
}
