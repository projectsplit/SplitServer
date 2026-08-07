using SplitServer.Models;
using SplitServer.Repositories.Mappers;

namespace SplitServer.Repositories.Implementations;

public class DonationPromptStatesMongoDbRepository :
    MongoDbRepositoryBase<DonationPromptState, DonationPromptState>,
    IDonationPromptStatesRepository
{
    public DonationPromptStatesMongoDbRepository(IMongoConnection mongoConnection) :
        base(
            mongoConnection,
            "DonationPromptStates",
            new PassThroughMapper<DonationPromptState>())
    {
    }
}
