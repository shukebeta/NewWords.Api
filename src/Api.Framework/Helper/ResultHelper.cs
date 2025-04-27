using Api.Framework.Result;

namespace Api.Framework.Helper;

public static class ResultHelper
{
    public static SuccessfulResult<TData> New<TData>(TData data)
    {
        return new SuccessfulResult<TData>(data);
    }

    public static FailedResult<TData> New<TData>(TData data, int errorCode, string errMessage, params object[] extraObjects)
    {
        var message = string.Format(errMessage, extraObjects);
        return new FailedResult<TData>(data, message, errorCode);
    }
 
    public static FailedResult New(int errCode, string errMessage, params object[] extraObjects)
    {
        var message = string.Format(errMessage, extraObjects);
        return new FailedResult(errCode, message);
    }
}