using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTypesAndReservedStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Sales",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
WITH numbered AS (
    SELECT ""Id"", 'INV-' || LPAD(ROW_NUMBER() OVER (ORDER BY ""Id"")::text, 6, '0') AS new_number
    FROM ""Sales""
    WHERE ""Type"" IN (0, 1) AND (""InvoiceNumber"" = '' OR ""InvoiceNumber"" IS NULL)
)
UPDATE ""Sales"" s SET ""InvoiceNumber"" = numbered.new_number
FROM numbered WHERE s.""Id"" = numbered.""Id"";
UPDATE ""Sales"" SET ""InvoiceNumber"" = 'DRAFT-' || LPAD(""Id""::text, 6, '0')
WHERE ""Type"" = 2 AND (""InvoiceNumber"" = '' OR ""InvoiceNumber"" IS NULL);");
            }
            else
            {
                migrationBuilder.Sql("UPDATE \"Sales\" SET \"InvoiceNumber\" = 'INV-' || printf('%06d', \"Id\") WHERE \"Type\" IN (0, 1) AND (\"InvoiceNumber\" = '' OR \"InvoiceNumber\" IS NULL);");
                migrationBuilder.Sql("UPDATE \"Sales\" SET \"InvoiceNumber\" = 'DRAFT-' || printf('%06d', \"Id\") WHERE \"Type\" = 2 AND (\"InvoiceNumber\" = '' OR \"InvoiceNumber\" IS NULL);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Products");
        }
    }
}
