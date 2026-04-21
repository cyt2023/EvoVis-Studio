using System;
using System.Collections.Generic;
using System.Linq;
using ImmersiveTaxiVis.Integration.Models;
using OperatorRunner;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public static class WorkflowRuntimeBackendResultAdapter
    {
        public static BackendResultRoot Adapt(RunnerResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            var snapshot = OperatorPackage.UnityIntegration.WorkflowPayloadReader.Read(response);
            var allPoints = snapshot.Points;
            var allLinks = snapshot.Links;

            var views = new List<BackendViewDefinition>
            {
                BuildPrimaryView(snapshot, allPoints, allLinks)
            };

            foreach (var coordinatedView in snapshot.CoordinatedViews)
            {
                views.Add(BuildCoordinatedView(coordinatedView, allPoints, allLinks, snapshot.EncodingState));
            }

            return new BackendResultRoot
            {
                meta = new BackendMeta
                {
                    schemaVersion = "2.0.0-workflow-runtime"
                },
                task = new BackendTask
                {
                    rawTaskText = response.WorkflowId
                },
                selectedWorkflow = new BackendWorkflow
                {
                    operators = response.Workflow.Select(op => new BackendOperator { name = op }).ToArray(),
                    scores = new BackendWorkflowScores
                    {
                        executionScore = response.SelfEvaluation != null ? response.SelfEvaluation.Score : 0f,
                        llmScore = 0f,
                        fitness = response.SelfEvaluation != null ? response.SelfEvaluation.Score : 0f
                    }
                },
                visualizationPayload = new BackendVisualizationPayload
                {
                    views = views.ToArray()
                },
                resultSummary = new BackendResultSummary
                {
                    selectedRowIds = response.SelectedRowIds?.ToArray() ?? Array.Empty<string>(),
                    selectedPointCount = response.SelectedPointCount,
                    backendBuilt = response.BackendBuilt
                }
            };
        }

        private static BackendViewDefinition BuildPrimaryView(
            OperatorPackage.UnityIntegration.WorkflowVisualizationSnapshot snapshot,
            List<OperatorPackage.UnityIntegration.WorkflowPointRecord> points,
            List<OperatorPackage.UnityIntegration.WorkflowLinkRecord> links)
        {
            return new BackendViewDefinition
            {
                viewName = string.IsNullOrWhiteSpace(snapshot.PrimaryView.ViewName) ? "WorkflowPrimaryView" : snapshot.PrimaryView.ViewName,
                viewType = NormalizeViewType(snapshot.PrimaryView.ViewType),
                projectionPlane = NormalizeProjectionPlane(snapshot.PrimaryView.ProjectionKind),
                visible = true,
                includeLinks = links.Count > 0,
                pointSizeScale = 1f,
                points = points.Select(MapPoint).ToArray(),
                links = links.Select(MapLink).ToArray(),
                encodingState = BuildEncodingState(snapshot.EncodingState, points)
            };
        }

        private static BackendViewDefinition BuildCoordinatedView(
            OperatorPackage.UnityIntegration.WorkflowPrimaryViewRecord coordinatedView,
            List<OperatorPackage.UnityIntegration.WorkflowPointRecord> allPoints,
            List<OperatorPackage.UnityIntegration.WorkflowLinkRecord> allLinks,
            Dictionary<string, string> sharedEncodingState)
        {
            var normalizedRole = (coordinatedView.Role ?? string.Empty).Trim();
            var normalizedType = NormalizeViewType(coordinatedView.ViewType);

            List<OperatorPackage.UnityIntegration.WorkflowPointRecord> points;
            List<BackendLinkDefinition> links;
            var includeLinks = false;

            if (string.Equals(normalizedType, "LINK", StringComparison.OrdinalIgnoreCase))
            {
                points = allPoints;
                links = allLinks.Select(MapLink).ToList();
                includeLinks = links.Count > 0;
            }
            else
            {
                points = FilterPointsByRole(allPoints, normalizedRole);
                links = new List<BackendLinkDefinition>();
            }

            return new BackendViewDefinition
            {
                viewName = string.IsNullOrWhiteSpace(coordinatedView.ViewName) ? "WorkflowCoordinatedView" : coordinatedView.ViewName,
                viewType = normalizedType,
                projectionPlane = NormalizeProjectionPlane(coordinatedView.ProjectionKind),
                visible = true,
                includeLinks = includeLinks,
                pointSizeScale = 1f,
                points = points.Select(MapPoint).ToArray(),
                links = links.ToArray(),
                encodingState = BuildEncodingState(sharedEncodingState, points)
            };
        }

        private static List<OperatorPackage.UnityIntegration.WorkflowPointRecord> FilterPointsByRole(
            List<OperatorPackage.UnityIntegration.WorkflowPointRecord> points,
            string role)
        {
            if (string.IsNullOrWhiteSpace(role) || string.Equals(role, "All", StringComparison.OrdinalIgnoreCase))
                return points;

            return points.Where(point => string.Equals(point.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private static BackendPointDefinition MapPoint(OperatorPackage.UnityIntegration.WorkflowPointRecord point)
        {
            return new BackendPointDefinition
            {
                id = point.RowId,
                x = point.X,
                y = point.Y,
                z = point.Z,
                time = point.Time,
                isSelected = point.IsSelected
            };
        }

        private static BackendLinkDefinition MapLink(OperatorPackage.UnityIntegration.WorkflowLinkRecord link)
        {
            return new BackendLinkDefinition
            {
                originIndex = link.OriginIndex,
                destinationIndex = link.DestinationIndex
            };
        }

        private static BackendEncodingState BuildEncodingState(
            Dictionary<string, string> encodingState,
            List<OperatorPackage.UnityIntegration.WorkflowPointRecord> points)
        {
            var selectedCount = points.Count(point => point.IsSelected);
            var highlightMode = encodingState != null &&
                                encodingState.TryGetValue("FilterTargetRole", out var filterTargetRole) &&
                                !string.IsNullOrWhiteSpace(filterTargetRole)
                ? filterTargetRole
                : "All";

            return new BackendEncodingState
            {
                selectedCount = selectedCount,
                highlightMode = highlightMode
            };
        }

        private static string NormalizeViewType(string viewType)
        {
            if (string.IsNullOrWhiteSpace(viewType))
                return "POINT";

            return viewType.Trim().ToUpperInvariant();
        }

        private static string NormalizeProjectionPlane(string projectionKind)
        {
            if (string.IsNullOrWhiteSpace(projectionKind))
                return "XY";

            return projectionKind.Trim().ToUpperInvariant() switch
            {
                "TEMPORAL" => "XZ",
                "SPATIAL" => "XY",
                var value => value
            };
        }
    }
}
