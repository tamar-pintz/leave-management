<div dir="rtl">

# DECISIONS

> מלאו את הקובץ הזה. הוא חלק מההערכה — קצר וברור עדיף על ארוך.

## 1. החלטות ארכיטקטוניות
- ...

## 2. הבאג ביתרת החופשה
- מה היה הבאג, איפה, ואיך תיקנתי: ב‑`LeaveRequestsController.Create` (`backend/src/LeaveManagement.Api/Controllers/LeaveRequestsController.cs`) חושב `used` — סך ימי החופשה המאושרים שהעובד כבר ניצל — אבל המשתנה מעולם לא נבדק. התנאי היה `days > employee.AnnualQuota`, כלומר רק הבקשה הבודדת הנוכחית נבדקת מול כל המכסה השנתית, בלי להתחשב בניצול קודם. תיקנתי ל‑`used + days > employee.AnnualQuota`.
- הטסט שמוכיח את התיקון: `Create_ExceedingRemainingQuota_FailsEvenThoughSingleRequestFitsAnnualQuota` ב‑`backend/tests/LeaveManagement.Tests/LeaveRequestsTests.cs` — עובד עם מכסה של 10 ימים ו‑8 ימים מאושרים כבר, מנסה להגיש בקשה נוספת של 3 ימים (פחות מ‑10 בעצמה, אך חורגת מהיתרה שנותרה) ומוודא שחוזר `BadRequest`.

## 3. אישור בקשה (approve) ו‑concurrency
- איך טיפלתי במצבים לא חוקיים (כבר אושר / לא קיים):
- מה לגבי אישור של שתי בקשות במקביל:

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
- **Docker לא היה מותקן על המכונה בזמן העבודה.** במקום להמתין להתקנה/הורדה, הרצתי את הפרויקט במצב הנוע ללא Docker: backend מול SQLite (`Database__Provider=Sqlite`) ו‑frontend עם `npm start` רגיל. בכוונה לעבור בהמשך ל‑Docker (PostgreSQL) כדי לבדוק את משימת ה‑concurrency מול DB אמיתי עם נעילות/טרנזקציות אמיתיות, כפי שה‑README ממליץ.

</div>
