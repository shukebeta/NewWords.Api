using Api.Framework.Exceptions;

namespace Api.Framework.Helper;

public static class CustomExceptionHelper
{
    /// <summary>
    /// Help raise a normal custom exception with errorMessage and additional data
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="data"></param>
    /// <param name="extraObjects"></param>
    /// <typeparam name="TData"></typeparam>
    /// <returns></returns>
    public static CustomException<TData> New<TData>(TData data, string errorMessage, params object[] extraObjects)
    {
        var message = string.Format(errorMessage, extraObjects);
        return new CustomException<TData>
        {
            CustomData = ResultHelper.New(data, FrameworkConstants.DefaultErrorCode, message)
        };
    }

    /// <summary>
    /// Help raise a standard custom exception with full information
    /// </summary>
    /// <param name="data"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMessage"></param>
    /// <param name="extraObjects"></param>
    /// <typeparam name="TData"></typeparam>
    /// <returns></returns>
    public static CustomException<TData> New<TData>(TData data, int errorCode, string errorMessage, params object[] extraObjects)
    {
        var message = string.Format(errorMessage, extraObjects);
        return new CustomException<TData>
        {
            CustomData = ResultHelper.New(data, errorCode, message)
        };
    }
}
