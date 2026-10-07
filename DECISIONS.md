<div dir="rtl">

# DECISIONS

> מלאו את הקובץ הזה. הוא חלק מההערכה — קצר וברור עדיף על ארוך.

## 1. החלטות ארכיטקטוניות
- פיצלתי את `LeaveRequestsController` לשתי שכבות: Controller (דק, רק ממפה HTTP ↔ קריאות לשירות) ו‑`LeaveRequestService` (כל הלוגיקה העסקית: בדיקת מכסה, טרנזקציה ל‑approve, validation). ה‑`DbContext` עצמו משמש כ‑repository/unit-of-work — **לא** הוספתי שכבת Repository נפרדת מעליו, כי זה over-engineering: EF Core כבר נותן את ההפשטה הזו, ושכבה נוספת רק הייתה מוסיפה בויילרפלייט בלי ערך אמיתי בפרויקט בגודל הזה.
- הוספתי `LeaveRequestDto`/`EmployeeDto` כצורת החזרה במקום לפרמט ישירות את ה‑entities של EF (`LeaveRequest`/`Employee`) — זה מנתק את חוזה ה‑API מהמודל של ה‑DB, ומייתר את הצורך ב‑`ReferenceHandler.IgnoreCycles` בגלל ה‑navigation המעגלי בין Employee ל‑LeaveRequest.
- לשגיאות משתמשים ב‑`ServiceResult<T>` עם `ErrorType` (NotFound / Conflict / Invalid) — ה‑Controller ממפה את זה לקודי HTTP (404/409/400) בלי לדעת שום דבר על הלוגיקה העסקית עצמה. פשוט מספיק בלי enum/exception hierarchy מורכב.
- דרך אגב תיקנתי גם SQL injection ב‑`Search` (ראו סעיף 5 — אבטחה) כי נגעתי באותו קוד בכל מקרה.
- **Frontend — טופס הגשת בקשה**: הוספתי Reactive Form (`FormGroup`) ב‑`LeaveRequestsComponent` עם `Validators.required` על עובד/סוג/תאריכים, ו‑cross-field validator שבודק `startDate <= endDate`. מכיוון שה‑API מחשב את מספר הימים מהתאריכים (ולא מקבל שדה `days` נפרד), הדרישה "אין מספר ימים שלילי" ממומשת דרך אותו validator — ברגע שתאריך ההתחלה לא מאוחר מתאריך הסיום, הימים המחושבים תמיד ≥ 1. הודעות שגיאה מוצגות לכל שדה בנפרד + הודעת שגיאה/הצלחה מהשרת בעת שליחה.

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
- **SQL Injection** (נמצא ותוקן): `LeaveRequestsController.Search` (לפני הריפקטור של סעיף 1) בנה שאילתת SQL על ידי שרשור מחרוזת ישירות עם קלט המשתמש (`"...LIKE '%" + name + "%'"`), ללא פרמטריזציה — תוקף יכל להזריק SQL דרך פרמטר ה‑query `name`. תוקן ב‑`LeaveRequestService.Search` עם `EF.Functions.Like` שמפרמט את הערך אוטומטית.

## 6. הוראות הרצה
- Docker לא היה מותקן על המכונה. עבדתי בלי Docker: backend מול SQLite (`Database__Provider=Sqlite`), frontend עם `npm start` רגיל. כוונה לעבור בהמשך ל‑Docker/Postgres לבדיקה אמיתית של משימת ה‑concurrency.

</div>

