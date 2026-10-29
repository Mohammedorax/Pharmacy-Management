# اختبار واجهة النظام

هذا المشغل يستخدم FlaUI UIA2 لأنه الأنسب لتطبيق WinForms الحالي. لا يدخل بيانات دخول ولا يحفظ في قاعدة البيانات.

## التشغيل

1. ابنِ project_fo_3.sln بوضع Release باستخدام MSBuild.
2. شغّل scripts\Test-UiSmoke.ps1.
3. راجع artifacts\ui-smoke\login-smoke-report.json والصورة login-window.png.

لقبول مكونات AntdUI الحقيقية مع العربية شغّل المشغل مع `--scenario antdui-arabic`. يفتح دليلًا ببيانات وهمية، يكتب نصًا عربيًا مشكولًا ومختلطًا عبر لوحة المفاتيح، ويتحقق حرفيًا من ValuePattern ومن ظهور Input وSelect وInputNumber وTable دون اتصال بقاعدة البيانات.

لفحص مرشح الكاشير المعزول شغّل `scripts\Test-UiSmoke.ps1 -Scenario dashboard`. يتحقق UIA من حجمه وترتيبه، ويستخدم `PrintWindow` لتصوير نافذة التطبيق نفسها حتى إن غطاها تطبيق آخر. قد يشوّه `PrintWindow` رسم العربية؛ الصورة ليست بديلًا عن فحص النافذة المرئية على الجهاز.

المشروع منفصل عن solution الإنتاجي عمداً؛ MSBuild الخاص بـ.NET Framework لا يملك SDK الخاص بمشروع الاختبار .NET 10، بينما dotnet يبنيه بصورة مستقلة.
