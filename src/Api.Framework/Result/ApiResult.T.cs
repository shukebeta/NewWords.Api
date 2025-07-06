namespace Api.Framework.Result;

public abstract class ApiResult<T> : ApiResult
{
    public T? Data { get; set; }
}