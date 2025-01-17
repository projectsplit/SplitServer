using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Implementations.Extensions;

public static class MongoDbLocationExtensions
{
    public static Location ToLocation(this MongoDbLocation mongoDbLocation)
    {
        var coordinates = new Coordinates
        {
            Latitude = mongoDbLocation.Geo.Coordinates.X,
            Longitude = mongoDbLocation.Geo.Coordinates.Y
        };

        return new Location
        {
            Coordinates = coordinates,
            Google = mongoDbLocation.Google
        };
    }
}