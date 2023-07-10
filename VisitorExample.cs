// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace DiscriminatedUnionAndVisitorType;

public static class VisitorExample
{
    public interface IGeometryRequest
    {
        string Accept(IGeometryQueryVisitor visitor);
    }
    
    public sealed record Geometry(string Value) : IGeometryRequest
    {
        public string Accept(IGeometryQueryVisitor visitor) =>
            visitor.VisitGeometry(this);
    }
    
    public sealed record AreaName(string Value) : IGeometryRequest
    {
        public string Accept(IGeometryQueryVisitor visitor) =>
            visitor.VisitAreaName(this);
    }
    
    public sealed record ServerDefinedGeometryQuery(string Value) : IGeometryRequest
    {
        public string Accept(IGeometryQueryVisitor visitor) =>
            visitor.VisitServerDefinedGeometryQuery(this);
    }
    
    public sealed class WhereConditionDto
    {
        public string? Expression { get; set; }
        
        public string? Geometry { get; set; }
    }

    public sealed record WhereConditionModel(string? Expression, IGeometryRequest? Geometry);
    
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
                    ? (IGeometryRequest) _areaNameFactory.Create(dto.Geometry)
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

    public interface IGeometryQueryVisitor
    {
        string VisitGeometry(Geometry geometry);
    
        string VisitAreaName(AreaName areaName);
    
        string VisitServerDefinedGeometryQuery(ServerDefinedGeometryQuery query);
    }

    public sealed class GeometryQueryVisitor : IGeometryQueryVisitor
    {
        public string VisitGeometry(Geometry geometry) =>
            $"ST_MakeValid(ST_GeomFromText('{geometry.Value}', 4326))";

        public string VisitAreaName(AreaName areaName) =>
            $"SELECT geometry FROM area WHERE name = {areaName.Value}";

        public string VisitServerDefinedGeometryQuery(ServerDefinedGeometryQuery query) =>
            query.Value;
    }

    public static string CreateGeometryRequestVisitor(IGeometryQueryVisitor visitor, IGeometryRequest geometry) =>
        geometry.Accept(visitor);
}