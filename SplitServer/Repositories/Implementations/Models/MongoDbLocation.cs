﻿using MongoDB.Driver.GeoJsonObjectModel;
using SplitServer.Models;

namespace SplitServer.Repositories.Implementations.Models;

public class MongoDbLocation
{
    public required GeoJsonPoint<GeoJson2DCoordinates> Geo { get; init; }

    public required GooglePlace? Google { get; init; }
}