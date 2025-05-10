namespace NewWords.Api.Services.interfaces;

public interface ICurrentUser
{
    long Id { get; }
    string Username { get; }
    string Email { get; }
}