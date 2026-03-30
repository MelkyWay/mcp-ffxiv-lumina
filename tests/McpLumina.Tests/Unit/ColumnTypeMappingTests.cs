using Lumina.Data.Structs.Excel;
using McpLumina.Services;
using Xunit;

namespace McpLumina.Tests.Unit;

/// <summary>
/// Verifies the ExcelColumnDataType → contract string mapping in GenericSheetReader.
/// Uses reflection to call the private ColumnTypeToString method so the mapping table
/// stays in one place (the production code) and tests stay in sync automatically.
/// </summary>
public sealed class ColumnTypeMappingTests
{
    private static string Map(ExcelColumnDataType type)
    {
        var method = typeof(GenericSheetReader)
            .GetMethod("ColumnTypeToString",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        return (string)method.Invoke(null, [type])!;
    }

    [Theory]
    [InlineData(ExcelColumnDataType.String,  "string")]
    [InlineData(ExcelColumnDataType.Bool,    "bool")]
    [InlineData(ExcelColumnDataType.Int8,    "int")]
    [InlineData(ExcelColumnDataType.Int16,   "int")]
    [InlineData(ExcelColumnDataType.Int32,   "int")]
    [InlineData(ExcelColumnDataType.Int64,   "int")]
    [InlineData(ExcelColumnDataType.UInt8,   "uint")]
    [InlineData(ExcelColumnDataType.UInt16,  "uint")]
    [InlineData(ExcelColumnDataType.UInt32,  "uint")]
    [InlineData(ExcelColumnDataType.UInt64,  "uint")]
    [InlineData(ExcelColumnDataType.Float32, "float")]
    public void ColumnType_MapsToExpectedContractString(ExcelColumnDataType type, string expected)
    {
        Assert.Equal(expected, Map(type));
    }

    [Theory]
    [InlineData(ExcelColumnDataType.PackedBool0)]
    [InlineData(ExcelColumnDataType.PackedBool1)]
    [InlineData(ExcelColumnDataType.PackedBool7)]
    public void PackedBool_MapsToBool(ExcelColumnDataType type)
    {
        Assert.Equal("bool", Map(type));
    }
}
