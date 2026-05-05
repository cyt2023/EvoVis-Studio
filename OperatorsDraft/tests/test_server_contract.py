import unittest
from pathlib import Path

import server
from scripts.validate_render_contract import validate_render_contract


ROOT = Path(__file__).resolve().parent.parent


class ServerContractTests(unittest.TestCase):
    def test_datasets_are_listed_with_relative_paths(self):
        payload = server.list_datasets()

        self.assertEqual(payload["status"], "success")
        self.assertGreaterEqual(len(payload["datasets"]), 1)
        for dataset in payload["datasets"]:
            self.assertIn("id", dataset)
            self.assertIn("relativePath", dataset)
            self.assertFalse(Path(dataset["relativePath"]).is_absolute())

    def test_render_contract_supports_summary_payload(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            summary_only=True,
            include_selected_ids=False,
        )

        validate_render_contract(render_payload)
        view = render_payload["visualizationPayload"]["views"][0]
        self.assertEqual(view["points"], [])
        self.assertEqual(render_payload["resultSummary"]["selectedRowIds"], [])
        self.assertTrue(render_payload["resultSummary"]["truncated"])

    def test_render_contract_supports_point_limit(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            point_limit=3,
            include_selected_ids=False,
        )

        validate_render_contract(render_payload)
        view = render_payload["visualizationPayload"]["views"][0]
        self.assertLessEqual(len(view["points"]), 3)
        self.assertEqual(render_payload["resultSummary"]["returnedPointCount"], len(view["points"]))

    def test_auto_view_type_preserves_evoflow_view(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        export_payload["visualization"]["renderPlan"]["primaryView"]["type"] = "STC"
        export_payload["resultSummary"]["viewType"] = "STC"

        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            summary_only=True,
            requested_view_type="Auto",
        )

        view = render_payload["visualizationPayload"]["views"][0]
        self.assertEqual(view["viewType"], "STC")

    def test_explicit_view_type_can_override_evoflow_view(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        export_payload["visualization"]["renderPlan"]["primaryView"]["type"] = "STC"

        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            summary_only=True,
            requested_view_type="Point",
        )

        view = render_payload["visualizationPayload"]["views"][0]
        self.assertEqual(view["viewType"], "Point")

    def test_projection2d_geometry_is_flat(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        export_payload["visualization"]["renderPlan"]["primaryView"]["type"] = "Projection2D"

        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            point_limit=10,
        )

        view = render_payload["visualizationPayload"]["views"][0]
        self.assertEqual(view["viewType"], "Projection2D")
        self.assertTrue(all(point["z"] == 0.0 for point in view["points"]))

    def test_stc_geometry_has_normalized_height(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        export_payload["visualization"]["renderPlan"]["primaryView"]["type"] = "STC"

        render_payload = server.adapt_to_unity_backend_result(
            export_payload,
            point_limit=10,
        )

        view = render_payload["visualizationPayload"]["views"][0]
        z_values = [point["z"] for point in view["points"]]
        self.assertEqual(view["viewType"], "STC")
        self.assertGreaterEqual(min(z_values), 0.0)
        self.assertLessEqual(max(z_values), 1.0)

    def test_link_indices_use_export_point_ids(self):
        export_payload = server.load_json(ROOT / "exports" / "test3.json")
        export_payload["visualization"]["renderPlan"]["primaryView"]["type"] = "Link"
        export_payload["visualization"]["renderPlan"]["geometry"]["points"] = [
            {"pointId": 0, "position": {"x": 0.1, "y": 0.2, "z": 0.0}},
            {"pointId": 1, "position": {"x": 0.3, "y": 0.4, "z": 0.0}},
        ]
        export_payload["visualization"]["renderPlan"]["geometry"]["links"] = [
            {"originPointId": 0, "destinationPointId": 1},
        ]

        render_payload = server.adapt_to_unity_backend_result(export_payload)

        view = render_payload["visualizationPayload"]["views"][0]
        self.assertEqual(view["viewType"], "Link")
        self.assertEqual(view["links"], [{"originIndex": 0, "destinationIndex": 1}])

    def test_unity_geometry_normalizes_mixed_xy_coordinates(self):
        points = [
            {"x": 0.5, "y": 0.6, "z": 0.0},
            {"x": -73.9, "y": 40.7, "z": 0.0},
        ]

        server.normalize_unity_point_geometry(points, "Link")

        for point in points:
            self.assertGreaterEqual(point["x"], 0.0)
            self.assertLessEqual(point["x"], 1.0)
            self.assertGreaterEqual(point["y"], 0.0)
            self.assertLessEqual(point["y"], 1.0)

    def test_render_query_options_parse_common_flags(self):
        options = server.render_options_from_query(
            {
                "limit": ["25"],
                "includeSelectedIds": ["false"],
                "includeLinks": ["0"],
                "summary": ["yes"],
            }
        )

        self.assertEqual(options["point_limit"], 25)
        self.assertFalse(options["include_selected_ids"])
        self.assertFalse(options["include_links"])
        self.assertTrue(options["summary_only"])


if __name__ == "__main__":
    unittest.main()
