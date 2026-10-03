using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "User",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "CustomerUserId",
                table: "Order",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_CustomerUserId",
                table: "Order",
                column: "CustomerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_User_CustomerUserId",
                table: "Order",
                column: "CustomerUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Order_User_CustomerUserId",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Order_CustomerUserId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "Order");

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
