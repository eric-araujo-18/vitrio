using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryCaptchaEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmationSentAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmedAt",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            // Contas que já existiam antes da confirmação de e-mail continuam liberadas.
            migrationBuilder.Sql("""UPDATE "User" SET "EmailConfirmedAt" = "CreationDate";""");

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Store",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeDeliveryFrom",
                table: "Store",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Order",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmationSentAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "EmailConfirmedAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "FreeDeliveryFrom",
                table: "Store");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Order");
        }
    }
}
