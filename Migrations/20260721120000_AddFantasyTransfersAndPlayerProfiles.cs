using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QemmaProject.Migrations
{
    public partial class AddFantasyTransfersAndPlayerProfiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "MetadataJson", table: "OtherSportEvents", type: "nvarchar(max)", nullable: false, defaultValue: "{}");
            migrationBuilder.AddColumn<int>(name: "Age", table: "Players", type: "int", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "BirthDate", table: "Players", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "FormerTeamsJson", table: "Players", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Height", table: "Players", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Nationality", table: "Players", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "PreferredFoot", table: "Players", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProfileMetadataJson", table: "Players", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FreeTransfersBanked", table: "FantasyEntries", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(name: "LastTransferRound", table: "FantasyEntries", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TransferPenaltyPoints", table: "FantasyEntries", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TransfersMadeThisRound", table: "FantasyEntries", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MetadataJson", table: "OtherSportEvents");
            migrationBuilder.DropColumn(name: "Age", table: "Players");
            migrationBuilder.DropColumn(name: "BirthDate", table: "Players");
            migrationBuilder.DropColumn(name: "FormerTeamsJson", table: "Players");
            migrationBuilder.DropColumn(name: "Height", table: "Players");
            migrationBuilder.DropColumn(name: "Nationality", table: "Players");
            migrationBuilder.DropColumn(name: "PreferredFoot", table: "Players");
            migrationBuilder.DropColumn(name: "ProfileMetadataJson", table: "Players");
            migrationBuilder.DropColumn(name: "FreeTransfersBanked", table: "FantasyEntries");
            migrationBuilder.DropColumn(name: "LastTransferRound", table: "FantasyEntries");
            migrationBuilder.DropColumn(name: "TransferPenaltyPoints", table: "FantasyEntries");
            migrationBuilder.DropColumn(name: "TransfersMadeThisRound", table: "FantasyEntries");
        }
    }
}
