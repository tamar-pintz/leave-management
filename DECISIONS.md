<div dir="rtl">

# DECISIONS

> מלאו את הקובץ הזה. הוא חלק מההערכה — קצר וברור עדיף על ארוך.

## 1. החלטות ארכיטקטוניות
- ...

## 2. הבאג ביתרת החופשה
- הבאג: ב‑`LeaveRequestsController.Create` חושב `used` (ימי חופשה מאושרים שכבר נוצלו) אבל הוא לא נבדק בפועל — התנאי היה רק `days > AnnualQuota`, כך שבקשה בודדת נבדקה מול כל המכסה בלי להתחשב בניצול קודם. תיקון: `used + days > AnnualQuota`.
- הטסט: `Create_ExceedingRemainingQuota_FailsEvenThoughSingleRequestFitsAnnualQuota` — מכסה 10, כבר 8 ימים מאושרים, בקשה נוספת של 3 ימים נדחית (`BadRequest`).

## 3. אישור בקשה (approve) ו‑concurrency
- `POST /api/leave-requests/{id}/approve`: בקשה לא קיימת → `404`. כבר `Approved`/`Rejected` → `409`. אישור שהיה חורג מהמכסה (בדיקה מחדש של הימים המאושרים) → `409` והבקשה נשארת `Pending`.
- ה‑concurrency risk: שתי בקשות `Pending` לאותו עובד שביחד חורגות מהמכסה (אך כל אחת לבד לא) — אם שתיהן מאושרות "במקביל", שתיהן עלולות לקרוא את אותו "used" לפני שמישהי עדכנה, ושתיהן יעברו בטעות.
- הפתרון: כל הלוגיקה (בדיקת סטטוס + חישוב used + עדכון) עטופה בטרנזקציה אחת. ב‑PostgreSQL נועלים את שורת ה‑Employee עם `SELECT ... FOR UPDATE`, כך שאישור שני לאותו עובד ימתין לראשון ויראה נתונים מעודכנים. ב‑SQLite (המצב שעבדתי בו, ללא Docker) אין row locking אמיתי — SQLite נועל את כל קובץ ה‑DB לכתיבה (single-writer), מה שמסתיר את הבעיה בפועל אך לא פותר אותה באופן מכוון.
- עם עוד זמן: להוסיף גם concurrency token (`RowVersion`/`xmin`) על `LeaveRequest` כהגנה כפולה (optimistic concurrency).

## 4. על מה ויתרתי בגלל הזמן
- ... ומה הייתי עושה עם עוד יום:

## 5. שימוש ב‑AI
### איפה AI עזר (כולל prompts)
1. prompt: "..." → מה קיבלתי ומה עשיתי איתו:
2. ...

### איפה דחיתי/תיקנתי הצעה של AI
- מה AI הציע, למה זה היה שגוי, ומה עשיתי במקום:

### אבטחה
- אם מצאתם בעיית אבטחה: מה מצאתם, איפה (קובץ + שורה), למה זו בעיה, ואיך תיקנתם:

## 6. הוראות הרצה
- Docker לא היה מותקן על המכונה. עבדתי בלי Docker: backend מול SQLite (`Database__Provider=Sqlite`), frontend עם `npm start` רגיל. כוונה לעבור בהמשך ל‑Docker/Postgres לבדיקה אמיתית של משימת ה‑concurrency.

</div>

