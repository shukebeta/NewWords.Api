namespace NewWords.Api.Services.interfaces;

public interface ICurrentUser
{
    int Id { get; }
    string Username { get; }
    string Email { get; }
}
