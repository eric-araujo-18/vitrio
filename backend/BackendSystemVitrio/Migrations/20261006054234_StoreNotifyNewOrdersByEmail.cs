using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class StoreNotifyNewOrdersByEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewOrdersByEmail",
                table: "Store",
                type: "boolean",
                nullable: false,
                // Lojas que já existem passam a receber o aviso, como as novas.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyNewOrdersByEmail",
                table: "Store");
        }
    }
}
