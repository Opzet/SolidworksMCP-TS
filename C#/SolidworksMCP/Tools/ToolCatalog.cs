namespace SolidworksMCP;

public static class ToolCatalog
{
    public static IReadOnlyList<McpToolDefinition> GetAllTools(AppConfiguration configuration)
    {
        var tools = new List<McpToolDefinition>();
        tools.AddRange(ModelingTools.GetTools());
        tools.AddRange(DrawingTools.GetTools());
        tools.AddRange(SketchTools.GetTools());
        tools.AddRange(ImageTraceTools.GetTools());
        tools.AddRange(ExportTools.GetTools());
        tools.AddRange(VbaTools.GetTools());
        tools.AddRange(AnalysisTools.GetTools());
        tools.AddRange(TemplateManagerTools.GetTools());
        tools.AddRange(NativeMacroTools.GetTools());

        if (configuration.EnablePdm)
        {
            // PDM tools are resource-backed today, so they are exposed through resources rather than duplicated here.
        }

        return tools;
    }
}
