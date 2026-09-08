using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmdNext.EF.Migration.Migrations
{
    /// <inheritdoc />
    public partial class CamelCaseSpaceSchemaJson : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CreateSpaceAsync used to serialize the entry-type template with default
            // JsonSerializer options, storing PascalCase property names
            // ({"Type":...,"Label":...,"Fields":[{"Name":...}]}). Every client reads the schema
            // camelCase like all other DTOs, so those spaces rendered an empty Type dropdown and
            // could not create entries (no type was sent). The serializer is fixed; rewrite the
            // rows written before that.
            //
            // Only the keys are renamed — values are untouched — and rows already camelCase are
            // left alone, so this is safe to re-run.
            migrationBuilder.Sql(@"
                UPDATE spaces.""Spaces"" s
                SET ""SchemaJson"" = rewritten.json::text
                FROM (
                    SELECT
                        sp.""Id"" AS id,
                        COALESCE(jsonb_agg(
                            jsonb_strip_nulls(
                                jsonb_build_object(
                                    'type',   t.value -> 'Type',
                                    'label',  t.value -> 'Label',
                                    'fields', COALESCE((
                                        SELECT jsonb_agg(
                                            jsonb_strip_nulls(
                                                jsonb_build_object(
                                                    'name',     f.value -> 'Name',
                                                    'type',     f.value -> 'Type',
                                                    'required', f.value -> 'Required',
                                                    'options',  f.value -> 'Options'
                                                )
                                            ) ORDER BY f.ordinality
                                        )
                                        FROM jsonb_array_elements(
                                            CASE WHEN jsonb_typeof(t.value -> 'Fields') = 'array'
                                                 THEN t.value -> 'Fields' ELSE '[]'::jsonb END
                                        ) WITH ORDINALITY AS f(value, ordinality)
                                    ), '[]'::jsonb)
                                )
                            ) ORDER BY t.ordinality
                        ), '[]'::jsonb) AS json
                    FROM spaces.""Spaces"" sp
                    CROSS JOIN LATERAL jsonb_array_elements(sp.""SchemaJson""::jsonb)
                        WITH ORDINALITY AS t(value, ordinality)
                    WHERE jsonb_typeof(sp.""SchemaJson""::jsonb) = 'array'
                      AND t.value ? 'Type'
                    GROUP BY sp.""Id""
                ) AS rewritten
                WHERE s.""Id"" = rewritten.id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: reverting to PascalCase would just reintroduce the broken entry form.
        }
    }
}
