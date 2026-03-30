namespace McpLumina.Models.Responses;

public sealed record SheetListResponse : BaseResponse
{
    public int      Count  { get; init; }
    public string[] Sheets { get; init; } = [];
}
