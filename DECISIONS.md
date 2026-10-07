<div dir="rtl">

# DECISIONS

> מלאו את הקובץ הזה. הוא חלק מההערכה — קצר וברור עדיף על ארוך.

## 1. החלטות ארכיטקטוניות (Backend)
- פיצלתי את `LeaveRequestsController` לשתי שכבות: Controller (דק, רק ממפה HTTP ↔ קריאות לשירות) ו‑`LeaveRequestService` (כל הלוגיקה העסקית: בדיקת מכסה, טרנזקציה ל‑approve, validation). ה‑`DbContext` עצמו משמש כ‑repository/unit-of-work — **לא** הוספתי שכבת Repository נפרדת מעליו, כי זה over-engineering: EF Core כבר נותן את ההפשטה הזו, ושכבה נוספת רק הייתה מוסיפה בויילרפלייט בלי ערך אמיתי בפרויקט בגודל הזה.
- הוספתי `LeaveRequestDto`/`EmployeeDto` כצורת החזרה במקום לפרמט ישירות את ה‑entities של EF (`LeaveRequest`/`Employee`) — זה מנתק את חוזה ה‑API מהמודל של ה‑DB, ומייתר את הצורך ב‑`ReferenceHandler.IgnoreCycles` בגלל ה‑navigation המעגלי בין Employee ל‑LeaveRequest.
- לשגיאות משתמשים ב‑`ServiceResult<T>` עם `ErrorType` (NotFound / Conflict / Invalid) — ה‑Controller ממפה את זה לקודי HTTP (404/409/400) בלי לדעת שום דבר על הלוגיקה העסקית עצמה. פשוט מספיק בלי enum/exception hierarchy מורכב.

## 2. הבאג ביתרת החופשה
- הבאג: ב‑`LeaveRequestsController.Create` חושב `used` (ימי חופשה מאושרים שכבר נוצלו) אבל הוא לא נבדק בפועל — התנאי היה רק `days > AnnualQuota`, כך שבקשה בודדת נבדקה מול כל המכסה בלי להתחשב בניצול קודם. תיקון: `used + days > AnnualQuota`.
- הטסט: `Create_ExceedingRemainingQuota_FailsEvenThoughSingleRequestFitsAnnualQuota` — מכסה 10, כבר 8 ימים מאושרים, בקשה נוספת של 3 ימים נדחית (`BadRequest`).

## 3. אישור בקשה (approve) ו‑concurrency
- `POST /api/leave-requests/{id}/approve`: בקשה לא קיימת → `404`. כבר `Approved`/`Rejected` → `409`. אישור שהיה חורג מהמכסה (בדיקה מחדש של הימים המאושרים) → `409` והבקשה נשארת `Pending`.
- ה‑concurrency risk: שתי בקשות `Pending` לאותו עובד שביחד חורגות מהמכסה (אך כל אחת לבד לא) — אם שתיהן מאושרות "במקביל", שתיהן עלולות לקרוא את אותו "used" לפני שמישהי עדכנה, ושתיהן יעברו בטעות.
- הפתרון: כל הלוגיקה (בדיקת סטטוס + חישוב used + עדכון) עטופה בטרנזקציה אחת. ב‑PostgreSQL נועלים את שורת ה‑Employee עם `SELECT ... FOR UPDATE`, כך שאישור שני לאותו עובד ימתין לראשון ויראה נתונים מעודכנים. ב‑SQLite (המצב שעבדתי בו, ללא Docker) אין row locking אמיתי — SQLite נועל את כל קובץ ה‑DB לכתיבה (single-writer), מה שמסתיר את הבעיה בפועל אך לא פותר אותה באופן מכוון.
- עם עוד זמן: להוסיף גם concurrency token (`RowVersion`/`xmin`) על `LeaveRequest` כהגנה כפולה (optimistic concurrency).

## 4. Frontend
- **טופס הגשת בקשה (validation)**: Reactive Form (`FormGroup`) ב‑`LeaveRequestsComponent` עם `Validators.required` על עובד/סוג/תאריכים, ו‑cross-field validator שבודק `startDate <= endDate`. מכיוון שה‑API מחשב את מספר הימים מהתאריכים (ולא מקבל שדה `days` נפרד), הדרישה "אין מספר ימים שלילי" ממומשת דרך אותו validator — ברגע שתאריך ההתחלה לא מאוחר מתאריך הסיום, הימים המחושבים תמיד ≥ 1. הודעות שגיאה מוצגות לכל שדה בנפרד + הודעת שגיאה/הצלחה מהשרת בעת שליחה.
- **כפתור האישור**: הוחלף מ‑"POST ואז רענון כל הרשימה" למצב per-row: `approvingIds: Set<number>` מנטרל רק את הכפתור של השורה הרלוונטית ומציג "Approving..." בזמן הקריאה. בהצלחה — מעדכנים רק את השורה הזו מה‑response (ה‑API כבר מחזיר את הבקשה המעודכנת) ומציגים toast חולף (נעלם אוטומטית אחרי 3 שניות, לא נשאר קבוע ולא `alert`). בכישלון (למשל 409 כי הבקשה כבר אושרה, או חריגה ממכסה) — מוצגת הודעת השגיאה מהשרת מתחת לשורה הספציפית.
- **שיפור הקומפוננטה (service layer, טיפוסים, RxJS)**: כל קריאות ה‑HTTP הוצאו מ‑`LeaveRequestsComponent` ל‑`LeaveRequestsService` (`getAll`/`getEmployees`/`create`/`approve`, כל אחת מחזירה `Observable` מוקלד). הוסר כל שימוש ב‑`any` — `requests: LeaveRequest[]`, ותגובות שגיאה מוקלדות כ‑`HttpErrorResponse`. הטפסים הריאקטיביים כבר מוקלדים אוטומטית ע"י Angular (strict typed forms), כך ש‑`form.value` מחזיר טיפוסים אמיתיים ולא `any`. לניקוי subscriptions בלי memory leaks השתמשתי ב‑`takeUntilDestroyed(this.destroyRef)` על כל ה‑Observables (גם אם קריאות HttpClient משלימות מעצמן אחרי תגובה אחת, זו עדיין הדרך הנכונה/אידיומטית ב‑Angular 17 להבטיח ניקוי אם הקומפוננטה נהרסת לפני שהתגובה חוזרת).

## 5. על מה ויתרתי בגלל הזמן
- ... ומה הייתי עושה עם עוד יום:

## 6. שימוש ב‑AI
### איפה AI עזר (כולל prompts)
1. prompt: "..." → מה קיבלתי ומה עשיתי איתו:
2. ...

### איפה דחיתי/תיקנתי הצעה של AI
- מה AI הציע, למה זה היה שגוי, ומה עשיתי במקום:
- **כפתור האישור (משימה 5)**: ה‑AI מימש loading state ו‑error handling לכפתור ה‑approve, אבל פספס את דרישת ה‑"משוב הצלחה" מהפרומפט המקורי — לא הייתה שום אינדיקציה למשתמש כשהאישור הצליח (השורה פשוט התעדכנה בשקט). **אני עצרתי את ה‑AI לפני ה‑commit**, הצבעתי על החוסר, והעתקתי שוב את דרישת המשימה המלאה כדי לוודא שתיקון מפורש יתווסף. לקח: גם כש‑AI עונה על רוב הדרישות בפרומפט מורכב (3 תת‑דרישות: loading/error/success), כדאי לבדוק שכל סעיף אכן מומש בפועל ולא רק חלקם לפני שמאשרים commit.

### אבטחה
- **SQL Injection** (נמצא ותוקן): `LeaveRequestsController.Search` (לפני הריפקטור של סעיף 1) בנה שאילתת SQL על ידי שרשור מחרוזת ישירות עם קלט המשתמש (`"...LIKE '%" + name + "%'"`), ללא פרמטריזציה — תוקף יכל להזריק SQL דרך פרמטר ה‑query `name`. תוקן ב‑`LeaveRequestService.Search` עם `EF.Functions.Like` שמפרמט את הערך אוטומטית.

## 7. הוראות הרצה
- Docker לא היה מותקן על המכונה. עבדתי בלי Docker: backend מול SQLite (`Database__Provider=Sqlite`), frontend עם `npm start` רגיל. כוונה לעבור בהמשך ל‑Docker/Postgres לבדיקה אמיתית של משימת ה‑concurrency.

</div>


