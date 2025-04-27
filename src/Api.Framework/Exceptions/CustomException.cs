using Api.Framework.Result;

namespace Api.Framework.Exceptions;

using System;

public class CustomException<T>(Exception? innerException = default)
    : Exception("A custom exception happened", innerException)
{
    public FailedResult<T>? CustomData { get; set; }

    // Override ToString() method to provide more detailed exception information
    public override string ToString()
    {
        return $"{base.ToString()}, {CustomData}";
    }
}
