using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class AddOtherSportsf1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8842));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8850));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8858));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8860));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8862));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8865));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8904));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8907));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8909));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 17, 14, 25, 348, DateTimeKind.Utc).AddTicks(8911));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6360));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6372));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6385));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6390));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6393));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6400));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6404));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6407));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6410));

            migrationBuilder.UpdateData(
                table: "CosmeticItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 10, 15, 0, 33, 279, DateTimeKind.Utc).AddTicks(6413));
        }
    }
}
