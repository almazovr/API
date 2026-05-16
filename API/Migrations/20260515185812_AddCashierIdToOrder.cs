using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierIdToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "email_verification_codes",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "email_verification_codes",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "email_verification_codes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "IsUsed",
                table: "email_verification_codes",
                newName: "is_used");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "email_verification_codes",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "email_verification_codes",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_email_verification_codes_Email",
                table: "email_verification_codes",
                newName: "idx_email_verification_codes_email");

            migrationBuilder.RenameIndex(
                name: "IX_email_verification_codes_Code",
                table: "email_verification_codes",
                newName: "idx_email_verification_codes_code");

            migrationBuilder.AlterColumn<string>(
                name: "brand",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_name",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_stock_threshold",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "stock_quantity",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    quantity_change = table.Column<int>(type: "integer", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("stock_movements_pkey", x => x.id);
                    table.ForeignKey(
                        name: "stock_movements_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "stock_movements_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_stock_movements_created_at",
                table: "stock_movements",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_stock_movements_product_id",
                table: "stock_movements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_stock_movements_user_id",
                table: "stock_movements",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropColumn(
                name: "category_name",
                table: "products");

            migrationBuilder.DropColumn(
                name: "min_stock_threshold",
                table: "products");

            migrationBuilder.DropColumn(
                name: "stock_quantity",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "email_verification_codes",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "email_verification_codes",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "email_verification_codes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "is_used",
                table: "email_verification_codes",
                newName: "IsUsed");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "email_verification_codes",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "email_verification_codes",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "idx_email_verification_codes_email",
                table: "email_verification_codes",
                newName: "IX_email_verification_codes_Email");

            migrationBuilder.RenameIndex(
                name: "idx_email_verification_codes_code",
                table: "email_verification_codes",
                newName: "IX_email_verification_codes_Code");

            migrationBuilder.AlterColumn<string>(
                name: "brand",
                table: "products",
                type: "character varying(500)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
