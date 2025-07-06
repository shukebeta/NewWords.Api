namespace Api.Framework.Result;

public class SuccessfulResult<T> : ApiResult<T>
{
    public SuccessfulResult(T data, string? message = null)
    {
        Successful = true;
        ErrorCode = 0;
        Message = message ?? "Successful";
        Data = data;
    }
}
