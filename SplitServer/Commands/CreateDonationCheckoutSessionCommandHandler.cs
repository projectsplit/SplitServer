using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services.Donations;

namespace SplitServer.Commands;

public class CreateDonationCheckoutSessionCommandHandler
    : IRequestHandler<CreateDonationCheckoutSessionCommand, Result<CreateDonationCheckoutSessionResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly StripeDonationService _stripeDonationService;
    private readonly DonationPromptPolicy _policy;

    public CreateDonationCheckoutSessionCommandHandler(
        IUsersRepository usersRepository,
        StripeDonationService stripeDonationService,
        DonationPromptPolicy policy)
    {
        _usersRepository = usersRepository;
        _stripeDonationService = stripeDonationService;
        _policy = policy;
    }

    public async Task<Result<CreateDonationCheckoutSessionResponse>> Handle(
        CreateDonationCheckoutSessionCommand command,
        CancellationToken ct)
    {
        if (!_stripeDonationService.IsConfigured)
        {
            return Result.Failure<CreateDonationCheckoutSessionResponse>("Donations are not available");
        }

        // Checked here and not only in the browser. The amount arrives from the client, and the
        // bounds are what stop both a fat-fingered decimal point and a hand-rolled request.
        if (!_policy.IsAmountAllowed(command.AmountMinor))
        {
            return Result.Failure<CreateDonationCheckoutSessionResponse>(_policy.AmountOutOfRangeMessage());
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<CreateDonationCheckoutSessionResponse>($"User with id {command.UserId} was not found");
        }

        // Deliberately no eligibility check. This is reachable from the settings entry, which has to
        // keep working for someone who dismissed the prompt or opted out of it entirely — turning
        // off the asking is not the same as refusing to accept.
        var urlResult = await _stripeDonationService.CreateCheckoutSession(
            command.UserId,
            command.AmountMinor,
            command.Monthly ? DonationKind.Monthly : DonationKind.OneTime,
            ct);

        if (urlResult.IsFailure)
        {
            return urlResult.ConvertFailure<CreateDonationCheckoutSessionResponse>();
        }

        return new CreateDonationCheckoutSessionResponse
        {
            CheckoutUrl = urlResult.Value,
        };
    }
}
