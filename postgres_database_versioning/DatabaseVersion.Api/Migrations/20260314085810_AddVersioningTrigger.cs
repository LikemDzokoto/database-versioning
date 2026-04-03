using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DatabaseVersion.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVersioningTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create function for versioning trigger
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_versioning_trigger()
                RETURNS TRIGGER AS $$
                BEGIN
                    -- Mark previous current version as not current
                    UPDATE ""TodoItems""
                    SET ""IsCurrent"" = false
                    WHERE ""EntityId"" = NEW.""EntityId"" AND ""IsCurrent"" = true;
                    
                    -- Set new record as current
                    NEW.""IsCurrent"" = true;
                    
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger
            migrationBuilder.Sql(@"
                CREATE TRIGGER versioning_trigger
                    BEFORE INSERT ON ""TodoItems""
                    FOR EACH ROW
                    EXECUTE FUNCTION update_versioning_trigger();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop trigger and function
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS versioning_trigger ON ""TodoItems"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS update_versioning_trigger();");
        }
    }
}
