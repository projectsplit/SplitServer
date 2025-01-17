using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class SignInWithPasswordCommand : IRequest<Result<AuthTokensResult>>
{
    public string Username { get; }
    
    public string Password { get; }

    public SignInWithPasswordCommand(string username, string password)
    {
        Username = username;
        Password = password;
    }
}