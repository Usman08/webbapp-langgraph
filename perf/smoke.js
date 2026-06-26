/**
 * T081: k6 smoke-test performance script.
 *
 * Tests the critical API path: POST /api/invoices/requests, then GET the run.
 * Run with: k6 run perf/smoke.js
 * Target: p95 < 3s, error rate < 1% at 5 concurrent users.
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { Trend, Rate } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5261";

export const options = {
  vus: 5,
  duration: "30s",
  thresholds: {
    http_req_duration: ["p(95)<3000"],   // 95th percentile < 3 s
    http_req_failed: ["rate<0.01"],      // error rate < 1%
    invoice_submit_duration: ["p(95)<5000"],
  },
};

const invoiceSubmitDuration = new Trend("invoice_submit_duration");
const invoiceErrorRate = new Rate("invoice_error_rate");

export default function () {
  // POST /api/invoices/requests
  const submitStart = Date.now();
  const submitResp = http.post(
    `${BASE_URL}/api/invoices/requests`,
    JSON.stringify({ requestText: "Invoice for ABC Traders, 10 units of SKU-001, apply 5% discount." }),
    { headers: { "Content-Type": "application/json" } }
  );

  const submitOk = check(submitResp, {
    "submit: status 200 or 202": (r) => r.status === 200 || r.status === 202,
    "submit: has runId": (r) => {
      try {
        return JSON.parse(r.body).runId != null;
      } catch {
        return false;
      }
    },
  });

  invoiceSubmitDuration.add(Date.now() - submitStart);
  invoiceErrorRate.add(!submitOk);

  if (!submitOk) {
    sleep(1);
    return;
  }

  const runId = JSON.parse(submitResp.body).runId;

  // GET /api/invoices/requests/{runId} — poll status (non-streaming health check)
  const getResp = http.get(`${BASE_URL}/api/invoices/requests/${runId}`, {
    headers: { Accept: "application/json" },
  });

  check(getResp, {
    "get run: status 200": (r) => r.status === 200,
    "get run: has status field": (r) => {
      try {
        return JSON.parse(r.body).status != null;
      } catch {
        return false;
      }
    },
  });

  // GET /api/invoices — list endpoint
  const listResp = http.get(`${BASE_URL}/api/invoices`);
  check(listResp, {
    "list invoices: status 200": (r) => r.status === 200,
    "list invoices: is array": (r) => {
      try {
        return Array.isArray(JSON.parse(r.body));
      } catch {
        return false;
      }
    },
  });

  sleep(1);
}
