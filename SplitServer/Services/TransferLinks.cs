using SplitServer.Models;
using SplitServer.Queries.Models;

namespace SplitServer.Services;

/// <summary>
/// In-app links that open a transfer list centred on one transfer, with it marked: the transfer
/// counterpart of <see cref="ExpenseLinks"/>, so a notification about a transfer lands on it too.
/// </summary>
public static class TransferLinks
{
    public static string JumpTo(GroupTransfer transfer) =>
        $"/shared/{transfer.GroupId}/transfers?jumpTo={CreateJumpToken(transfer)}";

    public static string JumpTo(NonGroupTransfer transfer) =>
        $"/shared/nongroup/transfers?jumpTo={CreateJumpToken(transfer)}";

    private static string CreateJumpToken(Transfer transfer)
    {
        var details = new NextTransferPageDetails
        {
            Created = ExpenseLinks.AsStored(transfer.Created),
            Occurred = ExpenseLinks.AsStored(transfer.Occurred),
            IsJumpTo = true
        };

        return Uri.EscapeDataString(Next.Encode(details));
    }
}
