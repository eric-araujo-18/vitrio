using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class ProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "OrderItem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VariantId",
                table: "OrderItem",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductVariant",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariant_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_VariantId",
                table: "OrderItem",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariant_ProductId_Size",
                table: "ProductVariant",
                columns: new[] { "ProductId", "Size" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItem_ProductVariant_VariantId",
                table: "OrderItem",
                column: "VariantId",
                principalTable: "ProductVariant",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItem_ProductVariant_VariantId",
                table: "OrderItem");

            migrationBuilder.DropTable(
                name: "ProductVariant");

            migrationBuilder.DropIndex(
                name: "IX_OrderItem_VariantId",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "OrderItem");
        }
    }
}
