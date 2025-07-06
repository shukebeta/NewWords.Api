namespace Api.Framework.Result;

public class FailedResult : ApiResult
{
    public FailedResult(string message = FrameworkConstants.DefaultErrorMessage) : this(FrameworkConstants.DefaultErrorCode, message)
    {
    }

    public FailedResult(int errorCode, string message)
    {
        Successful = false;
        ErrorCode = errorCode;
        Message = message;
    }
}
