using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Services;

namespace SplitServer.Commands;

public class CreateManyNonGroupTransfersCommandHandler : IRequestHandler<CreateManyNonGroupTransfersCommand, Result>
{
    private readonly ITransfersRepository _transfersRepository;
    private readonly ValidationService _validationService;

    public CreateManyNonGroupTransfersCommandHandler(
        ITransfersRepository transfersRepository,
        ValidationService validationService)
    {
        _transfersRepository = transfersRepository;
        _validationService = validationService;
    }

    public async Task<Result> Handle(CreateManyNonGroupTransfersCommand command, CancellationToken ct)
    {
        foreach (var t in command.Transfers)
        {
            var transferValidationResult = _validationService.ValidateNonGroupTransfer(
                t.SenderId,
                t.ReceiverId,
                command.UserId,
                t.Amount,
                t.Currency);

            if (transferValidationResult.IsFailure)
            {
                return transferValidationResult;
            }
        }

        var now = DateTime.UtcNow;

        var transfers = command.Transfers
            .Select(x => new NonGroupTransfer
            {
                Id = Guid.NewGuid().ToString(),
                Created = now,
                Updated = now,
                CreatorId = command.UserId,
                SenderId = x.SenderId,
                ReceiverId = x.ReceiverId,
                Amount = x.Amount,
                Currency = x.Currency,
                Description = x.Description,
                Occurred = x.Occurred ?? now,
            })
            .ToList();

        return await _transfersRepository.InsertMany(transfers, ct);
    }
}