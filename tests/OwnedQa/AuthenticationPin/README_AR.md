# فحص تثبيت اتصال المصادقة على QA

أدوات تطوير محلية للمسار D:/Comp/Pharmacy-Management-master. ليست جزءا من تشغيل المستخدم. تتطلب SQL Server محليا ونسخة QA موثقة تحتوي69جدولا ومصدر بيانات العرض المعروف. لا تستخدم قاعدة العمل ولا تشغّل اختبارات SQL على اسم قاعدة تختاره يدويا.

1. مرر إلى Prepare-AuthenticationPinSqlQa.ps1 -VerifiedBackupJobPath مسار ملف JSON لإثبات النسخة الاحتياطية المقبولة: Accepted وBackupPath وBackupSHA256 وOwner وSource وDirectory. يجب أن يحوي Directory/source-before.json. تتحقق الأداة من البصمة، وتستعيد قاعدتين جديدتين بأسماء PharmacyAuthPin_GUID_A_qa وB_qa وتضيف علامة الملكية، وترفض الاستبدال.
2. شغّل Run-AuthenticationPinSqlProbe.ps1 -Mode fixed مرة واحدة، ثم Run-AuthenticationPinFollowup.ps1 -Mode followup مرة واحدة. تحقق من stdout وstderr وstate.json في كل مرحلة. تكتب الاختبارات موظفين تجريبيين وترقي كلمات مرورهم وتسجل تدقيقهم في النسختين فقط. لا تعِد تشغيل المرحلة نفسها؛ يُرفض دليل موجود.
3. شغّل Audit-AuthenticationPinData.ps1 -VerifiedBackupJobPath بالملف نفسه لمقارنة الجداول69والتحقق من ثبات المصدر وDBCC. تبقى النسخ والأدلة محفوظة؛ لا حذف تلقائي.

وضع baseline مخصص للتشغيل التاريخي0D92قبلالإصلاح؛ ليس اختبار نجاح للنسخة المصححة. أدلته الأصلية محفوظة في تقرير القبول. تبديل ملف الاتصال في هذه الاختبارات داخل ذاكرة العملية؛ لا تتغير إعداداتProgramDataولا يبدأSessionContextللمستخدم ولا يُمس مخزن تذكرني أو أي طابعة.

التقرير: docs/qa/AUTHENTICATION_CONNECTION_PIN_20261013_AR.md. البصمة شرط قبول؛ وجود ملف الاختبار أو نجاح البناء وحده لا يثبت نجاحSQL.
