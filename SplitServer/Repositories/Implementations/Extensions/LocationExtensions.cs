using MongoDB.Driver.GeoJsonObjectModel;
using SplitServer.Models;
using SplitServer.Repositories.Implementations.Models;

namespace SplitServer.Repositories.Implementations.Extensions;

public static class LocationExtensions
{
    public static MongoDbLocation ToMongoDbLocation(this Location location)
    {
        var mongoDbCoordinates = new GeoJson2DCoordinates(location.Coordinates.Latitude, location.Coordinates.Longitude);

        return new MongoDbLocation
        {
            Geo = new GeoJsonPoint<GeoJson2DCoordinates>(mongoDbCoordinates),
            Google = location.Google
        };
    }
}