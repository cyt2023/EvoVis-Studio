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
