namespace Api.Framework.Result;

public abstract class ApiResult
{
    public bool Successful { get; set; }
    public int ErrorCode { get; set; }

    public string? Message { get; set; }
}