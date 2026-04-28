using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OperatorPackage.Core;

namespace OperatorRunner
{

public static class RequestNormalizer
{
    public static NormalizedRequest Parse(string json)
    {
        var root = JObject.Parse(json);

        if (root["selectedWorkflow"] != null)
            return new NormalizedRequest
            {
                RequestKind = RequestKind.UnityExport,
                Plan = FromUnityExport(json)
            };

        if (root["execution_graph"] != null)
            return new NormalizedRequest
            {
                RequestKind = RequestKind.StandardWorkflow,
                Plan = FromStandardWorkflow(json)
            };

        if (root["operator_type"] != null)
            return new NormalizedRequest
            {
                RequestKind = RequestKind.StandardOperator,
                Plan = FromStandardOperator(json)
            };

        return new NormalizedRequest
        {
            RequestKind = RequestKind.ExecutionPlan,
            Plan = JsonConvert.DeserializeObject<ExecutionPlan>(json, JsonDefaults.Settings) ?? new ExecutionPlan()
        };
    }

    private static ExecutionPlan FromUnityExport(string json)
    {
        var export = JsonConvert.DeserializeObject<UnityExportEnvelope>(json, JsonDefaults.Settings) ?? new UnityExportEnvelope();
        return new ExecutionPlan
        {
            WorkflowId = export.Meta.TaskId ?? "wf_unity_export",
            DataPath = export.Meta.SourceDataPath,
            Workflow = export.SelectedWorkflow.Operators,
            Mapping = export.Task.ParsedSpec.Mapping,
            SpatialRegion = export.Task.ParsedSpec.SpatialRegion,
            TimeWindow = export.Task.ParsedSpec.TimeWindow,
            NormalizeColumns = export.Task.ParsedSpec.NormalizeColumns,
            FilterColumn = export.Task.ParsedSpec.Filter?.Column,
            FilterValue = export.Task.ParsedSpec.Filter?.Value,
            TimeColumn = export.Task.ParsedSpec.TimeColumn,
            EncodedTimeColumn = export.Task.ParsedSpec.EncodedTimeColumn,
            AtomicMode = export.Task.ParsedSpec.AtomicMode,
            RequiredViewType = export.Task.ParsedSpec.RequiredViewType,
            RecurrentHours = export.Task.ParsedSpec.RecurrentHours,
            RequireBackendBuild = export.Task.ParsedSpec.RequireBackendBuild,
            TaskDescription = export.Task.RawText,
            TaskHints = new TaskHintsRequest
            {
                RequireBackendBuild = export.Task.ParsedSpec.RequireBackendBuild,
                RequireSpatialFilter = export.Task.ParsedSpec.SpatialRegion != null,
                RequireTemporalFilter = export.Task.ParsedSpec.TimeWindow != null,
                HotspotFocus = !string.IsNullOrWhiteSpace(export.Task.RawText) &&
                               export.Task.RawText.Contains("hotspot", StringComparison.OrdinalIgnoreCase)
            }
        };
    }

    private static ExecutionPlan FromStandardWorkflow(string json)
    {
        var workflow = JsonConvert.DeserializeObject<StandardWorkflowEnvelope>(json, JsonDefaults.Settings) ?? new StandardWorkflowEnvelope();
        var plan = new ExecutionPlan
        {
            WorkflowId = workflow.WorkflowId,
            Workflow = TopologicalSort(workflow.ExecutionGraph),
        };

        ApplyCommonInputData(plan, workflow.InputData);
        ApplyCommonParameters(plan, workflow.Parameters);
        return plan;
    }

    private static ExecutionPlan FromStandardOperator(string json)
    {
        var request = JsonConvert.DeserializeObject<StandardOperatorEnvelope>(json, JsonDefaults.Settings) ?? new StandardOperatorEnvelope();
        var plan = new ExecutionPlan
        {
            WorkflowId = request.WorkflowId,
            Workflow = new List<string> { MapOperatorType(request.OperatorType) }
        };

        ApplyCommonInputData(plan, request.InputData);
        ApplyCommonParameters(plan, request.Parameters);
        return plan;
    }

    private static void ApplyCommonInputData(ExecutionPlan plan, JObject inputData)
    {
        if (inputData == null)
            return;

        if (TryGetString(inputData, "source_path", out var sourcePath))
            plan.DataPath = sourcePath!;
        else if (TryGetString(inputData, "data_path", out var dataPath))
            plan.DataPath = dataPath!;
        else if (TryGetString(inputData, "pointer", out var pointer))
            plan.DataPath = pointer!;
        else if (TryGetString(inputData, "tabular_pointer", out var tabularPointer))
            plan.DataPath = tabularPointer!;
        else if (TryReadPointerFromObject(inputData, out var nestedPointer))
            plan.DataPath = nestedPointer!;
    }

    private static void ApplyCommonParameters(ExecutionPlan plan, JObject parameters)
    {
        if (parameters == null)
            return;

        if (TryGetString(parameters, "required_view_type", out var requiredViewType))
            plan.RequiredViewType = requiredViewType;

        if (parameters["require_backend_build"] is JValue requireBackendBuild &&
            requireBackendBuild.Type == JTokenType.Boolean)
            plan.RequireBackendBuild = requireBackendBuild.Value<bool>();

        if (TryGetString(parameters, "time_column", out var timeColumn))
            plan.TimeColumn = timeColumn;
        if (TryGetString(parameters, "output_column", out var outputColumn))
            plan.EncodedTimeColumn = outputColumn;

        if (parameters["mapping"] is JObject mappingElement)
            plan.Mapping = mappingElement.ToObject<MappingRequest>(JsonDefaults.Serializer);
        else
            plan.Mapping = ParseMappingFromFlatParameters(parameters, plan.Mapping);

        if (parameters["spatial_region"] is JObject spatialElement)
            plan.SpatialRegion = spatialElement.ToObject<SpatialRegionRequest>(JsonDefaults.Serializer);

        if (parameters["time_window"] is JObject timeWindowElement)
            plan.TimeWindow = timeWindowElement.ToObject<TimeWindowRequest>(JsonDefaults.Serializer);

        if (TryGetString(parameters, "mode", out var atomicMode) &&
            Enum.TryParse<AtomicQueryMode>(atomicMode, true, out var parsedAtomicMode))
            plan.AtomicMode = parsedAtomicMode;

        if (parameters["normalize_columns"] is JArray normalizeColumnsElement)
        {
            plan.NormalizeColumns = normalizeColumnsElement
                .Where(x => x.Type == JTokenType.String)
                .Select(x => x.Value<string>()!)
                .ToList();
        }

        if (parameters["filter"] is JObject filterElement)
        {
            if (TryGetString(filterElement, "column", out var filterColumn))
                plan.FilterColumn = filterColumn;
            if (TryGetString(filterElement, "value", out var filterValue))
                plan.FilterValue = filterValue;
        }

        if (parameters["recurrent_hours"] is JArray recurrentHoursElement)
        {
            plan.RecurrentHours = recurrentHoursElement
                .Where(x => x.Type == JTokenType.Integer)
                .Select(x => x.Value<int>())
                .ToList();
        }
    }

    private static MappingRequest ParseMappingFromFlatParameters(JObject parameters, MappingRequest? existing)
    {
        var mapping = existing ?? new MappingRequest();
        if (TryGetString(parameters, "trip_id_column", out var tripId)) mapping.TripIdColumn = tripId;
        if (TryGetString(parameters, "origin_x_column", out var ox)) mapping.OriginXColumn = ox;
        if (TryGetString(parameters, "origin_y_column", out var oy)) mapping.OriginYColumn = oy;
        if (TryGetString(parameters, "origin_z_column", out var oz)) mapping.OriginZColumn = oz;
        if (TryGetString(parameters, "origin_time_column", out var ot)) mapping.OriginTimeColumn = ot;
        if (TryGetString(parameters, "destination_x_column", out var dx)) mapping.DestinationXColumn = dx;
        if (TryGetString(parameters, "destination_y_column", out var dy)) mapping.DestinationYColumn = dy;
        if (TryGetString(parameters, "destination_z_column", out var dz)) mapping.DestinationZColumn = dz;
        if (TryGetString(parameters, "destination_time_column", out var dt)) mapping.DestinationTimeColumn = dt;
        if (TryGetString(parameters, "x_column", out var x)) mapping.XColumn = x;
        if (TryGetString(parameters, "y_column", out var y)) mapping.YColumn = y;
        if (TryGetString(parameters, "z_column", out var z)) mapping.ZColumn = z;
        if (TryGetString(parameters, "time_column", out var t)) mapping.TimeColumn = t;
        if (TryGetString(parameters, "color_column", out var c)) mapping.ColorColumn = c;
        if (TryGetString(parameters, "size_column", out var s)) mapping.SizeColumn = s;
        if (parameters["stc_mode"] is JValue stcMode && stcMode.Type == JTokenType.Boolean)
            mapping.IsSTCMode = stcMode.Value<bool>();
        return mapping;
    }

    private static List<string> TopologicalSort(ExecutionGraph graph)
    {
        if (graph.Nodes.Count == 0)
            return new List<string>();

        var inDegree = graph.Nodes.ToDictionary(node => node.Id, _ => 0);
        foreach (var edge in graph.Edges)
        {
            if (inDegree.ContainsKey(edge.To))
                inDegree[edge.To]++;
        }

        var queue = new Queue<ExecutionNode>(graph.Nodes.Where(node => inDegree[node.Id] == 0));
        var ordered = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            ordered.Add(MapOperatorType(node.Type));

            foreach (var edge in graph.Edges.Where(edge => edge.From == node.Id))
            {
                inDegree[edge.To]--;
                if (inDegree[edge.To] == 0)
                {
                    var next = graph.Nodes.First(candidate => candidate.Id == edge.To);
                    queue.Enqueue(next);
                }
            }
        }

        if (ordered.Count != graph.Nodes.Count)
            throw new InvalidOperationException("The execution graph contains a cycle or unresolved node references.");

        return ordered;
    }

    public static string MapOperatorType(string operatorType) => operatorType switch
    {
        "Read_Data_Op" => "ReadDataOperator",
        "Filter_Rows_Op" => "FilterRowsOperator",
        "Normalize_Attributes_Op" => "NormalizeAttributesOperator",
        "Encode_Time_Op" => "EncodeTimeOperator",
        "Map_To_Visual_Space_Op" => "MapToVisualSpaceOperator",
        "Build_Point_View_Op" => "BuildPointViewOperator",
        "Build_STC_View_Bundle_Op" => "BuildSTCViewOperator",
        "Build_2D_Projection_View_Op" => "Build2DProjectionViewOperator",
        "Build_Link_View_Op" => "BuildLinkViewOperator",
        "Create_Atomic_Query_Op" => "CreateAtomicQueryOperator",
        "Create_Directional_Query_Op" => "CreateDirectionalQueryOperator",
        "Merge_Queries_Op" => "MergeQueriesOperator",
        "Recurrent_Query_Compose_Op" => "RecurrentQueryComposeOperator",
        "Apply_Spatial_Filter_Op" => "ApplySpatialFilterOperator",
        "Apply_Temporal_Filter_Op" => "ApplyTemporalFilterOperator",
        "Combine_Filters_Op" => "CombineFiltersOperator",
        "Update_View_Encoding_Op" => "UpdateViewEncodingOperator",
        "Adapted_IATK_View_Builder_Op" => "AdaptedIATKViewBuilderOperator",
        "Adapted_IATK_View_Op" => "AdaptedIATKViewBuilderOperator",
        "Build_STC_View_Op" => "BuildSTCViewOperator",
        _ => operatorType
    };

    private static bool TryGetString(JObject element, string propertyName, out string? value)
    {
        if (element[propertyName] is JValue property &&
            property.Type == JTokenType.String)
        {
            value = property.Value<string>();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadPointerFromObject(JObject inputData, out string? pointer)
    {
        if (inputData["pointers"] is JObject pointersElement)
        {
            foreach (var property in pointersElement.Properties())
            {
                if (property.Value.Type == JTokenType.String)
                {
                    pointer = property.Value.Value<string>();
                    return !string.IsNullOrWhiteSpace(pointer);
                }
            }
        }

        pointer = null;
        return false;
    }
}

public static class JsonDefaults
{
    public static JsonSerializerSettings Settings => new()
    {
        Formatting = Formatting.Indented,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static JsonSerializer Serializer => JsonSerializer.Create(Settings);
}
}
