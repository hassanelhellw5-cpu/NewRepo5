using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class AddEdits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4828));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4835));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4848));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4849));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4851));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4854));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4856));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4858));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4861));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 0, 30, 4, 656, DateTimeKind.Utc).AddTicks(4863));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6387));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6396));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6402));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6404));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6406));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6409));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6418));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6420));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6423));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6425));
        }
    }
}
