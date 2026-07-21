using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class EditNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6460));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6486));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6495));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6497));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6499));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6503));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6508));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6511));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6513));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 18, 17, 20, 423, DateTimeKind.Utc).AddTicks(6515));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3520));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3526));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3532));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3535));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3538));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3542));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3545));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3548));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3550));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 15, 23, 35, 562, DateTimeKind.Utc).AddTicks(3551));
        }
    }
}
