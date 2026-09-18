namespace SolidworksMCP;

public sealed record SolidWorksModel
{
    public required string Path { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public bool IsActive { get; init; }

    public string? TemplatePath { get; init; }

    public string? TemplateSource { get; init; }
}

public sealed record SolidWorksFeature
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public bool Suppressed { get; init; }
}

public sealed record SelectionSpec
{
    public IReadOnlyList<int>? Faces { get; init; }

    public IReadOnlyList<int>? Edges { get; init; }

    public IReadOnlyList<int>? Vertices { get; init; }

    public IReadOnlyList<string>? Planes { get; init; }

    public IReadOnlyList<string>? Axes { get; init; }

    public IReadOnlyList<string>? Sketches { get; init; }

    public IReadOnlyList<int>? SketchSegments { get; init; }

    public IReadOnlyList<int>? SketchPoints { get; init; }

    public string? SketchName { get; init; }

    public IReadOnlyList<string>? Features { get; init; }

    public IReadOnlyList<string>? Components { get; init; }
}

public sealed record EntityDescriptor
{
    public required int Index { get; init; }

    public required string Kind { get; init; }

    public required string Name { get; init; }

    public string? Type { get; init; }

    public string? Handle { get; init; }
}

public sealed record SolidWorksDrawing
{
    public required string Name { get; init; }

    public int Sheets { get; init; }

    public double Scale { get; init; }
}

public sealed record SolidWorksDimension
{
    public required string Name { get; init; }

    public double Value { get; init; }

    public required string Units { get; init; }
}

public sealed record SolidWorksConfiguration
{
    public required string Name { get; init; }

    public bool IsActive { get; init; }

    public string? Parent { get; init; }
}

public sealed record MassProperties
{
    public double Mass { get; init; }

    public double Volume { get; init; }

    public double SurfaceArea { get; init; }

    public required CenterOfMass CenterOfMass { get; init; }
}

public sealed record CenterOfMass
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }
}

public sealed record ExportOptions
{
    public required string Format { get; init; }

    public string? Version { get; init; }

    public bool? Binary { get; init; }

    public string? Units { get; init; }
}

public sealed record VBAParameter
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Description { get; init; }

    public object? Default { get; init; }
}

public sealed record VBAScript
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Code { get; init; }

    public IReadOnlyList<VBAParameter>? Parameters { get; init; }
}
