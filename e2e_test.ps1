$base = "https://localhost:54090"
$results = @()
$pass = 0; $fail = 0

function Test($name, $status, $expected, $body="") {
    $ok = $status -eq $expected
    $symbol = if($ok) {"[PASS]"} else {"[FAIL]"}
    $global:pass += if($ok) {1} else {0}
    $global:fail += if($ok) {0} else {1}
    $msg = "$symbol $name (HTTP $status)"
    if (-not $ok) { $msg += " - expected $expected | body: $($body.Substring(0,[Math]::Min(120,$body.Length)))" }
    Write-Host $msg -ForegroundColor $(if($ok){"Green"}else{"Red"})
    $global:results += $msg
}

# Ignore SSL
add-type @"
using System.Net;using System.Security.Cryptography.X509Certificates;
public class TrustAll:System.Net.Http.HttpClientHandler{
  protected override bool SendAsync_CheckCertificate(System.Net.Http.HttpRequestMessage r,X509Certificate2 c,X509Chain ch,System.Net.Security.SslPolicyErrors e)=>true;
}
"@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

function Invoke-API($method, $url, $body=$null, $token=$null) {
    $headers = @{"Content-Type"="application/json"}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    try {
        $params = @{Method=$method; Uri=$url; Headers=$headers; UseBasicParsing=$true}
        if ($body) { $params["Body"] = ($body | ConvertTo-Json -Depth 10) }
        $r = Invoke-WebRequest @params
        return @{status=$r.StatusCode; body=$r.Content}
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        $errBody = ""
        try { $errBody = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()).ReadToEnd() } catch {}
        return @{status=$code; body=$errBody}
    }
}

Write-Host "`n========== GatherUp E2E Workflow Test ==========" -ForegroundColor Cyan

# ── 1. Manager Login ─────────────────────────────────────────────────────────
Write-Host "`n[1] Manager Login" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/auth/login" @{email="michal.cohen.dev@gmail.com";password="123456"}
Test "Manager login" $r.status 200 $r.body
$managerToken = ($r.body | ConvertFrom-Json).token
$managerId    = ($r.body | ConvertFrom-Json).id

# ── 2. Get Events (verify existing data) ─────────────────────────────────────
Write-Host "`n[2] Get All Events" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/events"
Test "GET /api/events" $r.status 200 $r.body
$events = $r.body | ConvertFrom-Json
Write-Host "   Found $($events.Count) events"

# ── 3. Create Event ───────────────────────────────────────────────────────────
Write-Host "`n[3] Create Event" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/events" @{
    name="E2E Test Event"; date="2025-12-15T10:00:00Z"; location="Jerusalem"
    pricePerParticipant=150; eventManagerId=$managerId; eventHostId=10
    invitationMessage="Welcome to E2E Test"; paymentDetails="Bank transfer"
} $managerToken
Test "POST /api/events" $r.status 201 $r.body
$newEventId = ($r.body | ConvertFrom-Json).id
Write-Host "   Created event ID: $newEventId"

# ── 4. Get Hosts ──────────────────────────────────────────────────────────────
Write-Host "`n[4] Get Event Hosts" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/eventhost" $null $managerToken
Test "GET /api/eventhost" $r.status 200 $r.body

# ── 5. Register Participant ───────────────────────────────────────────────────
Write-Host "`n[5] Register Participant" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/auth/register" @{name="Test Participant";email="test.e2e@example.com";password="Test1234"}
Test "POST /api/auth/register" $r.status 201 $r.body
$participantToken = ($r.body | ConvertFrom-Json).token
$participantId    = ($r.body | ConvertFrom-Json).id
Write-Host "   Participant ID: $participantId"

# ── 6. Add Participant to Event ───────────────────────────────────────────────
Write-Host "`n[6] Add Participant to Event" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/participants/event/$newEventId" @{
    name="Test Participant"; email="test.e2e@example.com"; mailingPreferences=@()
} $managerToken
Test "POST /api/participants/event/$newEventId" $r.status 201 $r.body

# ── 7. Create Poll ────────────────────────────────────────────────────────────
Write-Host "`n[7] Create Poll" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/polls/event/$newEventId" @{
    name="E2E Poll"; isPreliminary=$false; closingDate=$null
    questions=@(@{questionText="Best time?"; options=@("Morning","Evening","Weekend")})
} $managerToken
Test "POST /api/polls/event/$newEventId" $r.status 201 $r.body
$pollId = ($r.body | ConvertFrom-Json).id
Write-Host "   Created poll ID: $pollId"

# ── 8. Get Poll Results ───────────────────────────────────────────────────────
Write-Host "`n[8] Get Poll Results" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/polls/$pollId/results"
Test "GET /api/polls/$pollId/results" $r.status 200 $r.body

# ── 9. Get Polls by Event ─────────────────────────────────────────────────────
Write-Host "`n[9] Get Polls by Event" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/polls/event/$newEventId"
Test "GET /api/polls/event/$newEventId" $r.status 200 $r.body
$pollsByEvent = $r.body | ConvertFrom-Json
Write-Host "   Polls for event: $($pollsByEvent.Count)"

# ── 10. Send Invitations ──────────────────────────────────────────────────────
Write-Host "`n[10] Send Invitations" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/events/$newEventId/invitations" @{registrationLink="https://gatherup.app/rsvp"} $managerToken
Test "POST /api/events/$newEventId/invitations" $r.status 204 $r.body

# ── 11. Participant Login ─────────────────────────────────────────────────────
Write-Host "`n[11] Participant Login" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/auth/login" @{email="test.e2e@example.com";password="Test1234"}
Test "Participant login" $r.status 200 $r.body
$participantToken = ($r.body | ConvertFrom-Json).token
$participantId    = ($r.body | ConvertFrom-Json).id

# ── 12. Confirm Attendance ────────────────────────────────────────────────────
Write-Host "`n[12] Confirm Attendance (RSVP)" -ForegroundColor Yellow
$r = Invoke-API PUT "$base/api/participants/$participantId/attendance?eventId=$newEventId" @{
    isAttending=$true
    selectedPreferences=@("EventChanges","PollCreated","AttendanceConfirmed","PaymentReceived")
} $participantToken
Test "PUT /api/participants/$participantId/attendance" $r.status 200 $r.body

# ── 13. Get Participant ───────────────────────────────────────────────────────
Write-Host "`n[13] Get Participant" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/participants/$participantId" $null $participantToken
Test "GET /api/participants/$participantId" $r.status 200 $r.body
$p = $r.body | ConvertFrom-Json
Write-Host "   isAttending=$($p.isAttending) hasPaid=$($p.hasPaid)"

# ── 14. Submit Poll Vote ──────────────────────────────────────────────────────
Write-Host "`n[14] Submit Poll Vote" -ForegroundColor Yellow
$pollData    = Invoke-API GET "$base/api/polls/$pollId/results"
$questionId  = (($pollData.body | ConvertFrom-Json).questionResults | Select-Object -First 1).questionId
$r = Invoke-API POST "$base/api/polls/$pollId/vote" @{
    questionId=$questionId; participantId=$participantId; answer="Morning"
} $participantToken
Test "POST /api/polls/$pollId/vote" $r.status 200 $r.body

# ── 15. Manager Records Payment ───────────────────────────────────────────────
Write-Host "`n[15] Manager Records Payment" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/financial/payment" @{
    participantId=$participantId; eventId=$newEventId; amount=150
} $managerToken
Test "POST /api/financial/payment" $r.status 204 $r.body

# ── 16. Add Vendor ────────────────────────────────────────────────────────────
Write-Host "`n[16] Add Vendor" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/financial/vendor" @{
    eventId=$newEventId; name="E2E Catering"; initialDebt=500
} $managerToken
Test "POST /api/financial/vendor" $r.status 201 $r.body
$vendorId = ($r.body | ConvertFrom-Json).id
Write-Host "   Vendor ID: $vendorId"

# ── 17. Add Receipt ───────────────────────────────────────────────────────────
Write-Host "`n[17] Add Receipt" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/financial/vendor/$vendorId/receipt" @{
    receiptNumber="REC-E2E-001"; filePath="C:\temp\receipt.pdf"; amount=200; date="2025-12-01T00:00:00Z"
} $managerToken
Test "POST /api/financial/vendor/$vendorId/receipt" $r.status 201 $r.body

# ── 18. Get Financial Summary ─────────────────────────────────────────────────
Write-Host "`n[18] Financial Summary" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/financial/event/$newEventId/summary" $null $managerToken
Test "GET /api/financial/event/$newEventId/summary" $r.status 200 $r.body
$s = $r.body | ConvertFrom-Json
Write-Host "   Income=$($s.totalIncome) Expenses=$($s.totalExpenses) Balance=$($s.balance)"

# ── 19. Get Vendors by Event ──────────────────────────────────────────────────
Write-Host "`n[19] Get Vendors by Event" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/financial/event/$newEventId/vendors" $null $managerToken
Test "GET /api/financial/event/$newEventId/vendors" $r.status 200 $r.body

# ── 20. Send Payment Reminders ────────────────────────────────────────────────
Write-Host "`n[20] Send Payment Reminders" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/financial/event/$newEventId/reminders" @{bankDetails="Bank Hapoalim 12-345678"} $managerToken
Test "POST /api/financial/event/$newEventId/reminders" $r.status 204 $r.body

# ── 21. Send Host Invitation ──────────────────────────────────────────────────
Write-Host "`n[21] Send Host Invitation" -ForegroundColor Yellow
$r = Invoke-API POST "$base/api/events/$newEventId/host-invitation" @{hostMessageContent="Dear Host, you are invited!"} $managerToken
Test "POST /api/events/$newEventId/host-invitation" $r.status 204 $r.body

# ── 22. Update Event (triggers notifications) ─────────────────────────────────
Write-Host "`n[22] Update Event (triggers OnEventDetailsChanged)" -ForegroundColor Yellow
$r = Invoke-API PUT "$base/api/events/$newEventId" @{name="E2E Test Event Updated"; location="Tel Aviv"} $managerToken
Test "PUT /api/events/$newEventId" $r.status 200 $r.body

# ── 23. Notification Preferences ─────────────────────────────────────────────
Write-Host "`n[23] Update Notification Preferences" -ForegroundColor Yellow
$r = Invoke-API PUT "$base/api/participants/$participantId" @{
    name=$null; email=$null
    mailingPreferences=@("EventChanges","PollCreated","PaymentReceived")
} $participantToken
Test "PUT /api/participants/$participantId (prefs)" $r.status 200 $r.body

# ── 24. Poll Is Open ──────────────────────────────────────────────────────────
Write-Host "`n[24] Poll Is Open" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/polls/$pollId/open"
Test "GET /api/polls/$pollId/open" $r.status 200 $r.body

# ── 25. Get Event Participants ────────────────────────────────────────────────
Write-Host "`n[25] Get Event Participants" -ForegroundColor Yellow
$r = Invoke-API GET "$base/api/participants/event/$newEventId" $null $managerToken
Test "GET /api/participants/event/$newEventId" $r.status 200 $r.body
$ptps = $r.body | ConvertFrom-Json
Write-Host "   Participants in event: $($ptps.Count)"

# ── 26. Verify Email Log Written ──────────────────────────────────────────────
Write-Host "`n[26] Verify Email Log" -ForegroundColor Yellow
$logPath = "GatherUp.API\EmailsLog\emails.log"
if (Test-Path $logPath) {
    $lines = (Get-Content $logPath).Count
    $msg2 = "[PASS] Email log exists - $lines lines"
    Write-Host "   $msg2" -ForegroundColor Green
    $pass++; $results += $msg2
} else {
    $msg2 = "[FAIL] emails.log NOT found"
    Write-Host "   $msg2" -ForegroundColor Red
    $fail++; $results += $msg2
}

# ── 27. Verify XML Persistence ────────────────────────────────────────────────
Write-Host "`n[27] Verify XML Persistence" -ForegroundColor Yellow
$xmlFiles = @("Events.xml","Participants.xml","Polls.xml","VendorAllocations.xml","EventHosts.xml","EventManagers.xml")
foreach ($f in $xmlFiles) {
    $path = "GatherUp.API\XMLData\$f"
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        $xmlMsg = "[PASS] XML: $f - $size bytes"
        Write-Host "   $xmlMsg" -ForegroundColor Green
        $pass++; $results += $xmlMsg
    } else {
        $xmlMsg = "[FAIL] XML: $f missing"
        Write-Host "   $xmlMsg" -ForegroundColor Red
        $fail++; $results += $xmlMsg
    }
}

# ── 28. Delete test event (cleanup) ──────────────────────────────────────────
Write-Host "`n[28] Cleanup - Delete Test Event" -ForegroundColor Yellow
$r = Invoke-API DELETE "$base/api/events/$newEventId" $null $managerToken
Test "DELETE /api/events/$newEventId" $r.status 204 $r.body

# ── Summary ────────────────────────────────────────────────────────────────────
Write-Host "`n========== RESULTS ==========" -ForegroundColor Cyan
Write-Host "PASS: $pass" -ForegroundColor Green
Write-Host "FAIL: $fail" -ForegroundColor Red
Write-Host "=============================" -ForegroundColor Cyan
$results | Where-Object {$_ -like "*FAIL*"} | ForEach-Object { Write-Host $_ -ForegroundColor Red }
