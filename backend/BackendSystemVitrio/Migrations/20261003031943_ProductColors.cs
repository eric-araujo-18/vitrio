using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendSystemVitrio.Migrations
{
    /// <inheritdoc />
    public partial class ProductColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ColorGroupId",
                table: "Product",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Product",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorName",
                table: "Product",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "OrderItem",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Product_ColorGroupId",
                table: "Product",
                column: "ColorGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Product_ColorGroupId",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ColorGroupId",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ColorName",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "OrderItem");
        }
    }
}
