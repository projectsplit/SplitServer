using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services.Donations;

namespace SplitServer.Commands;

/// <summary>
/// The app reporting a purchase it has just made. Nothing here trusts that report beyond the two
/// strings it carries: the product id is checked against the catalogue, and the purchase token is
/// taken to Google, which is what decides whether anything is written.
///
/// Deliberately no eligibility check. This is reachable from the settings entry, which has to keep
/// working for someone who dismissed the prompt or turned it off entirely — declining to be asked is
/// not declining to give.
/// </summary>
public class RegisterDonationPurchaseCommandHandler : IRequestHandler<RegisterDonationPurchaseCommand, Result>
{
    private readonly IUsersRepository _usersRepository;
    private readonly GooglePlayBillingService _play;
    private readonly DonationCatalog _catalog;
    private readonly DonationRecorder _recorder;

    public RegisterDonationPurchaseCommandHandler(
        IUsersRepository usersRepository,
        GooglePlayBillingService play,
        DonationCatalog catalog,
        DonationRecorder recorder)
    {
        _usersRepository = usersRepository;
        _play = play;
        _catalog = catalog;
        _recorder = recorder;
    }

    public async Task<Result> Handle(RegisterDonationPurchaseCommand command, CancellationToken ct)
    {
        if (!_play.IsConfigured)
        {
            return Result.Failure("Donations are not available");
        }

        var product = _catalog.Find(command.ProductId);

        if (product is null)
        {
            return Result.Failure($"{command.ProductId} is not a contribution product");
        }

        var userMaybe = await _usersRepository.GetById(command.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure($"User with id {command.UserId} was not found");
        }

        return product.Kind == DonationKind.Monthly
            ? await _recorder.RecordSubscriptionPurchase(command.PurchaseToken, command.UserId, ct)
            : await _recorder.RecordProductPurchase(command.ProductId, command.PurchaseToken, command.UserId, ct);
    }
}
