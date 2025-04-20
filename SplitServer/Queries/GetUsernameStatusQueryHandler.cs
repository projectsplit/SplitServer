using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetUsernameStatusQueryHandler : IRequestHandler<GetUsernameStatusQuery, Result<GetUsernameStatusResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ValidationService _validationService;

    public GetUsernameStatusQueryHandler(
        IUsersRepository usersRepository,
        ValidationService validationService)
    {
        _usersRepository = usersRepository;
        _validationService = validationService;
    }

    public async Task<Result<GetUsernameStatusResponse>> Handle(GetUsernameStatusQuery query, CancellationToken ct)
    {
        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetUsernameStatusResponse>($"User with id {query.UserId} was not found");
        }

        var user = userMaybe.Value;

        var usernameValidationResult = _validationService.ValidateUsername(query.Username);

        var alreadyTaken = await _usersRepository.AnyWithUsername(query.Username, ct);
        var isSimilar = string.Equals(query.Username, user.Username, StringComparison.InvariantCultureIgnoreCase);

        return new GetUsernameStatusResponse
        {
            IsValid = usernameValidationResult.IsSuccess,
            ErrorMessage = usernameValidationResult.IsFailure ? usernameValidationResult.Error : null,
            IsAvailable = isSimilar || !alreadyTaken
        };
    }
}