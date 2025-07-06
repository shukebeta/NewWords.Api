using Api.Framework.Exceptions;
using Api.Framework.Extensions;
using Api.Framework.Helper;
using EventId = NewWords.Api.Enums.EventId;

namespace NewWords.Api.Helpers;

public static class ExceptionHelper
{
    public static CustomException<object> New(EventId eventId, params object[] extraObjects)
    {
        return CustomExceptionHelper.New<object>(eventId, (int)eventId, eventId.Description(extraObjects));
    }

    public static CustomException<object> New(object data, EventId eventId, params object[] extraObjects)
    {
        return CustomExceptionHelper.New(data, (int)eventId, eventId.Description(extraObjects));
    }
}
