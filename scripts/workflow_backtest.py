#!/usr/bin/env python3
"""LGB workflow API backtests — exercises MOI/MOA handoffs, gates, and edge cases."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from typing import Any, Callable

API_BASE = os.environ.get("LGB_API_BASE", "http://localhost:5003").rstrip("/")
PASSWORD = os.environ.get("LGB_DEV_PASSWORD", "password123")
BACKEND_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "LGBApp.Backend"))
BACKTEST: dict[str, Any] = {}

USERS = {
    "sharon": "sharon@lgb.test",
    "siti": "siti@lgb.test",
    "clientadmin": "clientadmin@acme.test",
    "ryan1": "ryan1@lgb.test",
    "ryan2": "ryan2@lgb.test",
    "ryan3": "ryan3@lgb.test",
    "dra2": "dra2@lgb.test",
    "dra3": "dra3@lgb.test",
}

COMPLETE_MOA_PACK = {
    "checklist": {
        "internalChecklistA": True,
        "internalChecklistB": True,
        "cleanAgreementAttached": True,
        "shareholdingTableAttached": False,
        "ssmRegistrationNo": "BT-123456-A",
        "ssmNewRegistrationNo": "",
        "ssmEntityType": "Sdn Bhd",
        "ssmStatus": "Active",
        "ssmAsAtDate": "2026-06-01",
    },
    "financeRelated": False,
    "bankSignatoryMatter": False,
    "shareMovement": False,
}


@dataclass
class ApiClient:
    email: str
    token: str = ""
    user: dict[str, Any] = field(default_factory=dict)

    def login(self) -> None:
        data = request_json("POST", "/api/auth/login", {"email": self.email, "password": PASSWORD})
        self.token = data["token"]
        self.user = data["user"]

    def headers(self) -> dict[str, str]:
        return {
            "Authorization": f"Bearer {self.token}",
            "Content-Type": "application/json",
        }

    def get(self, path: str) -> Any:
        return request_json("GET", path, headers=self.headers())

    def post(self, path: str, body: dict | None = None) -> tuple[int, Any]:
        return request_json_status("POST", path, body or {}, headers=self.headers())

    def put(self, path: str, body: dict) -> tuple[int, Any]:
        return request_json_status("PUT", path, body, headers=self.headers())

    def delete(self, path: str) -> tuple[int, Any]:
        return request_json_status("DELETE", path, None, headers=self.headers())


def request_json(method: str, path: str, body: dict | None = None, headers: dict | None = None) -> Any:
    status, data = request_json_status(method, path, body, headers)
    if status >= 400:
        raise RuntimeError(f"{method} {path} -> {status}: {data}")
    return data


def request_json_status(
    method: str,
    path: str,
    body: dict | None = None,
    headers: dict | None = None,
) -> tuple[int, Any]:
    url = f"{API_BASE}{path}"
    payload = None
    hdrs = dict(headers or {})
    if body is not None:
        payload = json.dumps(body).encode("utf-8")
        hdrs.setdefault("Content-Type", "application/json")
    req = urllib.request.Request(url, data=payload, headers=hdrs, method=method)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            raw = resp.read().decode("utf-8")
            return resp.status, parse_json(raw)
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8")
        return exc.code, parse_json(raw)


def parse_json(raw: str) -> Any:
    if not raw:
        return None
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return raw


@dataclass
class TestCase:
    name: str
    fn: Callable[[], None]
    tags: list[str] = field(default_factory=list)


class Runner:
    def __init__(self) -> None:
        self.tests: list[TestCase] = []
        self.passed = 0
        self.failed = 0
        self.skipped = 0
        self.failures: list[str] = []

    def add(self, name: str, fn: Callable[[], None], *tags: str) -> None:
        self.tests.append(TestCase(name, fn, list(tags)))

    def run_all(self) -> int:
        print(f"\n{'=' * 72}\nLGB Workflow Backtests @ {API_BASE}\n{'=' * 72}\n")
        for tc in self.tests:
            try:
                tc.fn()
                self.passed += 1
                print(f"  PASS  {tc.name}")
            except SkipTest as exc:
                self.skipped += 1
                print(f"  SKIP  {tc.name}: {exc}")
            except Exception as exc:  # noqa: BLE001
                self.failed += 1
                msg = f"{tc.name}: {exc}"
                self.failures.append(msg)
                print(f"  FAIL  {msg}")
        print(f"\n{'-' * 72}")
        print(f"Results: {self.passed} passed, {self.failed} failed, {self.skipped} skipped")
        if self.failures:
            print("\nFailures:")
            for f in self.failures:
                print(f"  - {f}")
        return 1 if self.failed else 0


class SkipTest(Exception):
    pass


def assert_eq(actual: Any, expected: Any, label: str = "") -> None:
    if actual != expected:
        raise AssertionError(f"{label} expected {expected!r}, got {actual!r}")


def assert_in(needle: Any, haystack: Any, label: str = "") -> None:
    if needle not in haystack:
        raise AssertionError(f"{label} expected {needle!r} in {haystack!r}")


def assert_true(value: bool, label: str = "") -> None:
    if not value:
        raise AssertionError(label or "expected truthy value")


def assert_status(code: int, expected: int | set[int], body: Any, label: str = "") -> None:
    allowed = {expected} if isinstance(expected, int) else expected
    if code not in allowed:
        raise AssertionError(f"{label} expected HTTP {allowed}, got {code}: {body}")


def login(key: str) -> ApiClient:
    client = ApiClient(USERS[key])
    client.login()
    return client


def slugify_company(name: str) -> str:
    return "".join(c for c in name.lower() if c.isalnum())[:24]


def wait_for_login(email: str, attempts: int = 8) -> None:
    last_error: Exception | None = None
    for _ in range(attempts):
        try:
            ApiClient(email).login()
            return
        except Exception as exc:  # noqa: BLE001
            last_error = exc
            time.sleep(0.5)
    raise RuntimeError(f"could not login as {email}") from last_error


def clear_customer_password_gate(customer_id: int) -> str:
    db_path = os.path.join(BACKEND_DIR, "lgbapp-dev.db")
    sql = (
        "UPDATE Users SET PasswordHash = (SELECT PasswordHash FROM Users WHERE Email = 'sharon@lgb.test' LIMIT 1), "
        f"MustChangePassword = 0 WHERE CustomerId = {customer_id};"
    )
    subprocess.run(["sqlite3", db_path, sql], capture_output=True, check=True, timeout=30)
    admin_email = subprocess.check_output(
        [
            "sqlite3",
            db_path,
            f"SELECT Email FROM Users WHERE CustomerId = {customer_id} AND Role = 'ClientAdmin' LIMIT 1;",
        ],
        text=True,
    ).strip()
    if not admin_email:
        raise RuntimeError(f"No ClientAdmin user for customer {customer_id}")
    return admin_email


def provision_backtest_customer() -> None:
    sharon = login("sharon")
    stamp = int(time.time())
    company = f"Workflow Backtest {stamp}"
    moi_email = f"btmoi{stamp}@lgb.test"
    appr_email = f"btappr{stamp}@lgb.test"
    moa_email = f"btmoa{stamp}@lgb.test"
    body = {
        "companyName": company,
        "contactName": "BT Moi Holder",
        "email": moi_email,
        "mobile": "0100000099",
        "packageName": "Enterprise Package",
        "packageValue": "6170",
        "validity": "1 Year",
        "cosec": True,
        "accountHolders": [
            {"id": 0, "name": "BT Moi Holder", "email": moi_email, "phone": "", "moi": True, "moiApproval": False, "moa": False},
            {"id": 0, "name": "BT Approver", "email": appr_email, "phone": "", "moi": False, "moiApproval": True, "moa": False},
            {"id": 0, "name": "BT Moa Holder", "email": moa_email, "phone": "", "moi": False, "moiApproval": False, "moa": True},
        ],
    }
    status, customer = sharon.post("/api/customers", body)
    assert_status(status, {200, 201}, customer, "provision backtest customer")

    admin_email = clear_customer_password_gate(customer["id"])
    wait_for_login(admin_email)
    wait_for_login(appr_email)
    wait_for_login(moa_email)

    BACKTEST.update(
        {
            "customer_id": customer["id"],
            "company": company,
            "admin_email": admin_email,
            "moi_email": moi_email,
            "appr_email": appr_email,
            "moa_email": moa_email,
        }
    )
    print(f"Backtest customer #{customer['id']} ({company})")


def backtest_admin() -> ApiClient:
    client = ApiClient(BACKTEST["admin_email"])
    client.login()
    return client


def backtest_approver() -> ApiClient:
    client = ApiClient(BACKTEST["appr_email"])
    client.login()
    return client


def backtest_moa_holder() -> ApiClient:
    client = ApiClient(BACKTEST["moa_email"])
    client.login()
    return client



def find_job(client: ApiClient, service_substr: str, customer_substr: str | None = None) -> dict:
    jobs = client.get("/api/jobrequests")
    for job in jobs:
        if job.get("id") in USED_JOB_IDS:
            continue
        if service_substr.lower() in (job.get("service") or "").lower():
            if customer_substr is None or customer_substr.lower() in (job.get("customer") or "").lower():
                return job
    raise AssertionError(f"job not found: {service_substr!r} ({customer_substr})")


def client_jobs_for_customer(client_email: str, customer_id: int | None = None) -> list[dict]:
    """Package lines visible to the client before MOI is sent to LGB."""
    client = ApiClient(client_email)
    client.login()
    jobs = client.get("/api/clientjobs/my-jobs")
    if customer_id is not None:
        jobs = [j for j in jobs if j.get("customerId") == customer_id]
    return jobs


def claim_job(job_id: int) -> None:
    USED_JOB_IDS.add(job_id)


USED_JOB_IDS: set[int] = set()


def find_backtest_job(service_substr: str) -> dict:
    sharon = login("sharon")
    jobs = sharon.get("/api/jobrequests")
    for job in jobs:
        if job.get("customerId") != BACKTEST.get("customer_id"):
            continue
        if job.get("id") in USED_JOB_IDS:
            continue
        if service_substr.lower() not in (job.get("service") or "").lower():
            continue
        if any(u.get("displayStatusKey") == "moi_not_received" for u in job.get("units", [])):
            return job
    raise SkipTest(f"no backtest job for {service_substr!r}")


def moi_for_job(client: ApiClient, job_id: int) -> dict:
    forms = client.get(f"/api/moiforms?jobId={job_id}")
    if not forms:
        raise AssertionError(f"no MOI for job {job_id}")
    return forms[0]


def moa_for_job(client: ApiClient, job_id: int, unit_number: int | None = None) -> dict:
    path = f"/api/moaforms/for-job/{job_id}"
    if unit_number is not None:
        path += f"?unitNumber={unit_number}"
    return client.get(path)


def first_open_unit(job: dict) -> dict:
    return next(u for u in job.get("units", []) if u.get("displayStatusKey") == "moi_not_received")


def submit_moi_through_client_phase(job_id: int, unit_number: int | None = None) -> dict:
    """Backtest customer: client admin issues/submits, approver signs -> ClientSubmitted."""
    claim_job(job_id)
    admin = backtest_admin()
    sharon = login("sharon")
    approver = backtest_approver()

    issue_body: dict[str, Any] = {}
    if unit_number is not None:
        issue_body["unitNumber"] = unit_number
    status, body = admin.post(f"/api/clientjobs/{job_id}/issue-moi", issue_body)
    assert_status(status, {200, 201}, body, "issue-moi")

    moi = moi_for_job(sharon, job_id)
    moi_id = moi["id"]

    status, body = admin.post(f"/api/moiforms/{moi_id}/submit-for-approval", {})
    assert_status(status, 200, body, "submit-for-approval")

    # If still pending client approval, Ryan2 signs
    moi = moi_for_job(sharon, job_id)
    if moi.get("workflowState") == "PendingClientMoiApproval":
        status, body = approver.post(
            f"/api/moiforms/{moi_id}/client-approve",
            {"comments": "backtest approve", "signatureDataUrl": "data:image/png;base64,AA=="},
        )
        assert_status(status, 200, body, "client-approve moi")

    job = sharon.get(f"/api/jobrequests/{job_id}")
    if unit_number is not None:
        unit = next(u for u in job["units"] if u.get("unitNumber") == unit_number)
    else:
        unit = job["units"][0]
    assert_eq(unit.get("displayStatusKey"), "awaiting_intake", "after client phase")
    if job.get("totalQty", 1) <= 1:
        assert_eq(job.get("internalHandoffStatus"), "ClientSubmitted", "job handoff")
    return job


def advance_moa_to_ready(sharon: ApiClient, siti: ApiClient, job_id: int, moa_id: int, unit_number: int | None = None) -> None:
    status, body = sharon.put(f"/api/moaforms/{moa_id}/pack", COMPLETE_MOA_PACK)
    assert_status(status, 200, body, "moa pack")

    handoff_body: dict[str, Any] = {"action": "submit-admin-review"}
    if unit_number is not None:
        handoff_body["unitNumber"] = unit_number
    status, body = siti.post(f"/api/jobrequests/{job_id}/handoff", handoff_body)
    assert_status(status, 200, body, "submit-admin-review")
    assert_eq(body.get("internalHandoffStatus"), "AdminReview", "handoff after submit")

    handoff_body = {"action": "sharon-approve-moa"}
    if unit_number is not None:
        handoff_body["unitNumber"] = unit_number
    status, body = sharon.post(f"/api/jobrequests/{job_id}/handoff", handoff_body)
    assert_status(status, 200, body, "sharon-approve-moa")

    handoff_body = {"action": "approve-for-moa"}
    if unit_number is not None:
        handoff_body["unitNumber"] = unit_number
    status, body = sharon.post(f"/api/jobrequests/{job_id}/handoff", handoff_body)
    assert_status(status, 200, body, "approve-for-moa")


def register_tests(runner: Runner) -> None:
    runner.add("API reachable", test_api_reachable, "smoke")
    runner.add("Auth: all seeded users login", test_all_users_login, "auth")
    runner.add("Auth: bad password rejected", test_bad_password, "auth")
    runner.add("Auth: no token -> 401", test_unauthorized, "auth")

    runner.add("Gate: sec assignment blocked before Sharon sign-off", test_sec_gate_before_signoff, "gate", "moi")
    runner.add("Gate: sec assignment blocked at ClientSubmitted", test_sec_gate_at_client_submitted, "gate")
    runner.add("Gate: approve-intake blocked before client phase", test_intake_before_client, "gate")
    runner.add("Gate: MOA pack incomplete rejected", test_moa_pack_incomplete, "gate", "moa")
    runner.add("Gate: MOA client sign blocked before release", test_moa_sign_before_release, "gate", "moa")
    runner.add("Gate: execution progress not reachable after MOA complete", test_no_execution_phase, "execution")

    runner.add("MOI reject path resets workflow", test_moi_reject_path, "moi", "negative")
    runner.add("MOA Sharon reject returns to prep", test_moa_sharon_reject, "moa", "negative")

    runner.add("Full happy path: MOI -> MOA -> Completed (no execution)", test_full_happy_path, "e2e", "critical")
    runner.add("Partial MOA signature stays in circulation", test_partial_moa_signature, "moa", "edge")
    runner.add("Sharon sign-off -> resolution prep + MOA provisioned", test_sharon_signoff_status, "moi", "critical")
    runner.add("Recommend does not skip to MOA without pack", test_recommend_without_early_moa, "moi")

    runner.add("Customer: add-ons only creation", test_addons_only_customer, "customer")
    runner.add("Customer: package fee override", test_fee_override_customer, "customer")
    runner.add("Customer: MOA workflow template override", test_moa_template_override, "customer")

    runner.add("Multi-session: unit 2 blocked until unit 1 MOI path", test_multi_session_gate, "multi", "edge")
    runner.add("Job list: completed unit shows completed status", test_completed_unit_display, "multi")


def test_api_reachable() -> None:
    status, _ = request_json_status("GET", "/api/auth/login")
    assert_true(status in (200, 405), "auth endpoint should exist")


def test_all_users_login() -> None:
    for key, email in USERS.items():
        data = request_json("POST", "/api/auth/login", {"email": email, "password": PASSWORD})
        assert_true(data.get("token"), f"login failed for {email} ({key})")
        assert_true(data.get("user", {}).get("email") == email)


def test_bad_password() -> None:
    status, body = request_json_status(
        "POST",
        "/api/auth/login",
        {"email": USERS["sharon"], "password": "wrong-password"},
    )
    assert_status(status, {400, 401}, body)


def test_unauthorized() -> None:
    status, _ = request_json_status("GET", "/api/jobrequests")
    assert_status(status, 401, None)


def test_sec_gate_before_signoff() -> None:
    sharon = login("sharon")
    job = find_backtest_job("BO Declaration")
    job_id = job["id"]
    submit_moi_through_client_phase(job_id)

    status, body = sharon.post(f"/api/jobrequests/{job_id}/assign-secretarial-team")
    assert_status(status, 400, body, "assign before sign-off")
    assert_in("signed off", str(body).lower(), "error message")


def test_sec_gate_at_client_submitted() -> None:
    sharon = login("sharon")
    job = find_backtest_job("Annual Return")
    job_id = job["id"]
    submit_moi_through_client_phase(job_id)
    assert_eq(sharon.get(f"/api/jobrequests/{job_id}").get("internalHandoffStatus"), "ClientSubmitted")

    status, body = sharon.post(f"/api/jobrequests/{job_id}/assign-secretarial-team")
    assert_status(status, 400, body)


def test_intake_before_client() -> None:
    sharon = login("sharon")
    job = find_backtest_job("Prov of register Office")
    job_id = job["id"]
    claim_job(job_id)
    admin = backtest_admin()
    status, _ = admin.post(f"/api/clientjobs/{job_id}/issue-moi", {})
    assert_status(status, {200, 201}, _)

    status, body = sharon.post(f"/api/jobrequests/{job_id}/approve-intake")
    assert_status(status, 400, body)


def test_moa_pack_incomplete() -> None:
    sharon = login("sharon")
    siti = login("siti")
    job = find_backtest_job("AR Filing to MBRS")
    job_id = job["id"]
    run_through_sec_assignment(job_id)

    moa = moa_for_job(sharon, job_id)
    status, body = siti.post(
        f"/api/jobrequests/{job_id}/handoff",
        {"action": "submit-admin-review"},
    )
    assert_status(status, 400, body, "incomplete pack submit")


def test_moa_sign_before_release() -> None:
    sharon = login("sharon")
    siti = login("siti")
    moa_holder = backtest_moa_holder()
    job = find_backtest_job("Resolution on annual audited account filing")
    job_id = job["id"]
    run_through_sec_assignment(job_id)
    moa = moa_for_job(sharon, job_id)
    siti.put(f"/api/moaforms/{moa['id']}/pack", COMPLETE_MOA_PACK)

    status, body = moa_holder.post(
        f"/api/moaforms/{moa['id']}/client-approve",
        {"comments": "early", "signatureDataUrl": "data:image/png;base64,AA=="},
    )
    assert_status(status, 400, body)


def test_partial_moa_signature() -> None:
    sharon = login("sharon")
    stamp = int(time.time())
    body = {
        "companyName": f"Partial MOA {stamp}",
        "contactName": "Moa A",
        "email": f"moaa{stamp}@lgb.test",
        "mobile": "0100000088",
        "packageName": "Enterprise Package",
        "packageValue": "1000",
        "validity": "1 Year",
        "cosec": True,
        "accountHolders": [
            {"id": 0, "name": "Moa A", "email": f"moaa{stamp}@lgb.test", "phone": "", "moi": True, "moiApproval": True, "moa": True},
            {"id": 0, "name": "Moa B", "email": f"moab{stamp}@lgb.test", "phone": "", "moi": False, "moiApproval": False, "moa": True},
        ],
    }
    status, customer = sharon.post("/api/customers", body)
    assert_status(status, {200, 201}, customer)
    admin_email = clear_customer_password_gate(customer["id"])
    wait_for_login(admin_email)
    wait_for_login(f"moaa{stamp}@lgb.test")

    jobs = client_jobs_for_customer(admin_email, customer["id"])
    job = next(j for j in jobs if j.get("totalQty") == 1 and "Secretarial" in j.get("service", ""))
    job_id = job["id"]
    claim_job(job_id)

    admin = ApiClient(admin_email)
    admin.login()
    moa_a = ApiClient(f"moaa{stamp}@lgb.test")
    moa_a.login()
    siti = login("siti")

    admin.post(f"/api/clientjobs/{job_id}/issue-moi", {})
    moi = moi_for_job(sharon, job_id)
    admin.post(f"/api/moiforms/{moi['id']}/submit-for-approval", {})
    sharon.post(f"/api/jobrequests/{job_id}/approve-intake")
    sharon.post(f"/api/jobrequests/{job_id}/assign-secretarial-team")
    moi = moi_for_job(sharon, job_id)
    sharon.post(f"/api/moiforms/{moi['id']}/recommend", {"comments": "partial test"})
    moa = moa_for_job(sharon, job_id)
    advance_moa_to_ready(sharon, sharon, job_id, moa["id"])

    status, body = moa_a.post(
        f"/api/moaforms/{moa['id']}/client-approve",
        {"comments": "one of two", "signatureDataUrl": "data:image/png;base64,AA=="},
    )
    assert_status(status, 200, body)
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("internalHandoffStatus"), "MoaCirculation")
    assert_true(job.get("status") != "Completed")


def test_no_execution_phase() -> None:
    sharon = login("sharon")
    jobs = sharon.get("/api/jobrequests")
    completed = [
        j for j in jobs
        if j.get("status") == "Completed"
        or any(u.get("internalHandoffStatus") == "Completed" for u in j.get("units", []))
    ]
    if not completed:
        raise SkipTest("no completed jobs/units in DB to verify execution removal")
    for job in completed:
        assert_true(
            job.get("internalHandoffStatus") != "PendingExecute",
            f"job {job['id']} should not be PendingExecute",
        )
        for unit in job.get("units", []):
            assert_true(
                unit.get("internalHandoffStatus") != "PendingExecute",
                f"unit {unit.get('unitNumber')} PendingExecute",
            )


def test_moi_reject_path() -> None:
    sharon = login("sharon")
    admin = backtest_admin()
    approver = backtest_approver()
    job = find_backtest_job("Submission of annual audited account MBRS zip file")
    job_id = job["id"]
    claim_job(job_id)
    admin.post(f"/api/clientjobs/{job_id}/issue-moi", {})
    moi = moi_for_job(sharon, job_id)
    status, body = admin.post(f"/api/moiforms/{moi['id']}/submit-for-approval", {})
    assert_status(status, 200, body, "submit-for-approval")
    status, body = approver.post(
        f"/api/moiforms/{moi['id']}/client-reject",
        {"reason": "backtest reject"},
    )
    assert_status(status, 200, body)
    moi = moi_for_job(sharon, job_id)
    assert_eq(moi.get("workflowState"), "MoiRejected")


def test_moa_sharon_reject() -> None:
    sharon = login("sharon")
    siti = login("siti")
    job = find_backtest_job("Assisting Auditor on statutory Audit")
    job_id = job["id"]
    run_through_sec_assignment(job_id)
    moa = moa_for_job(sharon, job_id)
    status, body = sharon.put(f"/api/moaforms/{moa['id']}/pack", COMPLETE_MOA_PACK)
    assert_status(status, 200, body, "moa pack")
    status, body = siti.post(f"/api/jobrequests/{job_id}/handoff", {"action": "submit-admin-review"})
    assert_status(status, 200, body, "submit-admin-review")
    assert_eq(body.get("internalHandoffStatus"), "AdminReview")

    status, body = sharon.post(
        f"/api/jobrequests/{job_id}/handoff",
        {"action": "reject-moa", "comments": "backtest sharon reject"},
    )
    assert_status(status, 200, body)
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_in(job.get("internalHandoffStatus"), {"PendingPrep", "ResoInProgress"}, "after reject")


def run_through_sec_assignment(job_id: int, unit_number: int | None = None) -> dict:
    submit_moi_through_client_phase(job_id, unit_number=unit_number)
    sharon = login("sharon")
    siti = login("siti")

    intake_path = f"/api/jobrequests/{job_id}/approve-intake"
    if unit_number is not None:
        intake_path += f"?unitNumber={unit_number}"
    status, body = sharon.post(intake_path)
    assert_status(status, 200, body, "approve-intake")
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("internalHandoffStatus"), "PendingPrep", "after sign-off")

    status, body = sharon.post(f"/api/jobrequests/{job_id}/assign-secretarial-team")
    assert_status(status, 200, body, "assign-secretarial-team")
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("internalHandoffStatus"), "PendingPrep", "after sec assign")

    moi = moi_for_job(sharon, job_id)
    status, body = sharon.post(f"/api/moiforms/{moi['id']}/recommend", {"comments": "backtest recommend"})
    assert_status(status, 200, body, "recommend")
    moi = moi_for_job(sharon, job_id)
    assert_eq(moi.get("workflowState"), "PendingRecommendation")
    return job


def test_full_happy_path() -> None:
    sharon = login("sharon")
    siti = login("siti")
    moa_holder = backtest_moa_holder()
    job = find_backtest_job("Secretarial record Checks")
    job_id = job["id"]
    run_through_sec_assignment(job_id)

    moa = moa_for_job(sharon, job_id)
    advance_moa_to_ready(sharon, siti, job_id, moa["id"])

    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("internalHandoffStatus"), "ReadyForMoa", "ready for client")

    status, body = moa_holder.post(
        f"/api/moaforms/{moa['id']}/client-approve",
        {"comments": "final", "signatureDataUrl": "data:image/png;base64,AA=="},
    )
    assert_status(status, 200, body, "moa client sign")

    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("status"), "In Progress")
    assert_eq(job.get("internalHandoffStatus"), "PendingExecute")

    status, body = sharon.post(
        f"/api/jobrequests/{job_id}/progress",
        {"unitNumber": 1, "markUnitComplete": True},
    )
    assert_status(status, 200, body, "complete execution")
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_eq(job.get("status"), "Completed")
    assert_eq(job.get("internalHandoffStatus"), "Completed")


def test_sharon_signoff_status() -> None:
    sharon = login("sharon")
    job = find_backtest_job("Overseas Support Service")
    unit = first_open_unit(job)
    job_id = job["id"]
    submit_moi_through_client_phase(job_id, unit_number=unit.get("unitNumber"))
    status, _ = sharon.post(f"/api/jobrequests/{job_id}/approve-intake?unitNumber={unit.get('unitNumber')}")
    assert_status(status, 200, _)
    job = sharon.get(f"/api/jobrequests/{job_id}")
    unit_row = next(u for u in job["units"] if u.get("unitNumber") == unit.get("unitNumber"))
    assert_eq(unit_row.get("displayStatusKey"), "resolution_prep")
    assert_true(unit_row.get("hasMoaForm") or job.get("hasMoaForm"), "MOA should be provisioned on sign-off")


def test_recommend_without_early_moa() -> None:
    sharon = login("sharon")
    job = find_backtest_job("Prepare Resolution")
    unit = first_open_unit(job)
    job_id = job["id"]
    run_through_sec_assignment(job_id, unit_number=unit.get("unitNumber"))
    # MOA may be provisioned on assign, but should not be client-ready
    job = sharon.get(f"/api/jobrequests/{job_id}")
    assert_true(job.get("internalHandoffStatus") != "ReadyForMoa")
    assert_true(job.get("internalHandoffStatus") != "Completed")


def test_addons_only_customer() -> None:
    sharon = login("sharon")
    stamp = int(time.time())
    body = {
        "companyName": f"Addons Only {stamp}",
        "contactName": "Add On User",
        "email": f"addons-{stamp}@test.local",
        "mobile": "0100000002",
        "packageName": "Add-ons only",
        "packageValue": "240",
        "validity": "1 Year",
        "cosec": False,
        "packages": [
            {
                "packageName": "Add-ons only",
                "packageValue": "240",
                "validity": "1 Year",
                "pricingJson": json.dumps({
                    "validity": "1 Year",
                    "basePackagePrice": 0,
                    "addOnLines": [{"name": "Overseas Support Service", "qty": 2, "unitPrice": 120}],
                }),
            }
        ],
        "accountHolders": [
            {"id": 0, "name": "Add On User", "email": f"addons-{stamp}@test.local", "phone": "", "moi": True, "moiApproval": False, "moa": False},
        ],
    }
    status, customer = sharon.post("/api/customers", body)
    assert_status(status, {200, 201}, customer)
    assert_eq(customer.get("package"), "Add-ons only")
    assert_eq(customer.get("cosec"), False)

    admin_email = clear_customer_password_gate(customer["id"])
    jobs = client_jobs_for_customer(admin_email, customer["id"])
    assert_true(len(jobs) > 0, "add-ons customer should sync jobs")
    assert_true(all(j.get("taskType") == "Service" for j in jobs))


def test_fee_override_customer() -> None:
    sharon = login("sharon")
    stamp = int(time.time())
    body = {
        "companyName": f"Fee Override {stamp}",
        "contactName": "Fee User",
        "email": f"fee-{stamp}@test.local",
        "mobile": "0100000003",
        "packageName": "Enterprise Package",
        "packageValue": "9999",
        "validity": "1 Year",
        "cosec": True,
        "accountHolders": [
            {"id": 0, "name": "Fee User", "email": f"fee-{stamp}@test.local", "phone": "", "moi": True, "moiApproval": False, "moa": False},
        ],
    }
    status, customer = sharon.post("/api/customers", body)
    assert_status(status, {200, 201}, customer)
    pkg = customer.get("packages", [{}])[0]
    assert_eq(float(pkg.get("packageValue", 0)), 9999.0)


def test_moa_template_override() -> None:
    sharon = login("sharon")
    stamp = int(time.time())
    body = {
        "companyName": f"Template Override {stamp}",
        "contactName": "Tpl User",
        "email": f"tpl-{stamp}@test.local",
        "mobile": "0100000004",
        "packageName": "Enterprise Package",
        "packageValue": "5000",
        "validity": "1 Year",
        "cosec": True,
        "moaWorkflowTemplateCode": "parallel_holders",
        "accountHolders": [
            {"id": 0, "name": "Tpl User", "email": f"tpl-{stamp}@test.local", "phone": "", "moi": True, "moiApproval": False, "moa": True},
        ],
    }
    status, customer = sharon.post("/api/customers", body)
    assert_status(status, {200, 201}, customer)
    assert_eq(customer.get("moaWorkflowTemplateCode"), "parallel_holders")


def test_multi_session_gate() -> None:
    sharon = login("sharon")
    job = find_job(sharon, "Attend Board Meeting", "Ryan Trading")
    units = job.get("units", [])
    unit_numbers = {u.get("unitNumber") for u in units}
    assert_true(
        3 not in unit_numbers,
        "unreleased sessions stay hidden from internal admin until client sends MOI",
    )
    completed_units = [u for u in units if u.get("displayStatusKey") == "completed"]
    assert_true(len(completed_units) >= 1, "earlier released session should still be visible")


def test_completed_unit_display() -> None:
    sharon = login("sharon")
    job = find_job(sharon, "Attend Board Meeting", "DRa")
    unit1 = next(u for u in job.get("units", []) if u.get("unitNumber") == 1)
    if unit1.get("displayStatusKey") != "completed":
        raise SkipTest("DRa board meeting unit 1 not completed in this DB snapshot")
    assert_eq(unit1.get("internalHandoffStatus"), "Completed")
    assert_true(job.get("internalHandoffStatus") != "PendingExecute")


def main() -> int:
    provision_backtest_customer()
    runner = Runner()
    register_tests(runner)
    return runner.run_all()


if __name__ == "__main__":
    sys.exit(main())
