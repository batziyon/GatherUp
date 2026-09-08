# GatherUp API — Swagger Flow Guide
## תהליך הצגה מסודר לבוחנת

---

## שלב 1: הרשמה וכניסה

### כניסה כמנהל אירוע (Manager)
```
POST /api/auth/login
{
  "email": "michal.cohen.dev@gmail.com",
  "password": "any"
}
```
→ קבלי token, לחצי **Authorize** והכניסי: `Bearer {token}`

### הרשמה כמשתתף חדש
```
POST /api/auth/register
{
  "name": "שם חדש",
  "email": "new@gmail.com",
  "password": "12345"
}
```

---

## שלב 2: צפייה באירועים (ללא Token)

```
GET /api/events            ← כל האירועים
GET /api/events/500        ← אירוע ספציפי
GET /api/events/my/managed ← האירועים שאני מנהל (דורש Token מנהל)
GET /api/events/my/participating ← האירועים שאני משתתף (דורש Token)
```

---

## שלב 3: יצירת אירוע חדש (Manager בלבד)

```
POST /api/events
{
  "name": "חגיגת סיום קורס",
  "date": "2025-12-01T18:00:00",
  "location": "תל אביב",
  "pricePerParticipant": 120,
  "eventManagerId": 1,
  "eventHostId": 10
}
```

---

## שלב 4: ניהול משתתפים (Manager)

```
GET  /api/participants/event/500         ← רשימת משתתפים באירוע
POST /api/participants/event/500         ← הוספת משתתף לאירוע
{
  "id": 300,
  "name": "משתמש חדש",
  "email": "user300@gmail.com",
  "mailingPreferences": [1, 4]
}

PUT  /api/participants/300/attendance?eventId=500   ← אישור הגעה
{ "isAttending": true }

POST /api/events/500/invitations?eventLink=https://gatherup.app/invite/500
     ← שליחת הזמנות לכולם שטרם השיבו
```

---

## שלב 5: ניהול פיננסי (Manager)

```
POST /api/financial/payment
{ "participantId": 300, "eventId": 500, "amount": 120 }

POST /api/financial/vendor
{ "name": "קייטרינג חדש", "initialDebt": 3000 }

POST /api/financial/vendor/1/debt
{ "amount": 500 }

POST /api/financial/vendor/1/receipt
{
  "id": 99,
  "receiptNumber": "RCP-2025-001",
  "filePath": "C:\\temp\\receipt.txt",
  "amount": 4500,
  "date": "2025-07-01T00:00:00"
}

GET /api/financial/event/500/summary   ← סיכום פיננסי מלא
GET /api/financial/event/500/balance   ← יתרה נקייה
GET /api/financial/event/500/receipts  ← קבלות ממוינות לפי תאריך
```

---

## שלב 6: ניהול סקרים

```
POST /api/polls/event/500   (Manager)
{
  "name": "סקר מיקום סופי",
  "questions": [
    { "questionText": "איפה?", "options": ["ת\"א", "י-ם", "חיפה"] },
    { "questionText": "מתי?",  "options": ["שישי", "שבת"] }
  ]
}

GET /api/polls/1/results     ← תוצאות עם אחוזים
GET /api/polls/1/open        ← האם הסקר פתוח

POST /api/polls/1/vote       (כל משתתף)
{ "questionId": 1, "participantId": 101, "answer": "ת\"א" }
```

---

## שלב 7: עדכון אירוע (רק מנהל הבעלים)

```
PUT /api/events/500
{ "name": "שם חדש", "location": "חיפה", "pricePerParticipant": 150 }

DELETE /api/events/500   ← מחיקה (רק מנהל האירוע)
```

---

## שלב 8: כניסה כמשתתף

```
POST /api/auth/login
{ "email": "avigail@gmail.com", "password": "any" }
```
→ Authorize עם Token החדש

```
GET  /api/events/my/participating            ← האירועים שלי
PUT  /api/participants/102/attendance?eventId=500
     { "isAttending": true }

POST /api/polls/1/vote
{ "questionId": 1, "participantId": 102, "answer": "ירושלים" }
```

---

## שגיאות לדוגמה

| פעולה | קוד | משמעות |
|-------|-----|--------|
| GET /api/events/9999 | 400 ENTITY_NOT_FOUND | אירוע לא קיים |
| PUT /api/events/501 (מנהל של 500) | 403 Forbidden | לא הבעלים |
| POST /api/financial/vendor/1/receipt (חוב=0) | 400 RECEIPT_LOCKED | קבלה נעולה |
| POST /api/polls/1/vote — participantId שונה מToken | 403 Forbidden | לא מורשה |
