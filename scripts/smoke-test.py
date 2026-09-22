#!/usr/bin/env python3
"""HTTP smoke test. Use ONLY against a disposable database: changes balances."""
import http.cookiejar
import os
from pathlib import Path
import re
import sys
import urllib.parse
import urllib.request
import urllib.error

base = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080"
password = Path(os.environ["AdminPasswordFile"]).read_text().rstrip("\r\n")
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def get(path):
    return client.open(base + path)


def post(path, values):
    page = get(path).read().decode()
    token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)[1]
    values["__RequestVerificationToken"] = token
    return client.open(base + path, urllib.parse.urlencode(values).encode())


assert get("/health").status == 200
for path in ["/accounts", "/transfers/new", "/incidents"]:
    assert "/login" in get(path).url
assert "Invalid username or password" in post("/login", {"Username": "admin", "Password": "wrong"}).read().decode()
assert post("/login", {"Username": "admin", "Password": password}).url.endswith("/accounts")
accounts = get("/accounts").read().decode()
def account_rows():
    return re.findall(r"<tr><td>.*?</tr>", get("/accounts").read().decode())
original_rows = account_rows()
assert all(name in accounts for name in ["Alice Johnson", "Bob Smith", "Demo Company"])
try:
    client.open(base + "/transfers/new", b"SourceAccountId=1&DestinationAccountId=2&Amount=1")
    raise AssertionError("Missing antiforgery token accepted")
except urllib.error.HTTPError as error:
    assert error.code == 400

assert "Insufficient funds" in post("/transfers/new", {
    "SourceAccountId": 1, "DestinationAccountId": 2, "Amount": "10000"
}).read().decode()
assert account_rows() == original_rows
assert "Insufficient funds" in get("/incidents").read().decode()
for source, destination, amount, message in [
    (1, 1, "1", "must be different"),
    (999, 2, "1", "Invalid source"),
    (1, 999, "1", "Invalid destination"),
    (1, 2, "0", "greater than zero"),
    (1, 2, "1.001", "two decimal places"),
]:
    assert message in post("/transfers/new", {
        "SourceAccountId": source, "DestinationAccountId": destination, "Amount": amount
    }).read().decode()
assert account_rows() == original_rows
assert "Transfer completed" in post("/transfers/new", {
    "SourceAccountId": 1, "DestinationAccountId": 2, "Amount": "100"
}).read().decode()
assert "4,900.00" in get("/accounts").read().decode()
assert "2,600.00" in get("/accounts").read().decode()
page = get("/accounts").read().decode()
token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)[1]
response = client.open(base + "/logout", urllib.parse.urlencode({"__RequestVerificationToken": token}).encode())
assert response.url.endswith("/login")
assert "/login" in get("/accounts").url
print("PASS: health, authentication, CSRF, validation, atomic rejection, incidents, successful transfer, logout")
