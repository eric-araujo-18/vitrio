using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class OnlineOrderPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "Order",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCheckoutUrl",
                table: "Order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDeadline",
                table: "Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentLastSyncedAt",
                table: "Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentPreferenceId",
                table: "Order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StorePaymentAccount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    MercadoPagoUserId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LiveMode = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorePaymentAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorePaymentAccount_Store_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Store",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Order_Status_PaymentDeadline",
                table: "Order",
                columns: new[] { "Status", "PaymentDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_StorePaymentAccount_StoreId",
                table: "StorePaymentAccount",
                column: "StoreId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorePaymentAccount");

            migrationBuilder.DropIndex(
                name: "IX_Order_Status_PaymentDeadline",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentCheckoutUrl",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentDeadline",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentLastSyncedAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentPreferenceId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Order");
        }
    }
}
