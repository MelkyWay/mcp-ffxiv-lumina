namespace McpLumina.Models.Responses;

public sealed record WorldsResponse : BaseResponse
{
    public string?      Query       { get; init; }
    public int          TotalMatches { get; init; }
    public WorldEntry[] Worlds      { get; init; } = [];
}

public sealed record WorldEntry(
    uint   RowId,
    string Name,
    string InternalName,
    uint   DataCenterId,
    string DataCenterName,
    bool   IsPublic
);
