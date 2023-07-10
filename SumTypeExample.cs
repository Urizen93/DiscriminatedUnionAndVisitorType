using OneOf;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace DiscriminatedUnionAndVisitorType;

public static class SumTypeExample
{
    public sealed record Geometry(string Value);
    public sealed record AreaName(string Value);
    public sealed record ServerDefinedGeometryQuery(string Value);

    public sealed class GeometryRequest : OneOfBase<Geometry, AreaName, ServerDefinedGeometryQuery>
    {
        public GeometryRequest(OneOf<Geometry, AreaName, ServerDefinedGeometryQuery> input) : base(input)
        {
        }

        public static implicit operator GeometryRequest(Geometry geometry) => new(geometry);
        
        public static implicit operator GeometryRequest(AreaName areaName) => new(areaName);
        
        public static implicit operator GeometryRequest(ServerDefinedGeometryQuery query) => new(query);
    }

    public sealed class WhereConditionDto
    {
        public string? Expression { get; set; }
        
        public string? Geometry { get; set; }
    }

    public sealed record WhereConditionModel(string? Expression, GeometryRequest? Geometry);

    #region interfaces

    public interface IExpressionValidator { string Validate(string expression); }

    public interface IGeometryFactory { Geometry Create(string geometry); }

    public interface IAreaNameFactory { AreaName Create(string areaName); }

    #endregion

    public sealed class WhereConditionFactory
    {
        #region ctor
        
        private readonly IExpressionValidator _expressionValidator;
        private readonly IGeometryFactory _geometryFactory;
        private readonly IAreaNameFactory _areaNameFactory;

        public WhereConditionFactory(
            IExpressionValidator expressionValidator,
            IGeometryFactory geometryFactory,
            IAreaNameFactory areaNameFactory)
        {
            _expressionValidator = expressionValidator;
            _geometryFactory = geometryFactory;
            _areaNameFactory = areaNameFactory;
        }
        
        #endregion

        public WhereConditionModel Create(WhereConditionDto dto)
        {
            var expression = dto.Expression is not null
                ? _expressionValidator.Validate(dto.Expression)
                : null;

            var geometryRequest = dto.Geometry is not null
                ? dto.Geometry.Contains('$')
                    ? (GeometryRequest) _areaNameFactory.Create(dto.Geometry)
                    : _geometryFactory.Create(dto.Geometry)
                : null;

            return new WhereConditionModel(expression, geometryRequest);
        }
    }

    public sealed class Service
    {
        public WhereConditionModel QueryFromAreaOfInterest(WhereConditionModel whereCondition, string areaOfInterest) =>
            whereCondition with { Geometry = FromAreaOfInterest(areaOfInterest) };
        
        private static ServerDefinedGeometryQuery FromAreaOfInterest(string areaOfInterest) =>
            new($"ST_Transform(ST_Buffer(ST_Transform(....{areaOfInterest}");
    }

    public static string ToSqlQuery(GeometryRequest geometry) =>
        geometry.Match(
            geometryInput => FromUserInput(geometryInput),
            area => FromAreaName(area),
            FromServerGeneratedQuery);
    
    private static string FromUserInput(Geometry geometry) =>
        $"ST_MakeValid(ST_GeomFromText('{geometry.Value}', 4326))";
        
    private static string FromAreaName(AreaName areaName) =>
        $"SELECT geometry FROM area WHERE name = {areaName.Value}";

    private static string FromServerGeneratedQuery(ServerDefinedGeometryQuery query) =>
        query.Value;
}