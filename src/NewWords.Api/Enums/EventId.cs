using System.ComponentModel;

namespace NewWords.Api.Enums;


public enum EventId
{
    [Description("Successful")]
    _00000_Successful = 0,

    [Description("Failed")]
    _00001_Failed = 1,

    [Description("Note with id {0} not found")]
    _00100_LoginFailed = 100,

    [Description("Note with id {0} is private")]
    _00101_NoteIsPrivate = 101,

    [Description("Note with id {0} is not yours")]
    _00102_NoteIsNotYours = 102,

    [Description("Invalid operation: note with id {0} is not deleted")]
    _00103_NoteIsNotDeleted = 103,

    [Description("Invalid operation: Note with id {0} has already been deleted.")]
    _00104_NoteIsDeleted = 104,

    [Description("Duplicate request detected. The request has been ignored.")]
    _00105_DetectedDuplicatePostRequest = 105,

    [Description("Unknown setting name: {0}")]
    _00106_UnknownSettingName = 106,

    [Description("User not found: {0}")]
    _00107_UserNotFound = 107,

    [Description("The provided old password is incorrect")]
    _00108_OldPasswordIncorrect = 108,
}
