[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
throw 'تم إيقاف هذا المشغّل القديم لمنع ترحيل غير موحّد. استخدم scripts\Apply-CommercialMigrations.ps1 مع -Environment و-VerifiedBackupPath على قاعدة مستعادة تنتهي بـ _test أو _qa أو _sandbox.'
