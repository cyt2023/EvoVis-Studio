using System;
using System.Collections.Generic;
using System.Globalization;
using OperatorRunner;

namespace OperatorPackage.UnityIntegration
{
    public sealed class WorkflowPointRecord
    {
        public int Index { get; set; }
        public int OriginalPointIndex { get; set; }
        public int SourceRowIndex { get; set; }
        public string RowId { get; set; } = string.Empty;
        public string Role { get; set; } = "Generic";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Time { get; set; }
        public float ColorValue { get; set; }
        public float SizeValue { get; set; }
        public bool IsSelected { get; set; }
    }

    public sealed class WorkflowLinkRecord
    {
        public int Index { get; set; }
        public int OriginIndex { get; set; }
        public int DestinationIndex { get; set; }
        public string OriginRowId { get; set; } = string.Empty;
        public string DestinationRowId { get; set; } = string.Empty;
        public float Weight { get; set; }
    }

    public sealed class WorkflowPrimaryViewRecord
    {
        public string ViewName { get; set; } = string.Empty;
        public string ViewType { get; set; } = string.Empty;
        public string Role { get; set; } = "All";
        public string ProjectionKind { get; set; } = string.Empty;
        public int PointCount { get; set; }
        public int LinkCount { get; set; }
        public bool BackendBuilt { get; set; }
    }

    public sealed class WorkflowSelectionState
    {
        public List<string> SelectedRowIds { get; set; } = new List<string>();
        public int SelectedPointCount { get; set; }
        public int SpatialSelectedCount { get; set; }
        public int TemporalSelectedCount { get; set; }
        public int FinalSelectedCount { get; set; }
    }

    public sealed class WorkflowVisualizationSnapshot
    {
        public WorkflowPrimaryViewRecord PrimaryView { get; set; } = new WorkflowPrimaryViewRecord();
        public List<WorkflowPrimaryViewRecord> CoordinatedViews { get; set; } = new List<WorkflowPrimaryViewRecord>();
        public List<WorkflowPointRecord> Points { get; set; } = new List<WorkflowPointRecord>();
        public List<WorkflowLinkRecord> Links { get; set; } = new List<WorkflowLinkRecord>();
        public Dictionary<string, string> EncodingState { get; set; } = new Dictionary<string, string>();
        public WorkflowSelectionState SelectionState { get; set; } = new WorkflowSelectionState();
    }

    public static class WorkflowPayloadReader
    {
        public static WorkflowVisualizationSnapshot Read(RunnerResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            var snapshot = new WorkflowVisualizationSnapshot();
            if (response.VisualizationPayload == null)
                return snapshot;

            if (TryGetDictionary(response.VisualizationPayload, "primaryView", out var primaryView))
                snapshot.PrimaryView = ParseView(primaryView);

            if (TryGetList(response.VisualizationPayload, "coordinatedViews", out var coordinatedViews))
            {
                foreach (var item in coordinatedViews)
                {
                    if (item is Dictionary<string, object> viewDict)
                        snapshot.CoordinatedViews.Add(ParseView(viewDict));
                }
            }

            if (TryGetList(response.VisualizationPayload, "points", out var points))
            {
                foreach (var item in points)
                {
                    if (item is Dictionary<string, object> pointDict)
                        snapshot.Points.Add(ParsePoint(pointDict));
                }
            }

            if (TryGetList(response.VisualizationPayload, "links", out var links))
            {
                foreach (var item in links)
                {
                    if (item is Dictionary<string, object> linkDict)
                        snapshot.Links.Add(ParseLink(linkDict));
                }
            }

            if (TryGetDictionary(response.VisualizationPayload, "encodingState", out var encodingState))
            {
                foreach (var pair in encodingState)
                    snapshot.EncodingState[pair.Key] = pair.Value?.ToString() ?? string.Empty;
            }

            if (TryGetDictionary(response.VisualizationPayload, "selectionState", out var selectionState))
                snapshot.SelectionState = ParseSelectionState(selectionState);

            return snapshot;
        }

        private static WorkflowPrimaryViewRecord ParseView(Dictionary<string, object> data)
        {
            return new WorkflowPrimaryViewRecord
            {
                ViewName = GetString(data, "viewName"),
                ViewType = GetString(data, "viewType"),
                Role = GetString(data, "role", "All"),
                ProjectionKind = GetString(data, "projectionKind"),
                PointCount = GetInt(data, "pointCount"),
                LinkCount = GetInt(data, "linkCount"),
                BackendBuilt = GetBool(data, "backendBuilt")
            };
        }

        private static WorkflowPointRecord ParsePoint(Dictionary<string, object> data)
        {
            return new WorkflowPointRecord
            {
                Index = GetInt(data, "index"),
                OriginalPointIndex = GetInt(data, "originalPointIndex"),
                SourceRowIndex = GetInt(data, "sourceRowIndex"),
                RowId = GetString(data, "rowId"),
                Role = GetString(data, "role", "Generic"),
                X = GetFloat(data, "x"),
                Y = GetFloat(data, "y"),
                Z = GetFloat(data, "z"),
                Time = GetFloat(data, "time"),
                ColorValue = GetFloat(data, "colorValue"),
                SizeValue = GetFloat(data, "sizeValue"),
                IsSelected = GetBool(data, "isSelected")
            };
        }

        private static WorkflowLinkRecord ParseLink(Dictionary<string, object> data)
        {
            return new WorkflowLinkRecord
            {
                Index = GetInt(data, "index"),
                OriginIndex = GetInt(data, "originIndex"),
                DestinationIndex = GetInt(data, "destinationIndex"),
                OriginRowId = GetString(data, "originRowId"),
                DestinationRowId = GetString(data, "destinationRowId"),
                Weight = GetFloat(data, "weight")
            };
        }

        private static WorkflowSelectionState ParseSelectionState(Dictionary<string, object> data)
        {
            var result = new WorkflowSelectionState
            {
                SelectedPointCount = GetInt(data, "selectedPointCount"),
                SpatialSelectedCount = GetInt(data, "spatialSelectedCount"),
                TemporalSelectedCount = GetInt(data, "temporalSelectedCount"),
                FinalSelectedCount = GetInt(data, "finalSelectedCount")
            };

            if (data.TryGetValue("selectedRowIds", out var selectedRowIds) &&
                selectedRowIds is IEnumerable<object> selectedItems)
            {
                foreach (var item in selectedItems)
                {
                    var value = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        result.SelectedRowIds.Add(value);
                }
            }

            return result;
        }

        private static bool TryGetDictionary(Dictionary<string, object> payload, string key, out Dictionary<string, object> result)
        {
            if (payload.TryGetValue(key, out var value) && value is Dictionary<string, object> dictionary)
            {
                result = dictionary;
                return true;
            }

            result = null!;
            return false;
        }

        private static bool TryGetList(Dictionary<string, object> payload, string key, out List<object> result)
        {
            if (payload.TryGetValue(key, out var value) && value is List<object> list)
            {
                result = list;
                return true;
            }

            result = null!;
            return false;
        }

        private static string GetString(Dictionary<string, object> data, string key, string fallback = "")
        {
            return data.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
        }

        private static int GetInt(Dictionary<string, object> data, string key, int fallback = 0)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return fallback;

            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                float floatValue => (int)floatValue,
                double doubleValue => (int)doubleValue,
                _ when int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => fallback
            };
        }

        private static float GetFloat(Dictionary<string, object> data, string key, float fallback = 0f)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return fallback;

            return value switch
            {
                float floatValue => floatValue,
                double doubleValue => (float)doubleValue,
                int intValue => intValue,
                long longValue => longValue,
                _ when float.TryParse(value.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => fallback
            };
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool fallback = false)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return fallback;

            return value switch
            {
                bool boolValue => boolValue,
                _ when bool.TryParse(value.ToString(), out var parsed) => parsed,
                _ => fallback
            };
        }
    }
}
