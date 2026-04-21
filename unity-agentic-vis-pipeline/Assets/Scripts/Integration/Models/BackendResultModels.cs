using System;

namespace ImmersiveTaxiVis.Integration.Models
{
    [Serializable]
    public class BackendResultRoot
    {
        public BackendMeta meta;
        public BackendTask task;
        public BackendWorkflow selectedWorkflow;
        public BackendVisualizationPayload visualizationPayload;
        public BackendResultSummary resultSummary;
    }

    [Serializable]
    public class BackendMeta
    {
        public string schemaVersion;
    }

    [Serializable]
    public class BackendTask
    {
        public string rawTaskText;
    }

    [Serializable]
    public class BackendWorkflow
    {
        public BackendOperator[] operators;
        public BackendWorkflowScores scores;
    }

    [Serializable]
    public class BackendOperator
    {
        public string name;
    }

    [Serializable]
    public class BackendWorkflowScores
    {
        public float executionScore;
        public float llmScore;
        public float fitness;
    }

    [Serializable]
    public class BackendVisualizationPayload
    {
        public BackendViewDefinition[] views;
    }

    [Serializable]
    public class BackendViewDefinition
    {
        public string viewType;
        public string viewName;
        public string projectionPlane;
        public bool visible = true;
        public bool includeLinks = true;
        public float pointSizeScale = 1f;
        public BackendPointDefinition[] points;
        public BackendLinkDefinition[] links;
        public BackendEncodingState encodingState;
    }

    [Serializable]
    public class BackendPointDefinition
    {
        public string id;
        public float x;
        public float y;
        public float z;
        public float time;
        public bool isSelected;
    }

    [Serializable]
    public class BackendLinkDefinition
    {
        public int originIndex;
        public int destinationIndex;
    }

    [Serializable]
    public class BackendEncodingState
    {
        public int selectedCount;
        public string highlightMode;
    }

    [Serializable]
    public class BackendResultSummary
    {
        public string[] selectedRowIds;
        public int selectedPointCount;
        public bool backendBuilt;
    }

    [Serializable]
    public class BackendErrorEnvelope
    {
        public string status;
        public BackendErrorBody error;
    }

    [Serializable]
    public class BackendErrorBody
    {
        public string stage;
        public string message;
        public string details;
    }
}
