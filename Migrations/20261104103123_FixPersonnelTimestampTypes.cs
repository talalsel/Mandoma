using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixPersonnelTimestampTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
DO $$
DECLARE
    tables_and_columns text[][] := ARRAY[
        ARRAY['Employees','HireDate'],
        ARRAY['EmployeeAttendances','Date'],
        ARRAY['EmployeeDeductions','DeductionDate']
    ];
    item text[];
    current_type text;
BEGIN
    FOREACH item SLICE 1 IN ARRAY tables_and_columns
    LOOP
        SELECT data_type INTO current_type
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = item[1] AND column_name = item[2];

        IF current_type = 'timestamp with time zone' THEN
            EXECUTE format(
                'ALTER TABLE public.%I ALTER COLUMN %I TYPE timestamp without time zone USING (%I AT TIME ZONE ''UTC'')',
                item[1], item[2], item[2]);
        END IF;
    END LOOP;
END $$;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Transfers",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "StockMovements",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SoldAt",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PurchaseDate",
                table: "Purchases",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EntryDate",
                table: "JournalEntries",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "JournalEntries",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpenseDate",
                table: "Expenses",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "HireDate",
                table: "Employees",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DeductionDate",
                table: "EmployeeDeductions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "EmployeeAttendances",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "OpenedAt",
                table: "CashRegisterSessions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ClosedAt",
                table: "CashRegisterSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
