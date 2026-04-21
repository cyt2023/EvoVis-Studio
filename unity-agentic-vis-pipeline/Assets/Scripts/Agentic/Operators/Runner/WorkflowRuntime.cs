using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OperatorPackage.Backend;
using OperatorPackage.Core;
using OperatorPackage.Data;
using OperatorPackage.Filter;
using OperatorPackage.Query;
using OperatorPackage.View;

namespace OperatorRunner;

public sealed class WorkflowRuntime
{
    public RunnerResponse RunFromJson(string requestJson, string requestSourcePath = null)
    {
        var normalized = RequestNormalizer.Parse(requestJson, JsonDefaults.Options);
        ResolveDataPath(normalized.Plan, requestSourcePath);
        return Run(normalized);
    }

    public async Task<RunnerResponse> RunFromFileAsync(string requestPath)
    {
        var requestJson = await File.ReadAllTextAsync(requestPath);
        return RunFromJson(requestJson, requestPath);
    }

    public RunnerResponse Run(NormalizedRequest normalizedRequest)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = ExecuteWorkflow(normalizedRequest.Plan, normalizedRequest.RequestKind);
            response.Runtime.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            return response;
        }
        catch (Exception ex)
        {
            return new RunnerResponse
            {
                Success = false,
                Status = "failed",
                WorkflowId = normalizedRequest.Plan.WorkflowId,
                RequestKind = normalizedRequest.RequestKind.ToString(),
                Workflow = normalizedRequest.Plan.Workflow,
                Errors = new List<string> { ex.Message },
                Runtime = new RuntimeMetadata
                {
                    ExecutedAtUtc = DateTime.UtcNow.ToString("O"),
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    DataPath = normalizedRequest.Plan.DataPath,
                    OperatorCount = normalizedRequest.Plan.Workflow.Count
                }
            };
        }
    }

    private static RunnerResponse ExecuteWorkflow(ExecutionPlan plan, RequestKind requestKind)
    {
        if (string.IsNullOrWhiteSpace(plan.DataPath))
            throw new InvalidOperationException("Execution plan is missing a data path.");

        if (plan.Workflow.Count == 0)
            throw new InvalidOperationException("Execution plan does not contain any operators.");

        var context = new WorkflowExecutionContext();
        context.Store.Set(plan.DataPath, plan.DataPath);
        var traces = new List<OperatorTraceRecord>();

        foreach (var opName in plan.Workflow)
        {
            var trace = new OperatorTraceRecord
            {
                OperatorName = opName
            };

            switch (opName)
            {
                case "ReadDataOperator":
                    context.Table = new ReadDataOperator().Execute(plan.DataPath);
                    SetPointer(context, "tabular://current", context.Table, trace);
                    break;

                case "NormalizeAttributesOperator":
                    context.Table = new NormalizeAttributesOperator
                    {
                        TargetColumns = plan.NormalizeColumns ?? new List<string>()
                    }.Execute(context.Table);
                    SetPointer(context, "tabular://current", context.Table, trace);
                    break;

                case "FilterRowsOperator":
                    context.Table = new FilterRowsOperator
                    {
                        FilterColumn = plan.FilterColumn ?? string.Empty,
                        FilterValue = plan.FilterValue ?? string.Empty
                    }.Execute(context.Table);
                    SetPointer(context, "tabular://current", context.Table, trace);
                    break;

                case "EncodeTimeOperator":
                    context.Table = new EncodeTimeOperator
                    {
                        TimeColumn = plan.TimeColumn ?? "pickup_datetime",
                        OutputColumn = plan.EncodedTimeColumn ?? "EncodedTime"
                    }.Execute(context.Table);
                    SetPointer(context, "tabular://current", context.Table, trace);
                    break;

                case "MapToVisualSpaceOperator":
                    context.VisualData = new MapToVisualSpaceOperator
                    {
                        Mapping = plan.Mapping?.ToVisualMapping() ?? new VisualMapping()
                    }.Execute(context.Table);
                    SetPointer(context, "visual://current", context.VisualData, trace);
                    break;

                case "BuildSTCViewOperator":
                    EnsureVisualData(context);
                    context.View = new BuildSTCViewOperator().Execute(context.VisualData!);
                    SetPointer(context, "view://current", context.View, trace);
                    break;

                case "BuildPointViewOperator":
                    EnsureVisualData(context);
                    context.View = new BuildPointViewOperator
                    {
                        Role = plan.AtomicMode switch
                        {
                            AtomicQueryMode.Origin => PointRole.Origin,
                            AtomicQueryMode.Destination => PointRole.Destination,
                            _ => PointRole.Generic
                        }
                    }.Execute(context.VisualData!);
                    SetPointer(context, "view://current", context.View, trace);
                    break;

                case "Build2DProjectionViewOperator":
                    EnsureVisualData(context);
                    context.View = new Build2DProjectionViewOperator().Execute(context.VisualData!);
                    SetPointer(context, "view://current", context.View, trace);
                    break;

                case "BuildLinkViewOperator":
                    EnsureVisualData(context);
                    context.View = new BuildLinkViewOperator().Execute(context.VisualData!);
                    SetPointer(context, "view://current", context.View, trace);
                    break;

                case "CreateAtomicQueryOperator":
                    context.SpatialQuery = new CreateAtomicQueryOperator
                    {
                        Mode = plan.AtomicMode
                    }.Execute((
                        plan.SpatialRegion?.MinX ?? 0f,
                        plan.SpatialRegion?.MinY ?? 0f,
                        plan.SpatialRegion?.MinTime ?? 0f,
                        plan.SpatialRegion?.MaxX ?? 0f,
                        plan.SpatialRegion?.MaxY ?? 0f,
                        plan.SpatialRegion?.MaxTime ?? float.MaxValue
                    ));
                    context.CurrentQuery = context.SpatialQuery;
                    SetPointer(context, "query://current", context.CurrentQuery, trace);
                    break;

                case "CreateDirectionalQueryOperator":
                    context.CurrentQuery = new CreateDirectionalQueryOperator
                    {
                        DestinationQuery = context.SpatialQuery ?? new QueryDefinition()
                    }.Execute(context.CurrentQuery ?? context.SpatialQuery ?? new QueryDefinition());
                    SetPointer(context, "query://current", context.CurrentQuery, trace);
                    break;

                case "RecurrentQueryComposeOperator":
                    context.CurrentQuery = new RecurrentQueryComposeOperator
                    {
                        Hours = plan.RecurrentHours ?? new List<int>()
                    }.Execute(new List<QueryDefinition> { context.CurrentQuery ?? context.SpatialQuery ?? new QueryDefinition() });
                    SetPointer(context, "query://current", context.CurrentQuery, trace);
                    break;

                case "MergeQueriesOperator":
                    context.CurrentQuery = new MergeQueriesOperator().Execute(
                        new List<QueryDefinition> { context.CurrentQuery ?? context.SpatialQuery ?? new QueryDefinition() });
                    SetPointer(context, "query://current", context.CurrentQuery, trace);
                    break;

                case "ApplySpatialFilterOperator":
                    EnsureVisualData(context);
                    context.SpatialMask = new ApplySpatialFilterOperator
                    {
                        Query = context.SpatialQuery ?? context.CurrentQuery ?? new QueryDefinition()
                    }.Execute(context.VisualData!);
                    SetPointer(context, "mask://spatial", context.SpatialMask, trace);
                    break;

                case "ApplyTemporalFilterOperator":
                    EnsureVisualData(context);
                    var temporalQuery = new QueryDefinition
                    {
                        AtomicMode = plan.AtomicMode,
                        TimeWindow = new TimeWindow
                        {
                            Start = plan.TimeWindow?.Start ?? 0f,
                            End = plan.TimeWindow?.End ?? float.MaxValue
                        }
                    };
                    context.TemporalMask = new ApplyTemporalFilterOperator
                    {
                        Query = temporalQuery
                    }.Execute(context.VisualData!);
                    SetPointer(context, "mask://temporal", context.TemporalMask, trace);
                    break;

                case "CombineFiltersOperator":
                    var availableMasks = new List<FilterMask>();
                    if (context.SpatialMask != null) availableMasks.Add(context.SpatialMask);
                    if (context.TemporalMask != null) availableMasks.Add(context.TemporalMask);
                    if (availableMasks.Count == 0)
                        throw new InvalidOperationException("CombineFiltersOperator requires at least one available filter mask.");
                    context.FinalMask = new CombineFiltersOperator
                    {
                        Mode = "AND"
                    }.Execute(availableMasks);
                    SetPointer(context, "mask://final", context.FinalMask, trace);
                    break;

                case "UpdateViewEncodingOperator":
                    EnsureView(context);
                    context.FinalMask ??= context.SpatialMask ?? context.TemporalMask
                        ?? throw new InvalidOperationException("UpdateViewEncodingOperator requires an available mask.");
                    context.View = new UpdateViewEncodingOperator
                    {
                        TargetView = context.View
                    }.Execute(context.FinalMask);
                    SetPointer(context, "view://current", context.View!, trace);
                    break;

                case "AdaptedIATKViewBuilderOperator":
                    EnsureView(context);
                    context.View = ApplyBackendBuild(context.View);
                    SetPointer(context, "view://current", context.View!, trace);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported operator '{opName}'.");
            }

            UpdateTrace(trace, context);
            traces.Add(trace);
        }

        context.FinalMask ??= context.SpatialMask ?? context.TemporalMask;
        var selectedRowIds = GetSelectedRowIds(context.View);

        return new RunnerResponse
        {
            Success = true,
            Status = "success",
            WorkflowId = plan.WorkflowId,
            RequestKind = requestKind.ToString(),
            Workflow = plan.Workflow,
            ViewType = context.View?.Type.ToString() ?? "None",
            TotalRows = context.Table.RowCount,
            SelectedPointCount = context.View?.PointData?.Points?.Count(p => p.IsSelected) ?? 0,
            SelectedRowIds = selectedRowIds,
            BackendBuilt = context.View?.BackendViewObject != null,
            EncodingState = context.View?.EncodingState?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>(),
            SelfEvaluation = Evaluate(plan, selectedRowIds, context),
            VisualizationPayload = BuildVisualizationPayload(plan, context, selectedRowIds),
            Diagnostics = BuildDiagnostics(context),
            Artifacts = BuildArtifacts(context),
            OperatorTrace = traces,
            Runtime = new RuntimeMetadata
            {
                ExecutedAtUtc = DateTime.UtcNow.ToString("O"),
                DataPath = plan.DataPath,
                OperatorCount = plan.Workflow.Count
            }
        };
    }

    private static void ResolveDataPath(ExecutionPlan plan, string requestSourcePath)
    {
        if (plan == null || string.IsNullOrWhiteSpace(plan.DataPath))
            return;

        if (File.Exists(plan.DataPath))
        {
            plan.DataPath = Path.GetFullPath(plan.DataPath);
            return;
        }

        var candidates = BuildCandidatePaths(plan.DataPath, requestSourcePath);
        var resolvedPath = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(resolvedPath))
            plan.DataPath = Path.GetFullPath(resolvedPath);
    }

    private static List<string> BuildCandidatePaths(string dataPath, string requestSourcePath)
    {
        var candidates = new List<string>();
        var trimmedPath = dataPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPath))
            return candidates;

        candidates.Add(trimmedPath);

        var fileName = Path.GetFileName(trimmedPath);
        var requestDirectory = !string.IsNullOrWhiteSpace(requestSourcePath)
            ? Path.GetDirectoryName(requestSourcePath)
            : null;

        AddCandidate(candidates, requestDirectory, trimmedPath);
        AddCandidate(candidates, requestDirectory, fileName);
        AddCandidate(candidates, requestDirectory, "demo_data", fileName);

        if (!string.IsNullOrWhiteSpace(requestDirectory))
        {
            var parentDirectory = Directory.GetParent(requestDirectory)?.FullName;
            AddCandidate(candidates, parentDirectory, trimmedPath);
            AddCandidate(candidates, parentDirectory, fileName);
            AddCandidate(candidates, parentDirectory, "demo_data", fileName);
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, params string[] segments)
    {
        if (segments == null || segments.Length == 0 || segments.Any(string.IsNullOrWhiteSpace))
            return;

        var combined = Path.Combine(segments);
        if (!candidates.Contains(combined, StringComparer.OrdinalIgnoreCase))
            candidates.Add(combined);
    }

    private static void EnsureVisualData(WorkflowExecutionContext context)
    {
        if (context.VisualData == null)
            throw new InvalidOperationException("The current workflow step requires visual data, but none is available.");
    }

    private static void EnsureView(WorkflowExecutionContext context)
    {
        if (context.View == null)
            throw new InvalidOperationException("The current workflow step requires a view, but none is available.");
    }

    private static void SetPointer(WorkflowExecutionContext context, string pointer, object value, OperatorTraceRecord trace)
    {
        context.Store.Set(pointer, value);
        trace.UpdatedPointers.Add(pointer);
    }

    private static void UpdateTrace(OperatorTraceRecord trace, WorkflowExecutionContext context)
    {
        trace.ViewType = context.View?.Type.ToString();
        trace.TableRows = context.Table.RowCount;
        trace.PointCount = context.VisualData?.Points.Count
            ?? context.View?.PointData?.Points.Count
            ?? 0;
        trace.LinkCount = context.VisualData?.Links.Count
            ?? context.View?.PointData?.Links.Count
            ?? 0;
        trace.SelectedCount = context.View?.PointData?.Points.Count(p => p.IsSelected)
            ?? context.FinalMask?.SelectedCount
            ?? context.SpatialMask?.SelectedCount
            ?? context.TemporalMask?.SelectedCount
            ?? 0;
        trace.BackendBuilt = context.View?.BackendViewObject != null;
    }

    private static ViewRepresentation? ApplyBackendBuild(ViewRepresentation? view)
    {
        if (view == null)
            return null;

        var builder = new AdaptedIATKViewBuilderOperator();
        var backend = new AdaptedIATKViewOperator();

        view = builder.Build(view);
        view = backend.CreateView(view);

        foreach (var coordinatedView in view.CoordinatedViews)
        {
            builder.Build(coordinatedView);
            backend.CreateView(coordinatedView);
        }

        return view;
    }

    private static List<string> GetSelectedRowIds(ViewRepresentation? view)
    {
        return view?.PointData?.Points?
            .Where(p => p.IsSelected)
            .Select(p => p.RowId)
            .Distinct()
            .ToList() ?? new List<string>();
    }

    private static SelfEvaluation Evaluate(ExecutionPlan plan, List<string> selectedRowIds, WorkflowExecutionContext context)
    {
        var expectedSet = new HashSet<string>(plan.ExpectedRowIds ?? new List<string>());
        var selectedSet = new HashSet<string>(selectedRowIds);
        var notes = new List<string>();
        var structuralBonus = 0f;
        var f1 = 0f;
        var precision = 0f;
        var recall = 0f;

        if (expectedSet.Count > 0)
        {
            var overlap = expectedSet.Intersect(selectedSet).Count();
            precision = selectedSet.Count == 0 ? 0f : (float)overlap / selectedSet.Count;
            recall = (float)overlap / expectedSet.Count;
            f1 = (precision + recall) == 0f ? 0f : (2f * precision * recall) / (precision + recall);
        }
        else
        {
            var selectedRatio = context.Table.RowCount == 0 ? 0f : (float)selectedSet.Count / context.Table.RowCount;
            var hotspotBonus = plan.TaskHints?.HotspotFocus == true && selectedRatio <= 0.1f ? 0.25f : 0.12f;
            var nonEmptyBonus = selectedSet.Count > 0 ? 0.1f : 0f;
            var temporalBonus = context.TemporalMask?.SelectedCount > 0 ? 0.08f : 0f;
            var spatialBonus = context.SpatialMask?.SelectedCount > 0 ? 0.08f : 0f;
            f1 = Math.Clamp(hotspotBonus + nonEmptyBonus + temporalBonus + spatialBonus, 0f, 0.35f);
            precision = f1;
            recall = f1;
            notes.Add("No expected row ids were provided; heuristic selection-quality evaluation was used.");
        }

        if (plan.RequiredViewType == null || string.Equals(context.View?.Type.ToString(), plan.RequiredViewType, StringComparison.OrdinalIgnoreCase))
        {
            structuralBonus += 0.15f;
            notes.Add("View type matches task.");
        }

        if (context.SpatialMask != null)
        {
            structuralBonus += 0.10f;
            notes.Add("Spatial filtering was included.");
        }

        if (context.TemporalMask != null)
        {
            structuralBonus += 0.10f;
            notes.Add("Temporal filtering was included.");
        }

        if (context.View?.EncodingState?.ContainsKey("MaskApplied") == true)
        {
            structuralBonus += 0.10f;
            notes.Add("View encoding was updated.");
        }

        if (plan.RequireBackendBuild)
        {
            if (context.View?.BackendViewObject != null)
            {
                structuralBonus += 0.10f;
                notes.Add("Backend build completed.");
            }
            else
            {
                notes.Add("Backend build is missing.");
            }
        }

        var costPenalty = Math.Max(0, plan.Workflow.Count - 8) * 0.02f;
        return new SelfEvaluation
        {
            Precision = precision,
            Recall = recall,
            F1 = f1,
            Score = Math.Clamp((0.55f * f1) + structuralBonus - costPenalty, 0f, 1f),
            Notes = notes
        };
    }

    private static Dictionary<string, object> BuildVisualizationPayload(ExecutionPlan plan, WorkflowExecutionContext context, List<string> selectedRowIds)
    {
        var activeData = context.View?.PointData ?? context.VisualData ?? new VisualPointData();
        return new Dictionary<string, object>
        {
            ["primaryView"] = SerializeView(context.View),
            ["coordinatedViews"] = context.View?.CoordinatedViews?.Select(SerializeView).ToList() ?? new List<Dictionary<string, object>>(),
            ["points"] = SerializePoints(activeData.Points),
            ["links"] = SerializeLinks(activeData),
            ["encodingState"] = context.View?.EncodingState?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty)
                ?? new Dictionary<string, string>(),
            ["selectionState"] = new Dictionary<string, object>
            {
                ["selectedRowIds"] = selectedRowIds,
                ["selectedPointCount"] = activeData.Points.Count(p => p.IsSelected),
                ["spatialSelectedCount"] = context.SpatialMask?.SelectedCount ?? 0,
                ["temporalSelectedCount"] = context.TemporalMask?.SelectedCount ?? 0,
                ["finalSelectedCount"] = context.FinalMask?.SelectedCount ?? 0
            },
            ["queryContext"] = new Dictionary<string, object?>
            {
                ["atomicMode"] = plan.AtomicMode.ToString(),
                ["requiredViewType"] = plan.RequiredViewType,
                ["spatialRegion"] = plan.SpatialRegion,
                ["timeWindow"] = plan.TimeWindow,
                ["activeQueryType"] = context.CurrentQuery?.Type.ToString() ?? context.SpatialQuery?.Type.ToString()
            },
            ["sourceDataSummary"] = new Dictionary<string, object>
            {
                ["pointCount"] = activeData.Points.Count,
                ["linkCount"] = activeData.Links.Count,
                ["timeMin"] = activeData.TimeMin,
                ["timeMax"] = activeData.TimeMax,
                ["hasODSemantics"] = activeData.HasODSemantics
            }
        };
    }

    private static Dictionary<string, object> SerializeView(ViewRepresentation? view) => new()
    {
        ["viewName"] = view?.ViewName ?? "None",
        ["viewType"] = view?.Type.ToString() ?? "None",
        ["role"] = view?.Role?.ToString() ?? "All",
        ["projectionKind"] = view?.ProjectionKind ?? string.Empty,
        ["pointCount"] = view?.PointData?.Points?.Count ?? 0,
        ["linkCount"] = view?.PointData?.Links?.Count ?? 0,
        ["backendBuilt"] = view?.BackendViewObject != null
    };

    private static List<Dictionary<string, object>> SerializePoints(List<VisualPoint> points)
    {
        return points.Select((point, index) => new Dictionary<string, object>
        {
            ["index"] = index,
            ["originalPointIndex"] = point.OriginalPointIndex,
            ["sourceRowIndex"] = point.SourceRowIndex,
            ["rowId"] = point.RowId,
            ["role"] = point.Role.ToString(),
            ["x"] = point.X,
            ["y"] = point.Y,
            ["z"] = point.Z,
            ["time"] = point.Time,
            ["colorValue"] = point.ColorValue,
            ["sizeValue"] = point.SizeValue,
            ["isSelected"] = point.IsSelected
        }).ToList();
    }

    private static List<Dictionary<string, object>> SerializeLinks(VisualPointData data)
    {
        return data.Links.Select((link, index) => new Dictionary<string, object>
        {
            ["index"] = index,
            ["originIndex"] = link.OriginIndex,
            ["destinationIndex"] = link.DestinationIndex,
            ["originRowId"] = link.OriginIndex >= 0 && link.OriginIndex < data.Points.Count ? data.Points[link.OriginIndex].RowId : string.Empty,
            ["destinationRowId"] = link.DestinationIndex >= 0 && link.DestinationIndex < data.Points.Count ? data.Points[link.DestinationIndex].RowId : string.Empty,
            ["weight"] = link.Weight
        }).ToList();
    }

    private static Dictionary<string, object> BuildDiagnostics(WorkflowExecutionContext context)
    {
        return new Dictionary<string, object>
        {
            ["tableRows"] = context.Table.RowCount,
            ["pointCount"] = context.VisualData?.Points?.Count ?? 0,
            ["linkCount"] = context.VisualData?.Links?.Count ?? 0,
            ["spatialSelectedCount"] = context.SpatialMask?.SelectedCount ?? 0,
            ["temporalSelectedCount"] = context.TemporalMask?.SelectedCount ?? 0,
            ["finalSelectedCount"] = context.FinalMask?.SelectedCount ?? 0,
            ["backendBuilt"] = context.View?.BackendViewObject != null
        };
    }

    private static List<ArtifactRecord> BuildArtifacts(WorkflowExecutionContext context)
    {
        return context.Store.Snapshot()
            .Select(kvp => new ArtifactRecord
            {
                Pointer = kvp.Key,
                Kind = GetArtifactKind(kvp.Value),
                Summary = GetArtifactSummary(kvp.Value),
                ItemCount = GetArtifactItemCount(kvp.Value)
            })
            .OrderBy(record => record.Pointer, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetArtifactKind(object value) => value switch
    {
        TabularData => "TabularData",
        VisualPointData => "VisualPointData",
        ViewRepresentation => "ViewRepresentation",
        QueryDefinition => "QueryDefinition",
        FilterMask => "FilterMask",
        string => "Path",
        _ => value.GetType().Name
    };

    private static string GetArtifactSummary(object value) => value switch
    {
        TabularData table => $"Rows={table.RowCount}, Columns={table.Columns.Count}",
        VisualPointData visual => $"Points={visual.Points.Count}, Links={visual.Links.Count}, OD={visual.HasODSemantics}",
        ViewRepresentation view => $"ViewType={view.Type}, Role={view.Role?.ToString() ?? "All"}, BackendBuilt={view.BackendViewObject != null}",
        QueryDefinition query => $"QueryType={query.Type}, AtomicMode={query.AtomicMode}",
        FilterMask mask => $"Selected={mask.SelectedCount}, TargetRole={mask.TargetRole?.ToString() ?? "All"}",
        string path => path,
        _ => value.ToString() ?? value.GetType().Name
    };

    private static int GetArtifactItemCount(object value) => value switch
    {
        TabularData table => table.RowCount,
        VisualPointData visual => visual.Points.Count,
        ViewRepresentation view => view.PointData?.Points?.Count ?? 0,
        FilterMask mask => mask.Mask.Count,
        _ => 1
    };
}
