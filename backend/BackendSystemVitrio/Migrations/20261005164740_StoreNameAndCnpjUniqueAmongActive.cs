using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class StoreNameAndCnpjUniqueAmongActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Store_Cnpj",
                table: "Store");

            migrationBuilder.DropIndex(
                name: "IX_Store_Name",
                table: "Store");

            migrationBuilder.DropIndex(
                name: "IX_Store_UserId",
                table: "Store");

            migrationBuilder.CreateIndex(
                name: "IX_Store_Cnpj",
                table: "Store",
                column: "Cnpj",
                unique: true,
                filter: "\"DeletionDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Store_UserId_Name",
                table: "Store",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"DeletionDate\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Store_Cnpj",
                table: "Store");

            migrationBuilder.DropIndex(
                name: "IX_Store_UserId_Name",
                table: "Store");

            migrationBuilder.CreateIndex(
                name: "IX_Store_Cnpj",
                table: "Store",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Store_Name",
                table: "Store",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Store_UserId",
                table: "Store",
                column: "UserId");
        }
    }
}
