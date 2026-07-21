using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class AddOtherSports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtherSportEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SportKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompetitionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSportEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherSportLiveUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherSportEventId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MinuteOrLap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSportLiveUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherSportLiveUpdates_OtherSportEvents_OtherSportEventId",
                        column: x => x.OtherSportEventId,
                        principalTable: "OtherSportEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtherSportParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherSportEventId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeedOrNumber = table.Column<int>(type: "int", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSportParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherSportParticipants_OtherSportEvents_OtherSportEventId",
                        column: x => x.OtherSportEventId,
                        principalTable: "OtherSportEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtherSportResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherSportEventId = table.Column<int>(type: "int", nullable: false),
                    ParticipantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSportResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherSportResults_OtherSportEvents_OtherSportEventId",
                        column: x => x.OtherSportEventId,
                        principalTable: "OtherSportEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtherSportStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherSportEventId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StreamUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    M3U8Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherSportStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherSportStreams_OtherSportEvents_OtherSportEventId",
                        column: x => x.OtherSportEventId,
                        principalTable: "OtherSportEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_OtherSportEvents_SportKey_ExternalId",
                table: "OtherSportEvents",
                columns: new[] { "SportKey", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherSportLiveUpdates_OtherSportEventId_ExternalId",
                table: "OtherSportLiveUpdates",
                columns: new[] { "OtherSportEventId", "ExternalId" },
                unique: true,
                filter: "[ExternalId] IS NOT NULL AND [ExternalId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_OtherSportParticipants_OtherSportEventId",
                table: "OtherSportParticipants",
                column: "OtherSportEventId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherSportResults_OtherSportEventId",
                table: "OtherSportResults",
                column: "OtherSportEventId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherSportStreams_OtherSportEventId",
                table: "OtherSportStreams",
                column: "OtherSportEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtherSportLiveUpdates");

            migrationBuilder.DropTable(
                name: "OtherSportParticipants");

            migrationBuilder.DropTable(
                name: "OtherSportResults");

            migrationBuilder.DropTable(
                name: "OtherSportStreams");

            migrationBuilder.DropTable(
                name: "OtherSportEvents");

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
    }
}
