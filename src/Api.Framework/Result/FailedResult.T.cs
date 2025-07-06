namespace Api.Framework.Result;

public class FailedResult<T> : ApiResult<T>
{
    private FailedResult(string message = FrameworkConstants.DefaultErrorMessage, int errorCode = FrameworkConstants.DefaultErrorCode, T? data = default)
    {
        Successful = false;
        ErrorCode = errorCode;
        Message = message;
        Data = data;
    }

    public FailedResult(T? data, string message = FrameworkConstants.DefaultErrorMessage, int errorCode = FrameworkConstants.DefaultErrorCode) : this(message, errorCode, data)
    {
    }
    public FailedResult(T? data, string message = FrameworkConstants.DefaultErrorMessage) : this(message, FrameworkConstants.DefaultErrorCode, data)
    {
    }
}
