using System.Text.Json;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using Application = FlaUI.Core.Application;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Match UIA and mouse coordinates to physical pixels on scaled displays.
        if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
            throw new InvalidOperationException("UI automation requires per-monitor DPI awareness.");
        string executablePath = ReadOption(args, "--exe")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "project_fo_3", "bin", "Release", "project_fo_3.exe"));
        string artifactsDirectory = ReadOption(args, "--artifacts")
            ?? Path.Combine(Path.GetDirectoryName(executablePath)!, "ui-automation-artifacts");
        string scenario = ReadValueOption(args, "--scenario") ?? "login";
        string? cancellationSignalPath = ReadOption(args, "--cancel-signal");

        var report = new UiSmokeReport
        {
            StartedAtUtc = DateTimeOffset.UtcNow,
            ExecutablePath = executablePath,
            AutomationProvider = "UIA2"
        };
        Application? application = null;

        try
        {
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("لم يتم العثور على ملف التطبيق. ابنِ Release أولاً.", executablePath);
            }

            Directory.CreateDirectory(artifactsDirectory);
            var startInfo = new ProcessStartInfo(executablePath)
            {
                Arguments = string.Equals(scenario, "login", StringComparison.OrdinalIgnoreCase)
                    ? "--preview-login-ui"
                    : string.Equals(scenario, "login-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-login-ui-dark"
                    : string.Equals(scenario, "login-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-login-ui-english"
                    : string.Equals(scenario, "login-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-login-ui-english-dark"
                    : string.Equals(scenario, "antdui-arabic", StringComparison.OrdinalIgnoreCase)
                    ? "--preview-antdui-components"
                    : string.Equals(scenario, "operational-report", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-operational-report-ui"
                    : string.Equals(scenario, "operational-report-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-operational-report-ui-dark"
                    : string.Equals(scenario, "operational-report-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-operational-report-ui-english"
                    : string.Equals(scenario, "operational-report-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-operational-report-ui-english-dark"
                    : string.Equals(scenario, "system-monitor", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-system-monitor-ui"
                    : string.Equals(scenario, "system-monitor-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-system-monitor-ui-english"
                    : string.Equals(scenario, "inventory-alerts", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-alerts-ui"
                    : string.Equals(scenario, "inventory-alerts-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-alerts-ui-english"
                    : string.Equals(scenario, "report-print-options", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-print-options-ui"
                    : string.Equals(scenario, "report-print-options-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-print-options-ui-dark"
                    : string.Equals(scenario, "report-print-options-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-print-options-ui-english"
                    : string.Equals(scenario, "report-print-options-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-print-options-ui-english-dark"
                    : string.Equals(scenario, "report-columns", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-columns-ui"
                    : string.Equals(scenario, "report-columns-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-columns-ui-dark"
                    : string.Equals(scenario, "report-columns-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-columns-ui-english"
                    : string.Equals(scenario, "report-columns-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-columns-ui-english-dark"
                    : string.Equals(scenario, "report-template-name", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-template-name-ui"
                    : string.Equals(scenario, "report-template-name-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-template-name-ui-dark"
                    : string.Equals(scenario, "report-template-name-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-template-name-ui-english"
                    : string.Equals(scenario, "report-template-name-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-report-template-name-ui-english-dark"
                    : string.Equals(scenario, "excel-import", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-excel-import-ui"
                    : string.Equals(scenario, "excel-export", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-excel-export-ui"
                    : string.Equals(scenario, "excel-export-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-excel-export-ui-dark"
                    : string.Equals(scenario, "excel-export-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-excel-export-ui-english"
                    : string.Equals(scenario, "excel-export-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-excel-export-ui-english-dark"
                    : scenario.StartsWith("invoice-drafts", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-invoice-drafts-ui" + scenario.Substring("invoice-drafts".Length)
                    : scenario.StartsWith("cashboxes", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-cashboxes-ui" + scenario.Substring("cashboxes".Length)
                    : string.Equals(scenario, "active-ingredients", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-active-ingredients-ui"
                    : scenario.StartsWith("customer-management", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-customer-management-ui" + scenario.Substring("customer-management".Length)
                    : string.Equals(scenario, "medicine-catalog-errors", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-errors-ui"
                    : string.Equals(scenario, "medicine-catalog-errors-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-errors-ui-dark"
                    : string.Equals(scenario, "medicine-catalog-errors-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-errors-ui-english"
                    : string.Equals(scenario, "medicine-catalog-errors-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-errors-ui-english-dark"
                    : string.Equals(scenario, "medicine-catalog-import", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-import-ui"
                    : string.Equals(scenario, "medicine-catalog-import-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-import-ui-dark"
                    : string.Equals(scenario, "medicine-catalog-import-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-import-ui-english"
                    : string.Equals(scenario, "medicine-catalog-import-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-catalog-import-ui-english-dark"
                    : string.Equals(scenario, "dashboard", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-dashboard-ui"
                    : string.Equals(scenario, "dashboard-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-dashboard-ui-english"
                    : string.Equals(scenario, "home", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-home-ui"
                    : string.Equals(scenario, "home-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-home-ui-english"
                    : string.Equals(scenario, "home-workspace", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-home-workspace-ui"
                    : string.Equals(scenario, "home-workspace-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-home-workspace-ui-english"
                    : string.Equals(scenario, "inventory-operation", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-operation-ui"
                    : string.Equals(scenario, "inventory-operation-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-operation-ui-english"
                    : string.Equals(scenario, "inventory-movement", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-movement-ui"
                    : string.Equals(scenario, "inventory-movement-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-movement-ui-english"
                    : string.Equals(scenario, "inventory-summary", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-summary-ui"
                    : string.Equals(scenario, "inventory-summary-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-summary-ui-english"
                    : string.Equals(scenario, "inventory-stock-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-stock"
                    : string.Equals(scenario, "inventory-lots-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-lots"
                    : string.Equals(scenario, "inventory-summary-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-summary"
                    : string.Equals(scenario, "inventory-turnover-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-turnover"
                    : string.Equals(scenario, "inventory-alerts-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-alerts"
                    : string.Equals(scenario, "inventory-movement-cancel-loading", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-movement"
                    : string.Equals(scenario, "inventory-movement-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-movement"
                    : string.Equals(scenario, "inventory-integrity-liveqa", StringComparison.OrdinalIgnoreCase)
                        ? "--qa-live-inventory-report-ui inventory-integrity"
                    : string.Equals(scenario, "inventory-stock-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-stock-ui-dark"
                    : string.Equals(scenario, "inventory-stock-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-stock-ui-english-dark"
                    : string.Equals(scenario, "inventory-stock", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-stock-ui"
                    : string.Equals(scenario, "inventory-stock-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-stock-ui-english"
                    : string.Equals(scenario, "inventory-lots-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-lots-ui-dark"
                    : string.Equals(scenario, "inventory-lots-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-lots-ui-english-dark"
                    : string.Equals(scenario, "inventory-lots", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-lots-ui"
                    : string.Equals(scenario, "inventory-lots-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-lots-ui-english"
                    : string.Equals(scenario, "inventory-turnover", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-turnover-ui"
                    : string.Equals(scenario, "inventory-turnover-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-turnover-ui-english"
                    : string.Equals(scenario, "purchases-inputs", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-purchases-ui"
                    : string.Equals(scenario, "purchases-inputs-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-purchases-ui-dark"
                    : string.Equals(scenario, "purchases-inputs-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-purchases-ui-english"
                    : string.Equals(scenario, "purchases-inputs-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-purchases-ui-english-dark"
                    : string.Equals(scenario, "medicine-editor", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-ui"
                    : string.Equals(scenario, "quick-search", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quick-search-ui"
                    : string.Equals(scenario, "quick-search-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quick-search-ui-dark"
                    : string.Equals(scenario, "quick-search-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quick-search-ui-english"
                    : string.Equals(scenario, "quick-search-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quick-search-ui-english-dark"
                    : string.Equals(scenario, "medicine-editor-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-ui-dark"
                    : string.Equals(scenario, "medicine-editor-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-ui-english"
                    : string.Equals(scenario, "medicine-editor-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-medicine-ui-english-dark"
                    : string.Equals(scenario, "agent-password", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-agent-ui"
                    : string.Equals(scenario, "agent", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-agent-ui"
                    : string.Equals(scenario, "agent-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-agent-ui-dark"
                    : string.Equals(scenario, "agent-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-agent-ui-english"
                    : string.Equals(scenario, "agent-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-agent-ui-english-dark"
                    : string.Equals(scenario, "billing-customer-type", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-billing-ui"
                    : string.Equals(scenario, "billing-customer-type-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-billing-ui-dark"
                    : string.Equals(scenario, "billing-customer-type-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-billing-ui-english"
                    : string.Equals(scenario, "billing-customer-type-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-billing-ui-english-dark"
                    : string.Equals(scenario, "saudi-settings", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-saudi-settings-ui"
                    : string.Equals(scenario, "saudi-settings-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-saudi-settings-ui-dark"
                    : string.Equals(scenario, "saudi-settings-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-saudi-settings-ui-english"
                    : string.Equals(scenario, "saudi-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-saudi-settings-ui-english-dark"
                    : string.Equals(scenario, "pharmacy-profile", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-pharmacy-profile-ui"
                    : string.Equals(scenario, "pharmacy-profile-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-pharmacy-profile-ui-dark"
                    : string.Equals(scenario, "pharmacy-profile-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-pharmacy-profile-ui-english"
                    : string.Equals(scenario, "pharmacy-profile-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-pharmacy-profile-ui-english-dark"
                    : string.Equals(scenario, "bootstrap-manager", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-bootstrap-manager-ui"
                    : string.Equals(scenario, "bootstrap-manager-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-bootstrap-manager-ui-dark"
                    : string.Equals(scenario, "bootstrap-manager-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-bootstrap-manager-ui-english"
                    : string.Equals(scenario, "bootstrap-manager-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-bootstrap-manager-ui-english-dark"
                    : string.Equals(scenario, "commercial-return", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-commercial-return-ui"
                    : string.Equals(scenario, "commercial-return-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-commercial-return-ui-dark"
                    : string.Equals(scenario, "commercial-return-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-commercial-return-ui-english"
                    : string.Equals(scenario, "commercial-return-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-commercial-return-ui-english-dark"
                    : string.Equals(scenario, "return-history", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-return-history-ui"
                    : string.Equals(scenario, "return-history-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-return-history-ui-dark"
                    : string.Equals(scenario, "return-history-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-return-history-ui-english"
                    : string.Equals(scenario, "return-history-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-return-history-ui-english-dark"
                    : string.Equals(scenario, "quarantine-resolution", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quarantine-resolution-ui"
                    : string.Equals(scenario, "quarantine-resolution-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quarantine-resolution-ui-dark"
                    : string.Equals(scenario, "quarantine-resolution-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quarantine-resolution-ui-english"
                    : string.Equals(scenario, "quarantine-resolution-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quarantine-resolution-ui-english-dark"
                    : string.Equals(scenario, "permissions", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-permissions-ui"
                    : string.Equals(scenario, "manufacturer", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-manufacturer-ui"
                    : string.Equals(scenario, "manufacturer-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-manufacturer-ui-dark"
                    : string.Equals(scenario, "manufacturer-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-manufacturer-ui-english"
                    : string.Equals(scenario, "manufacturer-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-manufacturer-ui-english-dark"
                    : string.Equals(scenario, "quick-journal", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-quick-journal-ui"
                    : string.Equals(scenario, "journal-entry", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-journal-ui"
                    : string.Equals(scenario, "journal-entry-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-journal-ui-dark"
                    : string.Equals(scenario, "voucher-actions", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-voucher-ui"
                    : string.Equals(scenario, "voucher-actions-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-voucher-ui-dark"
                    : string.Equals(scenario, "settings", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-antdui-settings"
                    : string.Equals(scenario, "settings-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-antdui-settings-english"
                        : string.Equals(scenario, "currency-settings", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-currency-settings"
                        : string.Equals(scenario, "currency-settings-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-currency-settings-dark"
                        : string.Equals(scenario, "currency-settings-english", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-currency-settings-english"
                        : string.Equals(scenario, "currency-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-currency-settings-english-dark"
                        : string.Equals(scenario, "language-settings", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-language-settings"
                        : string.Equals(scenario, "language-settings-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-language-settings-dark"
                        : string.Equals(scenario, "language-settings-english", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-language-settings-english"
                        : string.Equals(scenario, "language-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-language-settings-english-dark"
                        : string.Equals(scenario, "device-settings", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-device-settings"
                        : string.Equals(scenario, "device-settings-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-device-settings-dark"
                        : string.Equals(scenario, "device-settings-english", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-device-settings-english"
                        : string.Equals(scenario, "device-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-device-settings-english-dark"
                        : string.Equals(scenario, "tax-settings-readonly", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings"
                        : string.Equals(scenario, "tax-settings-readonly-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-dark"
                        : string.Equals(scenario, "tax-settings-readonly-english", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-english"
                        : string.Equals(scenario, "tax-settings-readonly-english-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-english-dark"
                        : string.Equals(scenario, "tax-settings", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings"
                        : string.Equals(scenario, "tax-settings-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-dark"
                        : string.Equals(scenario, "tax-settings-english", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-english"
                        : string.Equals(scenario, "tax-settings-english-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-tax-settings-english-dark"
                        : (string.Equals(scenario, "database-settings", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly", StringComparison.OrdinalIgnoreCase))
                            ? "--preview-database-settings-ui"
                        : (string.Equals(scenario, "database-settings-dark", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-dark", StringComparison.OrdinalIgnoreCase))
                            ? "--preview-database-settings-ui-dark"
                        : (string.Equals(scenario, "database-settings-english", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-english", StringComparison.OrdinalIgnoreCase))
                            ? "--preview-database-settings-ui-english"
                        : (string.Equals(scenario, "database-settings-english-dark", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-english-dark", StringComparison.OrdinalIgnoreCase))
                            ? "--preview-database-settings-ui-english-dark"
                    : string.Equals(scenario, "accounting-period", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-accounting-period-ui"
                    : string.Equals(scenario, "accounting-period-dark", StringComparison.OrdinalIgnoreCase)
                            ? "--preview-accounting-period-ui-dark"
                    : string.Equals(scenario, "accounting-period-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-period-ui-english"
                    : string.Equals(scenario, "accounting-period-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-period-ui-english-dark"
                    : string.Equals(scenario, "period-readiness", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-period-readiness-ui"
                    : string.Equals(scenario, "period-readiness-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-period-readiness-ui-dark"
                    : string.Equals(scenario, "period-readiness-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-period-readiness-ui-english"
                    : string.Equals(scenario, "period-readiness-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-period-readiness-ui-english-dark"
                    : string.Equals(scenario, "accounting-integrity", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-integrity-ui"
                    : string.Equals(scenario, "accounting-integrity-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-integrity-ui-dark"
                    : string.Equals(scenario, "accounting-integrity-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-integrity-ui-english"
                    : string.Equals(scenario, "accounting-integrity-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-accounting-integrity-ui-english-dark"
                    : string.Equals(scenario, "inventory-integrity", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-integrity-ui"
                    : string.Equals(scenario, "inventory-integrity-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-integrity-ui-dark"
                    : string.Equals(scenario, "inventory-integrity-english", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-integrity-ui-english"
                    : string.Equals(scenario, "inventory-integrity-english-dark", StringComparison.OrdinalIgnoreCase)
                        ? "--preview-inventory-integrity-ui-english-dark"
                            : string.Empty,
                UseShellExecute = false
            };
            application = Application.Launch(startInfo);
            using var automation = new UIA2Automation();

            application.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(15));
            Window? window = application.GetMainWindow(automation, TimeSpan.FromSeconds(15));
            Require(window != null, "ظهرت النافذة الرئيسية");
            if (string.Equals(scenario, "purchase-cancellation-live-qa", StringComparison.OrdinalIgnoreCase)) { return RunQaLivePurchaseCancellationScenario(application, automation, window!, artifactsDirectory, report); }
            if (string.Equals(scenario, "billing-live-qa", StringComparison.OrdinalIgnoreCase)) { return RunQaLiveBillingScenario(application, automation, window!, artifactsDirectory, report); }
            if (string.Equals(scenario, "qa-permission-home-state", StringComparison.OrdinalIgnoreCase))
            {
                return RunQaPermissionHomeStateScenario(application, automation, window!, artifactsDirectory, report);
            }
            if (string.Equals(scenario, "purchases-inputs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "purchases-inputs-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "purchases-inputs-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "purchases-inputs-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunPurchasesInputsScenario(application, window!, artifactsDirectory, report,
                    scenario.Contains("-english", StringComparison.OrdinalIgnoreCase),
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-operation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-operation-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryOperationScenario(application, window!, artifactsDirectory, report,
                    scenario.EndsWith("-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-movement", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-movement-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryMovementScenario(application, window!, artifactsDirectory, report,
                    scenario.EndsWith("-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-summary", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-summary-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventorySummaryScenario(application, window!, artifactsDirectory, report,
                    scenario.EndsWith("-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-stock-liveqa", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-lots-liveqa", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryStockLiveQaScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "inventory-lots-liveqa", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-movement-cancel-loading", StringComparison.OrdinalIgnoreCase))
                return RunInventoryMovementCancelLoadingScenario(application, window!, artifactsDirectory, report, cancellationSignalPath);
            if (string.Equals(scenario, "inventory-movement-liveqa", StringComparison.OrdinalIgnoreCase))
                return RunInventoryMovementLiveQaScenario(application, window!, artifactsDirectory, report);
            if (string.Equals(scenario, "inventory-integrity-liveqa", StringComparison.OrdinalIgnoreCase))
                return RunInventoryIntegrityLiveQaScenario(application, window!, artifactsDirectory, report);            if (scenario.EndsWith("-liveqa", StringComparison.OrdinalIgnoreCase) &&
                (scenario.StartsWith("inventory-summary", StringComparison.OrdinalIgnoreCase) ||
                 scenario.StartsWith("inventory-turnover", StringComparison.OrdinalIgnoreCase) ||
                 scenario.StartsWith("inventory-alerts", StringComparison.OrdinalIgnoreCase)))
            {
                return RunInventoryOperationalLiveQaScenario(application, window!, artifactsDirectory, report,
                    scenario.Substring("inventory-".Length).Replace("-liveqa", string.Empty, StringComparison.OrdinalIgnoreCase));
            }            if (scenario.StartsWith("inventory-stock", StringComparison.OrdinalIgnoreCase) ||
                scenario.StartsWith("inventory-lots", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryStockScenario(application, window!, artifactsDirectory, report,
                    scenario.StartsWith("inventory-lots", StringComparison.OrdinalIgnoreCase),
                    scenario.Contains("-english", StringComparison.OrdinalIgnoreCase),
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-turnover", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-turnover-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryTurnoverScenario(application, window!, artifactsDirectory, report,
                    scenario.EndsWith("-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "quick-search", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quick-search-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quick-search-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quick-search-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunQuickSearchScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "medicine-editor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "medicine-editor-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "medicine-editor-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "medicine-editor-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunMedicineEditorScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "agent-password", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "agent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "agent-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "agent-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "agent-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunAgentPasswordScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "billing-customer-type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "billing-customer-type-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "billing-customer-type-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "billing-customer-type-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunBillingCustomerTypeScenario(application, window!, artifactsDirectory, report,
                    scenario.Contains("-english", StringComparison.OrdinalIgnoreCase),
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "antdui-arabic", StringComparison.OrdinalIgnoreCase))
            {
                return RunAntdArabicScenario(application, window!, artifactsDirectory, report);
            }
            if (string.Equals(scenario, "settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "settings-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunSettingsScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "settings-english", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("operational-report", StringComparison.OrdinalIgnoreCase))
            {
                return RunOperationalReportScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "system-monitor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "system-monitor-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunSystemMonitorScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "system-monitor-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "inventory-alerts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-alerts-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunInventoryAlertsScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "inventory-alerts-english", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("report-print-options", StringComparison.OrdinalIgnoreCase))
            {
                return RunReportPrintOptionsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("report-columns", StringComparison.OrdinalIgnoreCase))
            {
                return RunReportColumnsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("report-template-name", StringComparison.OrdinalIgnoreCase))
            {
                return RunReportTemplateNameScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "excel-import", StringComparison.OrdinalIgnoreCase))
            {
                return RunExcelImportScenario(application, window!, artifactsDirectory, report);
            }
            if (string.Equals(scenario, "excel-export", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "excel-export-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "excel-export-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "excel-export-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunExcelExportScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("invoice-drafts-source", StringComparison.OrdinalIgnoreCase))
                return RunInvoiceSourceEntryScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (scenario.StartsWith("invoice-drafts", StringComparison.OrdinalIgnoreCase))
                return RunInvoiceDraftPreviewScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase),
                    scenario.IndexOf("direct", StringComparison.OrdinalIgnoreCase) >= 0);
            if (scenario.StartsWith("cashboxes", StringComparison.OrdinalIgnoreCase))
                return RunCashBoxPreviewScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "active-ingredients", StringComparison.OrdinalIgnoreCase))
            {
                return RunActiveIngredientsScenario(application, window!, artifactsDirectory, report);
            }
            if (scenario.StartsWith("customer-management", StringComparison.OrdinalIgnoreCase))
            {
                return RunCustomerManagementScenario(application, window!, artifactsDirectory, report, scenario.Contains("english"), scenario.EndsWith("dark"));
            }
            if (scenario.StartsWith("medicine-catalog-import", StringComparison.OrdinalIgnoreCase))
                return RunMedicineCatalogImportScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (scenario.StartsWith("medicine-catalog-errors", StringComparison.OrdinalIgnoreCase))
                return RunMedicineCatalogIssuesScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "language-settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "language-settings-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "language-settings-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "language-settings-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunLanguageSettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "currency-settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "currency-settings-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "currency-settings-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "currency-settings-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunCurrencySettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "device-settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "device-settings-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "device-settings-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "device-settings-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunDeviceSettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("tax-settings-readonly", StringComparison.OrdinalIgnoreCase))
            {
                return RunTaxReadOnlyCapture(application, window!, artifactsDirectory, report,
                    scenario.Contains("-english", StringComparison.OrdinalIgnoreCase),
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "tax-settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "tax-settings-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "tax-settings-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "tax-settings-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunTaxSettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if ((string.Equals(scenario, "database-settings", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly", StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(scenario, "database-settings-dark", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-dark", StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(scenario, "database-settings-english", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-english", StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(scenario, "database-settings-english-dark", StringComparison.OrdinalIgnoreCase) || string.Equals(scenario, "database-settings-readonly-english-dark", StringComparison.OrdinalIgnoreCase)))
                return RunDatabaseSettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase),
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.StartsWith("database-settings-readonly", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "accounting-period", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-period-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-period-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-period-english-dark", StringComparison.OrdinalIgnoreCase))
                return RunAccountingPeriodScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "period-readiness", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "period-readiness-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "period-readiness-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "period-readiness-english-dark", StringComparison.OrdinalIgnoreCase))
                return RunPeriodReadinessScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "accounting-integrity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-integrity-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-integrity-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "accounting-integrity-english-dark", StringComparison.OrdinalIgnoreCase))
                return RunIntegrityScenario(application, window!, artifactsDirectory, report, true,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "inventory-integrity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-integrity-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-integrity-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "inventory-integrity-english-dark", StringComparison.OrdinalIgnoreCase))
                return RunIntegrityScenario(application, window!, artifactsDirectory, report, false,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(scenario, "saudi-settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "saudi-settings-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "saudi-settings-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "saudi-settings-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunSaudiSettingsScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "pharmacy-profile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "pharmacy-profile-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "pharmacy-profile-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "pharmacy-profile-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunPharmacyProfileScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "bootstrap-manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "bootstrap-manager-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "bootstrap-manager-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "bootstrap-manager-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunBootstrapManagerScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "commercial-return", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "commercial-return-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "commercial-return-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "commercial-return-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunCommercialReturnScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "return-history", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "return-history-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "return-history-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "return-history-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunReturnHistoryScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "quarantine-resolution", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quarantine-resolution-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quarantine-resolution-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "quarantine-resolution-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunQuarantineResolutionScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "permissions", StringComparison.OrdinalIgnoreCase))
            {
                return RunPermissionsScenario(application, window!, artifactsDirectory, report);
            }
            if (string.Equals(scenario, "manufacturer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "manufacturer-dark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "manufacturer-english", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "manufacturer-english-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunManufacturerScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("-english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "quick-journal", StringComparison.OrdinalIgnoreCase))
            {
                return RunQuickJournalScenario(application, window!, artifactsDirectory, report);
            }
            if (string.Equals(scenario, "journal-entry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "journal-entry-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunJournalEntryScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "journal-entry-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "voucher-actions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "voucher-actions-dark", StringComparison.OrdinalIgnoreCase))
            {
                return RunVoucherActionsScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "voucher-actions-dark", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "home", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "home-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunHomeScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "home-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "home-workspace", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "home-workspace-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunHomeWorkspaceScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "home-workspace-english", StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(scenario, "dashboard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, "dashboard-english", StringComparison.OrdinalIgnoreCase))
            {
                return RunDashboardScenario(application, window!, artifactsDirectory, report,
                    string.Equals(scenario, "dashboard-english", StringComparison.OrdinalIgnoreCase));
            }
            if (scenario.StartsWith("login", StringComparison.OrdinalIgnoreCase))
                return RunLoginScenario(application, window!, artifactsDirectory, report,
                    scenario.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0,
                    scenario.EndsWith("-dark", StringComparison.OrdinalIgnoreCase));
            throw new InvalidOperationException("سيناريو واجهة غير مدعوم: " + scenario);
        }
        catch (Exception exception)
        {
            report.Passed = false;
            report.ErrorType = exception.GetType().FullName;
            report.Error = exception.Message;
            report.CompletedAtUtc = DateTimeOffset.UtcNow;
            try
            {
                Directory.CreateDirectory(artifactsDirectory);
                WriteReport(artifactsDirectory, report);
            }
            catch
            {
            }

            Console.Error.WriteLine("FlaUI login smoke failed: " + exception);
            return 1;
        }
        finally
        {
            if (application != null)
            {
                try
                {
                    if (!application.HasExited) application.Close();
                }
                catch
                {
                    // A failed UI test must not leave a preview process that blocks the next build.
                }
                application.Dispose();
            }
        }
    }

    private static int RunQaPermissionHomeStateScenario(Application application, UIA2Automation automation,
        Window loginWindow, string artifactsDirectory, UiSmokeReport report)
    {
        string? testUser = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_USER");
        string? testPassword = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_PASSWORD");
        Require(!string.IsNullOrWhiteSpace(testUser) && !string.IsNullOrWhiteSpace(testPassword),
            "حساب QA المؤقت غير متاح للاختبار");
        Require(loginWindow.Title == "تسجيل الدخول", "بدأ اختبار الصلاحيات من شاشة الدخول");
        AutomationElement user = RequireElement(loginWindow, "username", "اسم مستخدم QA");
        AutomationElement password = RequireElement(loginWindow, "password", "كلمة مرور QA");
        AutomationElement? userEditor = user.FindAllDescendants()
            .FirstOrDefault(item => item.ControlType == ControlType.Edit && item.Patterns.Value.IsSupported);
        Require(userEditor != null, "محرر اسم المستخدم داخل ComboBox متاح");
        userEditor!.Patterns.Value.Pattern.SetValue(testUser!);
        string userValue = userEditor.Patterns.Value.Pattern.Value.Value;
        Require(userValue == testUser, "حقل اسم مستخدم QA احتفظ بالقيمة المدخلة");
        IntPtr passwordHandle = new IntPtr(password.Properties.NativeWindowHandle.Value);
        Require(passwordHandle != IntPtr.Zero, "حقل كلمة المرور يملك مقبض نافذة صالحًا");
        SendMessage(passwordHandle, 0x000C, IntPtr.Zero, testPassword!);
        Require(SendMessage(passwordHandle, 0x000E, IntPtr.Zero, IntPtr.Zero).ToInt32() == testPassword!.Length,
            "حقل كلمة مرور QA احتفظ بعدد المحارف المدخلة");
        AutomationElement loginButton = RequireElement(loginWindow, "button1", "زر الدخول");
        IntPtr loginHandle = new IntPtr(loginButton.Properties.NativeWindowHandle.Value);
        Require(loginButton.IsEnabled && loginHandle != IntPtr.Zero, "زر الدخول مفعّل وله مقبض صالح");
        loginWindow.Focus();
        loginButton.Click();

        Window? home = WaitForWindow(application, automation, "الرئيسية", TimeSpan.FromSeconds(20));
        Require(home != null, "تم دخول كاشير QA إلى الصفحة الرئيسية؛ النوافذ الحالية: " +
            string.Join(", ", application.GetAllTopLevelWindows(automation).Select(item => item.Title)));
        AutomationElement sales = RequireElement(home!, "label6", "بطاقة المبيعات");
        AutomationElement accounting = RequireElement(home!, "AccountingLabel", "بطاقة المحاسبة");
        string salesHint = sales.Properties.HelpText.Value ?? string.Empty;
        string accountingHint = accounting.Properties.HelpText.Value ?? string.Empty;
        Console.WriteLine("QA home access hints: Sales=" + salesHint + "; Accounting=" + accountingHint);
        Require(salesHint.Contains("لا تملك صلاحية فتح"),
            "منع المبيعات المحفوظ انعكس على بطاقة التنقل (الوصف: " + salesHint + ")");
        Require(accountingHint.Contains("فتح المحاسبة"),
            "سماح المحاسبة المحفوظ انعكس على بطاقة التنقل (الوصف: " + accountingHint + ")");

        home!.Focus();
        sales.Click();
        Thread.Sleep(300);
        Console.WriteLine("QA click windows: " + string.Join(" | ", automation.GetDesktop().FindAllChildren().Where(item => item.Properties.ProcessId.Value == application.ProcessId).Select(item => item.Name)));
        CaptureVisibleWindow(new IntPtr(home.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-home-after-sales-click.png"));
        Window? denied = home.FindFirstDescendant(cf => cf.ByName("الصلاحيات"))?.AsWindow()
            ?? WaitForWindow(application, automation, "الصلاحيات", TimeSpan.FromSeconds(5));
        Require(denied != null, "النقر على المبيعات الممنوعة أظهر رسالة الصلاحيات");
        Require(!application.GetAllTopLevelWindows(automation).Any(item => item.Title == "المبيعات"),
            "لم تفتح شاشة البيع بعد رفض الصلاحية");
        AutomationElement? acknowledge = denied!.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Button);
        Require(acknowledge != null, "زر إغلاق رسالة الصلاحيات متاح");
        acknowledge!.Click();
        for (int attempt = 0; attempt < 30 && !home.IsEnabled; attempt++) Thread.Sleep(100);
        Require(home.IsEnabled, "عادت الرئيسية بعد إغلاق رسالة الصلاحيات");
        home.Focus();
        AutomationElement content = RequireElement(home, "panelContent", "قائمة الأقسام");
        SendMessage(new IntPtr(content.Properties.NativeWindowHandle.Value), 0x0115, new IntPtr(7), IntPtr.Zero);
        Thread.Sleep(150);
        accounting.Click();
        Thread.Sleep(300);
        CaptureVisibleWindow(new IntPtr(home.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-after-accounting-click.png"));
        Window? accountingWindow = home.FindFirstDescendant(cf => cf.ByName("المحاسبة").And(cf.ByControlType(ControlType.Window)))?.AsWindow() ?? WaitForWindow(application, automation, "المحاسبة", TimeSpan.FromSeconds(10));
        Require(accountingWindow != null, "النقر على المحاسبة المسموحة فتح الشاشة فعليًا");
        accountingWindow!.Focus();
        Thread.Sleep(500);
        AutomationElement internalPayment = accountingWindow.FindFirstDescendant(cf => cf.ByName("سند صرف داخلي"))!;
        Require(internalPayment != null && !internalPayment.IsOffscreen, "بطاقة سند الصرف الداخلي ظاهرة في شجرة الواجهة");
        CaptureVisibleWindow(new IntPtr(accountingWindow.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-accounting-home-svg.png"));
        Mouse.MoveTo(new Point((int)accountingWindow.BoundingRectangle.Left + 30, (int)accountingWindow.BoundingRectangle.Top + 50));
        Thread.Sleep(300);
        CaptureVisibleWindow(new IntPtr(accountingWindow.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-accounting-no-hover.png"));
        AutomationElement journalTab = RequireElement(accountingWindow, "accountingPageTab2", "تبويب القيود");
        journalTab.Focus();
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(250);
        Require((journalTab.Properties.HelpText.Value ?? string.Empty).StartsWith("التبويب الحالي."), "Enter يفعّل تبويب القيود");
        AutomationElement reportsTab = RequireElement(accountingWindow, "accountingPageTab3", "تبويب التقارير");
        reportsTab.Focus();
        Keyboard.Press(VirtualKeyShort.SPACE);
        Thread.Sleep(250);
        Require((reportsTab.Properties.HelpText.Value ?? string.Empty).StartsWith("التبويب الحالي."), "Space يفعّل تبويب التقارير");
        CaptureVisibleWindow(new IntPtr(accountingWindow.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-accounting-reports-svg.png"));
        accountingWindow.Close();
        for (int attempt = 0; attempt < 30 && !home.IsEnabled; attempt++) Thread.Sleep(100);
        Require(home.IsEnabled, "عادت الرئيسية مفعّلة بعد إغلاق المحاسبة");
        home.Focus();
        AutomationElement inventory = RequireElement(home, "InventoryLabel", "بطاقة المخازن");
        inventory.Click();
        Window? inventoryWindow = WaitForWindow(application, automation, "المخازن", TimeSpan.FromSeconds(10));
        Require(inventoryWindow != null, "فتح المخازن المسموح من الصفحة الرئيسية");
        inventoryWindow!.Focus();
        AutomationElement adjustment = inventoryWindow.FindFirstDescendant(cf => cf.ByName("تسوية المخزون"))!;
        Require(adjustment != null && (adjustment.Properties.HelpText.Value ?? string.Empty).StartsWith("لا تملك صلاحية"),
            "تسوية المخزون تعرض منع العملية للكاشير قبل النقر");
        AutomationElement stockTab = RequireElement(inventoryWindow, "inventoryPageTab2", "عمليات المخزون");
        stockTab.Focus();
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(250);
        Require((stockTab.Properties.HelpText.Value ?? string.Empty).StartsWith("التبويب الحالي."), "Enter يفعّل عمليات المخزون");
        foreach (string title in new[] { "مخزون أول المدة", "التوريد المخزني", "الصرف المخزني" })
        {
            AutomationElement operation = inventoryWindow.FindFirstDescendant(cf => cf.ByName(title))!;
            Require(operation != null && (operation.Properties.HelpText.Value ?? string.Empty).StartsWith("لا تملك صلاحية"),
                "بطاقة العملية تعرض الصلاحية المطلوبة: " + title);
        }
        CaptureVisibleWindow(new IntPtr(inventoryWindow.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-inventory-operation-permissions.png"));
        AutomationElement trackingTab = RequireElement(inventoryWindow, "inventoryPageTab1", "متابعة المخزون");
        trackingTab.Focus();
        Keyboard.Press(VirtualKeyShort.SPACE);
        Thread.Sleep(250);
        Require((trackingTab.Properties.HelpText.Value ?? string.Empty).StartsWith("التبويب الحالي."), "Space يعيد تقارير المخزون");
        AutomationElement stockCard = inventoryWindow.FindFirstDescendant(cf => cf.ByName("جرد التشغيلات والصلاحيات"))!;
        Require(stockCard != null && !stockCard.IsOffscreen, "بطاقة جرد التشغيلات ظاهرة");
        Mouse.MoveTo(new Point((int)(stockCard!.BoundingRectangle.Left + stockCard.BoundingRectangle.Width / 2), (int)(stockCard.BoundingRectangle.Top + stockCard.BoundingRectangle.Height / 2)));
        Thread.Sleep(300);
        CaptureVisibleWindow(new IntPtr(inventoryWindow.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "qa-inventory-hover.png"));
        inventoryWindow.Close();
        for (int attempt = 0; attempt < 30 && !home.IsEnabled; attempt++) Thread.Sleep(100);
        Require(home.IsEnabled, "عادت الرئيسية بعد إغلاق المخازن");
        home!.Close();
        loginWindow.Close();
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "qa-permission-home-state-report.json");
        Console.WriteLine("FlaUI QA permission home state passed. Saved deny/grant verified through actual tile clicks.");
        return 0;
    }

    private static Window? WaitForOwnedWindow(Window owner, string title, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Window? found = owner.FindFirstDescendant(factory => factory.ByName(title).And(factory.ByControlType(ControlType.Window)))?.AsWindow();
            if (found != null) return found;
            Thread.Sleep(200);
        }
        return null;
    }
    private static Window? WaitForWindow(Application application, UIA2Automation automation,
        string title, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Window? found = application.GetAllTopLevelWindows(automation)
                .FirstOrDefault(window => string.Equals(window.Title, title, StringComparison.Ordinal));
            if (found != null) return found;
            Thread.Sleep(200);
        }
        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr first, string second);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr first, IntPtr second);

    private static int RunExcelExportScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english, bool dark)
    {
        string expectedTitle = english ? "Excel center" : "مركز Excel";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal), "فُتحت معاينة تصدير Excel المعزولة باللغة المطلوبة");
        window.Focus();
        AutomationElement source = RequireElement(window, "excelTransferSource", "نوع البيانات");
        AutomationElement search = RequireElement(window, "excelTransferSearch", "البحث");
        AutomationElement from = RequireElement(window, "excelTransferFrom", "بداية الفترة");
        AutomationElement to = RequireElement(window, "excelTransferTo", "نهاية الفترة");
        AutomationElement export = RequireElement(window, "excelTransferExport", "تصدير إكسل");
        AutomationElement import = RequireElement(window, "excelTransferImport", "فتح الاستيراد");
        AutomationElement close = RequireElement(window, "excelTransferClose", "إغلاق");
        AutomationElement status = RequireElement(window, "excelTransferStatus", "حالة المعاينة");
        Require(source.Name == (english ? "Data type" : "نوع البيانات"), "تُرجمت تسمية مصدر البيانات");
        Require(search.Name == (english ? "Optional search" : "بحث اختياري"), "تُرجمت تسمية البحث");
        Require(from.Name == (english ? "From date" : "من تاريخ") &&
                to.Name == (english ? "To date" : "إلى تاريخ"), "تُرجمت حقول الفترة");
        Require(status.Name.Contains(english ? "Export preview" : "معاينة التصدير", StringComparison.Ordinal),
            "تعرض حالة المعاينة بلغة الشاشة");
        Require(!export.IsEnabled && !import.IsEnabled && close.IsEnabled,
            "المعاينة تمنع التصدير والاستيراد وتتيح إغلاق الشاشة");
        Require(export.BoundingRectangle.Height >= 44 && import.BoundingRectangle.Height >= 44 &&
                close.BoundingRectangle.Height >= 44,
            "أهداف إجراءات التصدير لا تقل عن 44 بكسل");
        Require(english
                ? export.BoundingRectangle.Left < close.BoundingRectangle.Left
                : export.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات التصدير من حافة القراءة الصحيحة");
        Require(search.Patterns.Value.IsSupported, "حقل البحث قابل للتحرير عبر واجهة الوصول");
        search.Patterns.Value.Pattern.SetValue("باراسيتامول 500 mg");
        close.Focus(); // A direction change after editing can recreate the native TextBox handle.
        Thread.Sleep(150);
        search = RequireElement(window, "excelTransferSearch", "البحث بعد تغيير اتجاه المحتوى");
        Require(search.Patterns.Value.Pattern.Value.Value == "باراسيتامول 500 mg",
            "احتفظ البحث بالنص العربي واللاتيني المختلط حرفيًا");
        string screenshotPath = Path.Combine(artifactsDirectory,
            "excel-export-" + (english ? "english" : "arabic") + (dark ? "-dark" : "-light") + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي لشاشة تصدير Excel");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة التصدير دون قراءة أو كتابة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report,
            "excel-export-" + (english ? "english" : "arabic") + (dark ? "-dark" : "-light") + "-report.json");
        Console.WriteLine("FlaUI Excel export preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunActiveIngredientsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report)
    {
        Require(window.Title == "إدارة المواد الفعالة ومصادرها", "فُتحت معاينة المواد الفعالة المعزولة");
        window.Focus();
        RequireElement(window, "activeIngredientsGrid", "جدول المواد الفعالة");
        RequireElement(window, "activeIngredientEvidenceGrid", "جدول المراجع");
        RequireElement(window, "activeIngredientMappingsGrid", "جدول الربط");
        AutomationElement date = RequireElement(window, "activeIngredientRetrievedOn", "تاريخ الاطلاع الاختياري");
        Require(date.Patterns.Value.IsSupported, "تاريخ الاطلاع يتيح تحرير AntdUI عبر واجهة الوصول");
        date.Patterns.Value.Pattern.SetValue("2026/09/23");
        Thread.Sleep(150);
        Require(date.Patterns.Value.Pattern.Value.Value == "2026/09/23",
            "احتفظ تاريخ الاطلاع الاختياري بالقيمة المدخلة");
        AutomationElement add = RequireElement(window, "activeIngredientAdd", "إضافة مادة ومصدرها");
        AutomationElement refresh = RequireElement(window, "activeIngredientRefresh", "تحديث");
        AutomationElement close = RequireElement(window, "activeIngredientClose", "إغلاق");
        Require(!add.IsEnabled && refresh.IsEnabled && close.IsEnabled,
            "المعاينة تمنع الحفظ وتبقي التحديث والإغلاق متاحين");
        ActivateButton(refresh);
        Thread.Sleep(120);
        AutomationElement status = RequireElement(window, "activeIngredientStatus", "حالة معاينة المواد الفعالة");
        Require(status.Name.Contains("معاينة", StringComparison.Ordinal),
            "تحديث المعاينة لم يفتح اتصال قاعدة البيانات");
        report.ScreenshotPath = null;
        AutomationElement closeAfterRefresh = RequireElement(window, "activeIngredientClose", "إغلاق بعد التحديث");
        Require(closeAfterRefresh.IsEnabled, "يبقى إغلاق المعاينة متاحًا بعد التحديث");
        ActivateButton(closeAfterRefresh);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة المواد الفعالة دون SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "active-ingredients-report.json");
        Console.WriteLine("FlaUI active ingredient preview and save guard passed.");
        return 0;
    }

    private static int RunExcelImportScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report)
    {
        Require(string.Equals(window.Title, "استيراد البيانات", StringComparison.Ordinal), "فُتحت معاينة استيراد Excel المعزولة");
        RequireElement(window, "csvImportKind", "نوع بيانات الاستيراد");
        RequireElement(window, "qaCsvPreviewGrid", "جدول معاينة الاستيراد");
        AutomationElement choose = RequireElement(window, "chooseCsvImport", "اختيار ملف الاستيراد");
        AutomationElement approve = RequireElement(window, "executeCsvImport", "اعتماد الاستيراد");
        AutomationElement close = RequireElement(window, "closeCsvImport", "إغلاق الاستيراد");
        Require(!choose.IsEnabled && !approve.IsEnabled && close.IsEnabled,
            "وضع المعاينة يعطل اختيار الملف والاعتماد ويبقي الإغلاق متاحًا");
        string screenshotPath = Path.Combine(artifactsDirectory, "excel-import-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة معاينة الاستيراد");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة الاستيراد دون قراءة أو كتابة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "excel-import-report.json");
        Console.WriteLine("FlaUI Excel import preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static void VerifyInvoiceColumnDirection(Window window, bool english)
    {
        AutomationElement grid = RequireElement(window, "invoiceDraftGrid", "جدول الاستيراد");
        AutomationElement? kind = grid.FindFirstDescendant(cf => cf.ByName(english ? "Kind" : "النوع"));
        AutomationElement? currency = grid.FindFirstDescendant(cf => cf.ByName(english ? "Currency" : "العملة"));
        Require(kind != null && currency != null && (english
            ? kind.BoundingRectangle.Left < currency.BoundingRectangle.Left
            : kind.BoundingRectangle.Left > currency.BoundingRectangle.Left),
            "أعمدة الفاتورة تبدأ من يمين العربية ويسار الإنجليزية");
    }
    private static int RunInvoiceSourceEntryScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        AutomationElement source = RequireElement(window, "invoiceDraftSystemInput", "النظام المصدر");
        AutomationElement label = RequireElement(window, "invoiceDraftSystemLabel", "عنوان النظام المصدر");
        AutomationElement close = RequireElement(window, "invoiceDraftClose", "إغلاق");
        Require(source.IsEnabled && RequireElement(window, "invoiceDraftSystemFrame", "إطار المصدر").BoundingRectangle.Height >= 44,
            "حقل المصدر العربي متاح داخل إطار AntdUI مناسب للتحرير");
        Require(english ? label.BoundingRectangle.Left < source.BoundingRectangle.Left
            : label.BoundingRectangle.Left > source.BoundingRectangle.Left,
            "حقل المصدر وعنوانه يتبعان اتجاه اللغة");
        VerifyInvoiceColumnDirection(window, english);
        string mixed = "نظام الصيدلية — Legacy 2026";
        Require(PromoteForScreenCapture(application.MainWindowHandle), "معاينة المصدر ظاهرة قبل إدخال لوحة المفاتيح");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            window.Focus();
            source.Click();
            source.Focus();
            Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && source.Properties.HasKeyboardFocus.Value) break;
        }
        Require(GetForegroundWindow() == application.MainWindowHandle && source.Properties.HasKeyboardFocus.Value,
            "نافذة الاختبار تملك الإدخال والحقل يملك التركيز قبل إرسال المفاتيح");
        TypeInvariantUnicodeText(mixed);
        // Changing native RTL direction recreates the editor's HWND. UIA references
        // must be acquired again after edits, including clearing and pasting text.
        string ReadSourceText()
        {
            source = RequireElement(window, "invoiceDraftSystemInput", "المصدر بعد التحرير");
            return source.AsTextBox().Text;
        }
        void RequireSourceFocus()
        {
            source = RequireElement(window, "invoiceDraftSystemInput", "المصدر قبل إرسال المفاتيح");
            Require(GetForegroundWindow() == application.MainWindowHandle && source.Properties.HasKeyboardFocus.Value,
                "نافذة المعاينة والحقل يحتفظان بملكية الإدخال");
        }
        for (int attempt = 0; attempt < 20 && ReadSourceText() != mixed; attempt++) Thread.Sleep(50);
        Require(ReadSourceText() == mixed, "حُفظ النص العربي المختلط حرفيًا؛ لا تُسجل قيمة حقل سطح المكتب عند الفشل.");
        RequireSourceFocus();
        Keyboard.Type(VirtualKeyShort.END);
        Keyboard.Type(VirtualKeyShort.BACK);
        Thread.Sleep(100);
        Require(ReadSourceText() == mixed.Substring(0, mixed.Length - 1),
            "End ثم الحذف يتبعان النص المحفوظ");
        RequireSourceFocus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Type(VirtualKeyShort.KEY_Z);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(100);
        Require(ReadSourceText() == mixed, "التراجع يعيد النص العربي المختلط");
        RequireSourceFocus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Type(VirtualKeyShort.KEY_A);
        Keyboard.Type(VirtualKeyShort.KEY_X);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(100);
        Require(ReadSourceText() == string.Empty, "تحديد النص وقصه يفرغان الحقل");
        RequireSourceFocus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Type(VirtualKeyShort.KEY_V);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(100);
        Require(ReadSourceText() == mixed, "اللصق يعيد النص المحفوظ دون تغيير");
        RequireSourceFocus();
        Keyboard.Type(VirtualKeyShort.TAB);
        for (int attempt = 0; attempt < 20 && source.Properties.HasKeyboardFocus.Value; attempt++) Thread.Sleep(50);
        Require(!source.Properties.HasKeyboardFocus.Value, "Tab يغادر حقل المصدر");
        foreach (Size size in new[] { new Size(680, 560), new Size(1000, 700) })
        {
            Require(SetWindowPos(application.MainWindowHandle, IntPtr.Zero, 0, 0, size.Width, size.Height, 0x0002 | 0x0004),
                "يمكن تغيير مقاس نافذة الاستيراد");
            Thread.Sleep(150);
            source = RequireElement(window, "invoiceDraftSystemInput", "المصدر بعد تغيير المقاس");
            Require(RequireElement(window, "invoiceDraftSystemFrame", "إطار المصدر بعد تغيير المقاس").BoundingRectangle.Height >= 44 && source.BoundingRectangle.Width >= 160 &&
                source.AsTextBox().Text == mixed && !source.IsOffscreen,
                "حقل المصدر يحافظ على النص والمقاس المناسب بعد تغيير النافذة");
        }
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshot = Path.Combine(artifactsDirectory, "invoice-source-" + suffix + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshot);
        report.ScreenshotPath = screenshot;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة المصدر دون كتابة");
        report.Passed = true; report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "invoice-source-" + suffix + ".json");
        Console.WriteLine("PASS: AntdUI-framed native input preserves Arabic mixed text, End/delete/undo/cut/paste/Tab, direction and resize; no workbook or SQL writes.");
        return 0;
    }

    private static int RunInvoiceDraftPreviewScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark, bool direct)
    {
        Require(window.Title == (english ? "Import and review Excel invoices" : "استيراد ومراجعة فواتير Excel"),
            "فُتحت معاينة مسودات الفواتير باللغة المختارة");
        AutomationElement grid = RequireElement(window, "invoiceDraftGrid", "جدول الفواتير");
        AutomationElement source = RequireElement(window, "invoiceDraftSystemInput", "النظام المصدر");
        AutomationElement choose = RequireElement(window, "invoiceDraftChoose", "اختيار ملف");
        AutomationElement template = RequireElement(window, "invoiceDraftTemplate", "قالب");
        AutomationElement save = RequireElement(window, "invoiceDraftSave", "حفظ مسودات");
        AutomationElement directChoice = RequireElement(window, "invoiceDraftApproveMode", "اعتماد مباشر");
        AutomationElement draftChoice = RequireElement(window, "invoiceDraftReviewMode", "حفظ كمسودة");
        AutomationElement close = RequireElement(window, "invoiceDraftClose", "إغلاق");
        AutomationElement status = RequireElement(window, "invoiceDraftStatus", "الحالة");
        VerifyInvoiceColumnDirection(window, english);
        Require(grid.IsEnabled && source.Name == (english ? "Source system" : "النظام المصدر"),
            "حقل المصدر وجدول المعاينة واضحان");
        Require(!choose.IsEnabled && !template.IsEnabled && !save.IsEnabled && close.IsEnabled,
            "المعاينة لا تختار ملفًا ولا تحفظ في قاعدة البيانات");
        Require(directChoice.IsEnabled && draftChoice.IsEnabled &&
            directChoice.BoundingRectangle.Height >= 44 && draftChoice.BoundingRectangle.Height >= 44 &&
            directChoice.Name == (english ? "Approve now" : "اعتماد مباشر") &&
            draftChoice.Name == (english ? "Save as draft" : "حفظ كمسودة"),
            "قرار الاعتماد أو المسودة ظاهر بزرين من AntdUI بعد معاينة الملف");
        Require(save.Name == (direct ? (english ? "Approve selected" : "اعتماد المحددة") :
            (english ? "Save drafts" : "حفظ كمسودات")), "نمط إجراء الملف واضح ولا يخلط المسودة بالاعتماد");
        if (direct)
        {
            ActivateButton(draftChoice);
            Require(save.Name == (english ? "Save drafts" : "حفظ كمسودات"),
                "تغيير النمط بعد المعاينة يعرض حفظ المسودات دون اعتماد");
            ActivateButton(directChoice);
            Require(save.Name == (english ? "Approve selected" : "اعتماد المحددة"),
                "العودة للاعتماد المباشر تحافظ على إجراء الفاتورة المحددة");
            Require(PromoteForScreenCapture(application.MainWindowHandle), "نافذة الفاتورة ظاهرة لاختبار لوحة المفاتيح");
            window.Focus();
            draftChoice.Focus();
            for (int attempt = 0; attempt < 10 && !draftChoice.Properties.HasKeyboardFocus.Value; attempt++) Thread.Sleep(50);
            Require(draftChoice.Properties.HasKeyboardFocus.Value, "زر المسودة يملك التركيز");
            Keyboard.Type(VirtualKeyShort.RETURN);
            for (int attempt = 0; attempt < 10 && save.Name != (english ? "Save drafts" : "حفظ كمسودات"); attempt++) Thread.Sleep(50);
            Require(save.Name == (english ? "Save drafts" : "حفظ كمسودات"),
                "Enter يختار المسودة بعد المعاينة");
            directChoice = RequireElement(window, "invoiceDraftApproveMode", "اعتماد مباشر بعد تبديل النمط");
            directChoice.Focus();
            for (int attempt = 0; attempt < 10 && !directChoice.Properties.HasKeyboardFocus.Value; attempt++) Thread.Sleep(50);
            Require(directChoice.Properties.HasKeyboardFocus.Value, "زر الاعتماد يملك التركيز");
            Keyboard.Type(VirtualKeyShort.RETURN);
            for (int attempt = 0; attempt < 10 && save.Name != (english ? "Approve selected" : "اعتماد المحددة"); attempt++) Thread.Sleep(50);
            Require(save.Name == (english ? "Approve selected" : "اعتماد المحددة"),
                "Enter يعيد الاعتماد المباشر بعد المعاينة");
        }
        Require(status.Name.Contains(english ? "Visual preview" : "معاينة مرئية", StringComparison.Ordinal),
            "حالة المعاينة معلنة بلغتها");
        Require(new[] { choose, template, save, close }.All(action => action.BoundingRectangle.Height >= 44),
            "أهداف إجراءات المسودات لا تقل عن 44 بكسل");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light") + (direct ? "-direct" : "");
        string screenshotPath = Path.Combine(artifactsDirectory, "invoice-drafts-" + suffix + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة شاشة المسودات");
        report.ScreenshotPath = screenshotPath;
        Require(PromoteForScreenCapture(application.MainWindowHandle), "معاينة الفواتير ظاهرة قبل تحديد الصف");
        window.Focus();
        AutomationElement review = RequireElement(window, "invoiceDraftOpen", "مراجعة البنود");
        grid = RequireElement(window, "invoiceDraftGrid", "جدول الفواتير بعد تبديل النمط");
        // WinForms exposes rows as Unknown under UIA; use the row's own bounds,
        // never derive the click position from an unrelated button's height.
        AutomationElement? firstRow = grid.FindAllDescendants().FirstOrDefault(node =>
            node.Properties.Name.ValueOrDefault == "Row 0" || node.Properties.Name.ValueOrDefault == "صف 0");
        Require(firstRow != null, "ظهر صف الفاتورة الأول في شجرة الوصول");
        var rowBounds = firstRow!.BoundingRectangle;
        var rowPoint = new NativePoint { X = (int)(rowBounds.Left + rowBounds.Width / 2),
            Y = (int)(rowBounds.Top + rowBounds.Height / 2) };
        Require(LogicalToPhysicalPointForPerMonitorDPI(application.MainWindowHandle, ref rowPoint),
            "تحويل نقطة صف WinForms إلى إحداثيات الشاشة الفعلية");
        Mouse.Click(new Point(rowPoint.X, rowPoint.Y));
        for (int attempt = 0; attempt < 10 && !review.IsEnabled; attempt++) Thread.Sleep(50);
        Require(review.IsEnabled, "يمكن مراجعة بنود الفاتورة المختارة قبل حفظها");
        ActivateButton(review);
        AutomationElement metadata = RequireElement(window, "invoiceDraftMetadata", "بيانات الفاتورة");
        Require(metadata.AsTextBox().Text.Contains("C-001", StringComparison.Ordinal) &&
            metadata.AsTextBox().Text.Contains("2.5", StringComparison.Ordinal), "تظهر هوية الطرف والخصم في المراجعة");
        string detailScreenshot = Path.Combine(artifactsDirectory, "invoice-draft-detail-" + suffix + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, detailScreenshot);
        Require(File.Exists(detailScreenshot) && new FileInfo(detailScreenshot).Length > 0,
            "حُفظت صورة تفاصيل الفاتورة");
        ActivateButton(review);
        ActivateButton(RequireElement(window, "invoiceDraftSavedTab", "المسودات المحفوظة"));
        AutomationElement open = RequireElement(window, "invoiceDraftOpen", "فتح المسودة");
        Require(!open.IsEnabled, "فتح المسودة يتطلب تحديد صف من القائمة");
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة المسودات دون اتصال أو كتابة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "invoice-drafts-" + suffix + ".json");
        Console.WriteLine("FlaUI invoice drafts preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunCashBoxPreviewScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(window.Title == (english ? "Cashboxes" : "إدارة صناديق النقد"),
            "عنوان شاشة الصناديق يطابق اتجاه اللغة");
        AutomationElement grid = RequireElement(window, "cashBoxGrid", "جدول الصناديق");
        AutomationElement code = RequireElement(window, "cashBoxCode", "رمز الصندوق");
        AutomationElement name = RequireElement(window, "cashBoxName", "اسم الصندوق");
        AutomationElement status = RequireElement(window, "cashBoxStatus", "حالة المعاينة");
        AutomationElement codeLabel = RequireElement(window, "cashBoxCodeLabel", "عنوان رمز الصندوق");
        AutomationElement nameLabel = RequireElement(window, "cashBoxNameLabel", "عنوان اسم الصندوق");
        AutomationElement employeeLabel = RequireElement(window, "cashBoxEmployeeLabel", "عنوان الكاشير");
        bool readingOrder = english
            ? codeLabel.BoundingRectangle.Left < nameLabel.BoundingRectangle.Left &&
              nameLabel.BoundingRectangle.Left < employeeLabel.BoundingRectangle.Left
            : codeLabel.BoundingRectangle.Left > nameLabel.BoundingRectangle.Left &&
              nameLabel.BoundingRectangle.Left > employeeLabel.BoundingRectangle.Left;
        Require(readingOrder, "ترتيب حقول الصناديق يطابق اتجاه القراءة");
        Require(grid.IsEnabled && code.IsEnabled && name.IsEnabled,
            "تعريفات الصناديق مرئية في وضع المعاينة");
        Require(status.Name.Contains(english ? "Visual preview" : "معاينة مرئية", StringComparison.Ordinal),
            "المعاينة لا تتصل بقاعدة البيانات");
        foreach (string id in new[] { "cashBoxSave", "cashBoxImport", "cashBoxExport" })
            Require(!RequireElement(window, id, "إجراء معاينة معطل").IsEnabled,
                "أزرار الحفظ والاستيراد والتصدير معطلة في المعاينة");
        AutomationElement close = RequireElement(window, "cashBoxClose", "إغلاق");
        AutomationElement create = RequireElement(window, "cashBoxNew", "جديد");
        Require(english ? create.BoundingRectangle.Left < close.BoundingRectangle.Left
            : create.BoundingRectangle.Left > close.BoundingRectangle.Left,
            "إجراءات التذييل تبدأ من جهة القراءة");
        Require(close.BoundingRectangle.Height >= 44, "هدف إغلاق الصناديق أصغر من 44 بكسل");
        Require(PromoteForScreenCapture(application.MainWindowHandle), "معاينة الصناديق ظاهرة قبل تحديد الصف");
        window.Focus();
        AutomationElement? firstRow = grid.FindAllDescendants().FirstOrDefault(node =>
            node.Properties.Name.ValueOrDefault == "Row 0" || node.Properties.Name.ValueOrDefault == "صف 0");
        Require(firstRow != null, "ظهر صف الصندوق الأول في شجرة الوصول");
        var bounds = firstRow!.BoundingRectangle;
        var point = new NativePoint { X = (int)(bounds.Left + bounds.Width / 2),
            Y = (int)(bounds.Top + bounds.Height / 2) };
        Require(LogicalToPhysicalPointForPerMonitorDPI(application.MainWindowHandle, ref point),
            "تحويل نقطة صف الصندوق إلى إحداثيات الشاشة الفعلية");
        Mouse.Click(new Point(point.X, point.Y));
        for (int attempt = 0; attempt < 10 && code.AsTextBox().Text != "MAIN-1"; attempt++) Thread.Sleep(50);
        Require(code.AsTextBox().Text == "MAIN-1", "اختيار الصندوق يملأ محرر التعريف");
        ActivateButton(create);
        // ClearEditor can recreate WinForms editor handles. UIA references from
        // before the click are no longer reliable even though the fields remain.
        code = RequireElement(window, "cashBoxCode", "رمز الصندوق بعد جديد");
        name = RequireElement(window, "cashBoxName", "اسم الصندوق بعد جديد");
        Require(string.IsNullOrEmpty(code.AsTextBox().Text),
            "جديد يفرغ محرر الصندوق دون إعادة اختيار الصف تلقائيًا");
        code.AsTextBox().Text = "BOX-002";
        name = RequireElement(window, "cashBoxName", "اسم الصندوق بعد تحرير الرمز");
        name.Focus();
        string mixedName = "صندوق المبيعات — Cash 02";
        Keyboard.Type(mixedName);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            name = RequireElement(window, "cashBoxName", "اسم الصندوق أثناء التحرير");
            if (name.AsTextBox().Text == mixedName) break;
            Thread.Sleep(50);
        }
        code = RequireElement(window, "cashBoxCode", "رمز الصندوق بعد التحرير");
        name = RequireElement(window, "cashBoxName", "اسم الصندوق بعد التحرير");
        Require(code.AsTextBox().Text == "BOX-002" && name.AsTextBox().Text == mixedName,
            "حقول الصناديق تحفظ الرمز والنص العربي المختلط حرفيًا في المعاينة: code='" +
            code.AsTextBox().Text + "', name='" + name.AsTextBox().Text + "'");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "cashboxes-" + suffix + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "صورة الصناديق لم تحفظ");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "معاينة الصناديق لم تغلق");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "cashboxes-" + suffix + ".json");
        Console.WriteLine("FlaUI cashboxes preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunMedicineCatalogImportScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        string expectedTitle = english ? "Import medicine definitions from Excel" : "استيراد ملف إكسل لتعريفات الأصناف";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتحت معاينة استيراد تعريفات الأصناف");
        AutomationElement grid = RequireElement(window, "catalogImportGrid", "جدول الأصناف");
        AutomationElement fileName = RequireElement(window, "catalogImportFileName", "حالة اختيار الملف");
        AutomationElement status = RequireElement(window, "catalogImportStatus", "حالة استيراد الأصناف");
        AutomationElement choose = RequireElement(window, "catalogImportChoose", "اختيار ملف");
        AutomationElement template = RequireElement(window, "catalogImportTemplate", "تنزيل قالب");
        AutomationElement approve = RequireElement(window, "catalogImportApprove", "اعتماد الأصناف");
        AutomationElement details = RequireElement(window, "catalogImportErrors", "تفاصيل الأخطاء");
        AutomationElement close = RequireElement(window, "catalogImportClose", "إغلاق");
        Require(!choose.IsEnabled && !template.IsEnabled && !approve.IsEnabled && !details.IsEnabled && close.IsEnabled,
            "المعاينة المعزولة لا تفتح ملفًا أو تكتب صنفًا");
        Require(grid.IsEnabled && fileName.Name == (english ? "No file selected." : "لم يتم اختيار ملف."),
            "تعرض المعاينة والملف بلغتها الصحيحة");
        Require(status.Name.Contains(english ? "Visual preview" : "معاينة مرئية", StringComparison.Ordinal),
            "تعلن حالة المعاينة بلغتها الصحيحة");
        Require(new[] { choose, template, approve, details, close }.All(action => action.BoundingRectangle.Height >= 44),
            "أهداف إجراءات استيراد الأصناف لا تقل عن 44 بكسل");
        Require(english
                ? choose.BoundingRectangle.Left < close.BoundingRectangle.Left
                : choose.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات استيراد الأصناف من حافة القراءة الصحيحة");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "medicine-catalog-import-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي لاستيراد تعريفات الأصناف");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة استيراد الأصناف");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "medicine-catalog-import-" + suffix + "-report.json");
        Console.WriteLine("FlaUI medicine catalog import preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunMedicineCatalogIssuesScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "File validation errors" : "تفاصيل أخطاء الملف", StringComparison.Ordinal),
            "فُتحت معاينة قائمة أخطاء الأصناف المعزولة");
        AutomationElement grid = RequireElement(window, "catalogIssuesGrid", "قائمة الأخطاء");
        Require(grid.FindAllDescendants().Length >= 3, "جدول الأخطاء يكشف صفوفًا لقارئ الشاشة");
        AutomationElement summary = RequireElement(window, "catalogIssuesSummary", "عدد الأخطاء");
        Require(summary.Name.Contains("3") && grid.IsEnabled &&
                grid.Name == (english ? "Medicine import errors" : "قائمة أخطاء استيراد الأصناف"),
            "جدول الأخطاء الثلاثة متاح للقراءة وبلغته الصحيحة");
        AutomationElement close = RequireElement(window, "catalogIssuesClose", "إغلاق تفاصيل الأخطاء");
        Require(close.IsEnabled && close.BoundingRectangle.Height >= 44 &&
                close.Name == (english ? "Close" : "إغلاق"),
            "زر إغلاق تفاصيل الأخطاء متاح ومترجم");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "medicine-catalog-errors-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي لتفاصيل أخطاء الأصناف");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت قائمة أخطاء الأصناف");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "medicine-catalog-errors-" + suffix + "-report.json");
        Console.WriteLine("FlaUI medicine catalog issues preview passed. Screenshot=" + screenshotPath);
        return 0;
    }
    private static int RunReportPrintOptionsScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Report print options" : "خيارات طباعة التقرير", StringComparison.Ordinal),
            "فُتحت معاينة خيارات الطباعة المعزولة باللغة المطلوبة");
        foreach (string fieldId in new[] { "reportPrintOrientation", "reportPrintDensity", "reportPrintCompanyName",
            "reportPrintTaxNumber", "reportPrintRegistration", "reportPrintMobile", "reportPrintAddress",
            "reportPrintWebsite", "reportPrintLogoUnavailable" })
            RequireElement(window, fieldId, "حقل خيار الطباعة " + fieldId);
        AutomationElement orientation = RequireElement(window, "reportPrintOrientation", "اتجاه التقرير");
        AutomationElement density = RequireElement(window, "reportPrintDensity", "تفصيل التقرير");
        Require(orientation.Name == (english ? "Report orientation" : "اتجاه التقرير") &&
                density.Name == (english ? "Report detail" : "تفصيل التقرير"),
            "تُرجمت أسماء وصول اختيارات الطباعة");
        var companyChoiceElement = RequireElement(window, "reportPrintCompanyName", "خيار اسم المنشأة");
        Require(companyChoiceElement.Name == (english ? "Company name" : "اسم المنشأة"),
            "تُرجم خيار اسم المنشأة");
        var companyChoice = companyChoiceElement.AsCheckBox();
        Require(companyChoice.IsChecked == true, "اسم المنشأة محدد افتراضيًا");
        companyChoice.IsChecked = false;
        Require(companyChoice.IsChecked == false, "يمكن تعطيل اسم المنشأة عبر واجهة الوصول");
        companyChoice.IsChecked = true;
        Require(companyChoice.IsChecked == true, "يمكن إعادة تحديد اسم المنشأة عبر واجهة الوصول");
        AutomationElement preview = RequireElement(window, "reportPrintPreview", "زر معاينة الطباعة");
        AutomationElement cancel = RequireElement(window, "reportPrintCancel", "زر إلغاء خيارات الطباعة");
        Require(preview.IsEnabled && cancel.IsEnabled &&
                preview.Name == (english ? "Print preview" : "معاينة الطباعة") &&
                cancel.Name == (english ? "Cancel" : "إلغاء") &&
                preview.BoundingRectangle.Height >= 44 && cancel.BoundingRectangle.Height >= 44,
            "إجراءات خيارات الطباعة متاحة ومترجمة");
        Require(english
                ? preview.BoundingRectangle.Left < cancel.BoundingRectangle.Left
                : preview.BoundingRectangle.Right > cancel.BoundingRectangle.Right,
            "تبدأ إجراءات خيارات الطباعة من حافة القراءة الصحيحة");
        ActivateButton(preview);
        WaitForAccessibleName(window, "reportPrintStatus", english ? "Preview only" : "معاينة فقط",
            TimeSpan.FromSeconds(3), "تأكيد منع الطباعة في المعاينة");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "report-print-options-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة خيارات الطباعة");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(cancel);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت خيارات الطباعة دون تشغيل طابعة أو قاعدة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "report-print-options-" + suffix + "-report.json");
        Console.WriteLine("FlaUI report print options passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunReportColumnsScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(window.Title == (english ? "Customize report columns" : "تخصيص أعمدة التقرير"),
            "فُتحت نافذة تخصيص أعمدة التقرير باللغة المطلوبة");
        AutomationElement list = RequireElement(window, "reportColumns", "قائمة أعمدة التقرير");
        Require(list.FindAllDescendants().Length >= 4, "تعرض قائمة الأعمدة أربعة خيارات للقراءة");
        AutomationElement moveUp = RequireElement(window, "moveReportColumnUp", "نقل العمود لأعلى");
        AutomationElement moveDown = RequireElement(window, "moveReportColumnDown", "نقل العمود لأسفل");
        AutomationElement showAll = RequireElement(window, "showAllReportColumns", "إظهار جميع الأعمدة");
        AutomationElement apply = RequireElement(window, "applyReportColumns", "تطبيق تخصيص الأعمدة");
        AutomationElement cancel = RequireElement(window, "cancelReportColumns", "إلغاء تخصيص الأعمدة");
        Require(moveUp.Name == (english ? "Move the selected column up" : "نقل العمود المحدد لأعلى") &&
                moveDown.Name == (english ? "Move the selected column down" : "نقل العمود المحدد لأسفل") &&
                showAll.Name == (english ? "Show all report columns" : "إظهار كل أعمدة التقرير"),
            "تُرجمت أسماء وصول إجراءات ترتيب الأعمدة");
        Require(apply.IsEnabled && cancel.IsEnabled && apply.BoundingRectangle.Height >= 44 && cancel.BoundingRectangle.Height >= 44,
            "إجراءات تخصيص الأعمدة متاحة بمساحة تفاعل مناسبة");
        Require(english ? apply.BoundingRectangle.Left < cancel.BoundingRectangle.Left
                        : apply.BoundingRectangle.Right > cancel.BoundingRectangle.Right,
            "تبدأ إجراءات تخصيص الأعمدة من حافة القراءة الصحيحة");
        ActivateButton(showAll);
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "report-columns-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة تخصيص الأعمدة");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(apply);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "طُبّق تخطيط الأعمدة في الذاكرة وأُغلقت المعاينة دون كتابة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "report-columns-" + suffix + "-report.json");
        Console.WriteLine("FlaUI report columns preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunReportTemplateNameScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(window.Title == (english ? "Save report template" : "حفظ قالب التقرير"),
            "فُتحت نافذة اسم قالب التقرير باللغة المطلوبة");
        AutomationElement editor = RequireElement(window, "reportTemplateName", "اسم قالب التقرير");
        const string mixedName = "قالب Daily 500";
        editor.Patterns.Value.Pattern.SetValue(mixedName);
        Require(editor.Patterns.Value.Pattern.Value.Value == mixedName,
            "احتفظ اسم القالب بالنص العربي والإنجليزي حرفيًا");
        AutomationElement save = RequireElement(window, "saveReportTemplate", "حفظ قالب التقرير");
        AutomationElement cancel = RequireElement(window, "cancelReportTemplate", "إلغاء حفظ قالب التقرير");
        Require(save.Name == (english ? "Save report template" : "حفظ قالب التقرير") &&
                cancel.Name == (english ? "Cancel saving the report template" : "إلغاء حفظ قالب التقرير") &&
                save.BoundingRectangle.Height >= 44 && cancel.BoundingRectangle.Height >= 44,
            "إجراءات اسم القالب مترجمة وبمساحة تفاعل مناسبة");
        Require(english ? save.BoundingRectangle.Left < cancel.BoundingRectangle.Left
                        : save.BoundingRectangle.Right > cancel.BoundingRectangle.Right,
            "تبدأ إجراءات اسم القالب من حافة القراءة الصحيحة");
        save.Focus();
        Thread.Sleep(150);
        Require(editor.Patterns.Value.Pattern.Value.Value == mixedName,
            "بقي اسم القالب ثابتًا بعد نقل التركيز إلى إجراء الحفظ");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "report-template-name-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة اسم قالب التقرير");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(save);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُعيد اسم القالب من الذاكرة وأُغلقت المعاينة دون كتابة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "report-template-name-" + suffix + "-report.json");
        Console.WriteLine("FlaUI report template name preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunLoginScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Sign in" : "تسجيل الدخول", StringComparison.Ordinal),
            "فُتحت شاشة الدخول باللغة المطلوبة");
        AutomationElement card = RequireElement(window, "loginCard", "بطاقة الدخول");
        AutomationElement hero = RequireElement(window, "loginHero", "لوحة الترحيب");
        AutomationElement username = RequireElement(window, "username", "حقل اسم المستخدم");
        AutomationElement password = RequireElement(window, "password", "حقل كلمة المرور");
        AutomationElement remember = RequireElement(window, "chkRememberMe", "خيار تذكر المستخدم");
        AutomationElement login = RequireElement(window, "button1", "زر الدخول");
        AutomationElement passwordVisibility = RequireElement(window, "passwordVisibilityButton", "زر إظهار كلمة المرور");
        AutomationElement close = RequireElement(window, "button3", "زر الإغلاق");
        AutomationElement databaseSettings = RequireElement(window, "databaseSettingsButton", "زر إعداد قاعدة البيانات");

        Require(username.IsEnabled && password.IsEnabled && login.IsEnabled, "حقول الدخول والإجراء الأساسي مفعلة");
        Require(username.Name == (english ? "Username" : "اسم المستخدم") &&
                password.Name == (english ? "Password" : "كلمة المرور") &&
                login.Name == (english ? "Sign in to the system" : "دخول إلى النظام") &&
                close.Name == (english ? "Close application" : "إغلاق التطبيق") &&
                databaseSettings.Name == (english ? "Database settings" : "إعداد قاعدة البيانات") &&
                remember.Name == (english ? "Remember username" : "تذكر اسم المستخدم"),
            "أسماء الوصول لشاشة الدخول مترجمة");
        Require(login.BoundingRectangle.Height >= 44 && close.BoundingRectangle.Height >= 44 &&
                databaseSettings.BoundingRectangle.Height >= 44 && passwordVisibility.BoundingRectangle.Height >= 44,
            "إجراءات الدخول تحقق هدف التفاعل الموحد");
        Require(english ? card.BoundingRectangle.Left < hero.BoundingRectangle.Left
                        : card.BoundingRectangle.Right > hero.BoundingRectangle.Right,
            "بطاقة الدخول تبدأ من حافة القراءة الصحيحة");

        const string mixedUser = "مدير Admin";
        username.Patterns.Value.Pattern.SetValue(mixedUser);
        Require(username.Patterns.Value.Pattern.Value.Value == mixedUser, "حُفظ اسم المستخدم المختلط حرفيًا");
        Require(passwordVisibility.Name == (english ? "Show password" : "إظهار كلمة المرور"), "حالة إظهار كلمة المرور واضحة");
        ActivateButton(passwordVisibility);
        Thread.Sleep(150);
        Require(passwordVisibility.Name == (english ? "Hide password" : "إخفاء كلمة المرور"), "زر كلمة المرور يعلن حالة الإظهار");
        ActivateButton(passwordVisibility);
        Thread.Sleep(150);
        Require(passwordVisibility.Name == (english ? "Show password" : "إظهار كلمة المرور"), "زر كلمة المرور يعود إلى حالة الإخفاء");

        ActivateButton(databaseSettings);
        Thread.Sleep(150);
        AutomationElement subtitle = RequireElement(window, "loginSubtitle", "حالة الدخول");
        Require(subtitle.Name.StartsWith(english ? "Preview only" : "معاينة فقط", StringComparison.Ordinal),
            "إعدادات قاعدة البيانات محمية داخل المعاينة");
        ActivateWithKeyboard(window, login);
        Thread.Sleep(150);
        subtitle = RequireElement(window, "loginSubtitle", "حالة الدخول بعد إجراء المصادقة");
        Require(subtitle.Name.StartsWith(english ? "Preview only" : "معاينة فقط", StringComparison.Ordinal),
            "المصادقة محمية داخل المعاينة");
        Require(!application.HasExited, "بقيت شاشة الدخول مفتوحة بعد إجراء المعاينة");

        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "login-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لشاشة الدخول");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة الدخول من زر الإغلاق");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "login-" + suffix + "-report.json");
        Console.WriteLine("FlaUI login preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunOperationalReportScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Operational report builder" : "منشئ التقارير التشغيلي", StringComparison.Ordinal),
            "فُتحت معاينة التقارير المعزولة باللغة المطلوبة");
        foreach (string controlId in new[] { "operationalReportSource", "operationalReportSearch",
            "operationalReportFrom", "operationalReportTo", "operationalReportTemplates", "operationalReportGrid" })
            RequireElement(window, controlId, "عنصر التقرير " + controlId);
        foreach (string actionId in new[] { "saveReportTemplate", "applyReportTemplate", "customizeReportColumns",
            "deleteReportTemplate", "showOperationalReport", "exportOperationalCsv", "printOperationalReport", "closeOperationalReport" })
        {
            AutomationElement action = RequireElement(window, actionId, "إجراء التقرير " + actionId);
            Require(action.IsEnabled && action.BoundingRectangle.Height >= 44, "إجراء التقرير متاح بمساحة مناسبة: " + actionId);
        }
        AutomationElement source = RequireElement(window, "operationalReportSource", "مصدر التقرير");
        AutomationElement search = RequireElement(window, "operationalReportSearch", "بحث التقرير");
        AutomationElement show = RequireElement(window, "showOperationalReport", "عرض التقرير");
        AutomationElement close = RequireElement(window, "closeOperationalReport", "إغلاق التقرير");
        Require(source.Name == (english ? "Report source" : "مصدر التقرير") &&
                search.Name == (english ? "Search report results" : "بحث في نتائج التقرير") &&
                show.Name == (english ? "View report results" : "عرض نتائج التقرير") &&
                close.Name == (english ? "Close the operational report builder" : "إغلاق منشئ التقارير التشغيلي"),
            "تُرجمت أسماء الوصول للمرشحات والإجراءات");
        const string mixedSearch = "باراسيتامول 500 mg";
        search.Patterns.Value.Pattern.SetValue(mixedSearch);
        Require(search.Patterns.Value.Pattern.Value.Value == mixedSearch, "احتفظ بحث التقرير بالنص المختلط حرفيًا");
        Console.WriteLine("Operational action bounds: show={0}; close={1}; direction={2}",
            show.BoundingRectangle, close.BoundingRectangle, english ? "LTR" : "RTL");
        Require(english ? show.BoundingRectangle.Left < close.BoundingRectangle.Left
                        : show.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات التقرير من حافة القراءة الصحيحة");
        ActivateWithKeyboard(window, RequireElement(window, "showOperationalReport", "عرض التقرير"));
        ActivateWithKeyboard(window, RequireElement(window, "exportOperationalCsv", "تصدير التقرير"));
        AutomationElement status = RequireElement(window, "operationalReportStatus", "حالة التقرير");
        Console.WriteLine("Operational status after guarded export: {0}", status.Name);
        Require(status.Name.StartsWith(english ? "Preview only" : "معاينة فقط", StringComparison.Ordinal),
            "تظهر حماية المعاينة باللغة المطلوبة دون إنشاء ملف");
        Require(!application.HasExited, "معاينة التقرير بقيت مفتوحة دون حفظ ملف أو قراءة قاعدة البيانات");
        string suffix = (english ? "english" : "arabic") + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory, "operational-report-" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة نافذة التقرير");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(RequireElement(window, "closeOperationalReport", "إغلاق التقرير"));
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة التقرير بزر التذييل");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "operational-report-" + suffix + "-report.json");
        Console.WriteLine("FlaUI operational report interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunSystemMonitorScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english)
    {
        Require(window.Title == (english ? "System monitoring" : "مراقبة النظام"),
            "فُتحت معاينة مراقبة النظام بالاتجاه المطلوب");
        Require(PromoteForScreenCapture(application.MainWindowHandle),
            "نافذة المراقبة الأصلية ظاهرة لالتقاط الصور وإدخال المفاتيح");
        Require(window.FindFirstDescendant(cf => cf.ByAutomationId("__UiStatusStrip")) == null,
            "المعاينة المحلية لا تعرض شريط جاهزية قاعدة البيانات");
        void PressOwnedKey(AutomationElement control, VirtualKeyShort key)
        {
            control.Focus();
            for (int attempt = 0; attempt < 10 && !control.Properties.HasKeyboardFocus.Value; attempt++)
                Thread.Sleep(50);
            Require(GetForegroundWindow() == application.MainWindowHandle &&
                control.Properties.HasKeyboardFocus.Value,
                "نافذة المراقبة والعنصر يملكان التركيز قبل إدخال المفتاح");
            Keyboard.Type(key);
            Thread.Sleep(150);
        }
        string[] statuses = { "summaryLabel", "auditLabel", "inventoryLabel" };
        string[] grids = { "alertsGrid", "auditGrid", "inventoryGrid" };
        string suffix = english ? "english" : "arabic";
        foreach (var size in new[] { (Width: 760, Height: 540), (Width: 1080, Height: 680) })
        {
            Require(window.Patterns.Transform.IsSupported, "يمكن تغيير مقاس نافذة المراقبة");
            window.Patterns.Transform.Pattern.Resize(size.Width, size.Height);
            Thread.Sleep(200);
            for (int index = 0; index < 3; index++)
            {
                AutomationElement tab = RequireElement(window, "monitorPageTab" + index, "تبويب المراقبة الأصلي");
                Require(tab.IsEnabled && !tab.IsOffscreen && !string.IsNullOrWhiteSpace(tab.Name) &&
                    window.BoundingRectangle.Contains(tab.BoundingRectangle),
                    "زر التبويب ظاهر وله اسم وصول داخل النافذة");
                PressOwnedKey(tab, index % 2 == 0 ? VirtualKeyShort.RETURN : VirtualKeyShort.SPACE);
                AutomationElement status = RequireElement(window, statuses[index], "حالة الصفحة المختارة");
                AutomationElement grid = RequireElement(window, grids[index], "جدول الصفحة المختارة");
                Require(!status.IsOffscreen && !grid.IsOffscreen &&
                    !string.IsNullOrWhiteSpace(status.Name) && grid.BoundingRectangle.Height >= 120,
                    "المفتاح نقل العرض إلى الحالة والجدول الصحيحين");
                string screenshot = Path.Combine(artifactsDirectory,
                    "monitor-" + suffix + "-" + size.Width + "-page" + index + ".png");
                CaptureVisibleWindow(application.MainWindowHandle, screenshot);
                report.ScreenshotPath = screenshot;
            }
        }
        PressOwnedKey(RequireElement(window, "monitorPageTab0", "تبويب التنبيهات"), VirtualKeyShort.RETURN);
        AutomationElement refresh = RequireElement(window, "refreshButton", "تحديث المراقبة");
        AutomationElement close = RequireElement(window, "closeButton", "إغلاق المراقبة");
        Require(refresh.IsEnabled && close.IsEnabled &&
            refresh.Name == (english ? "Refresh system monitoring data" : "تحديث بيانات مراقبة النظام"),
            "إجراءات المراقبة لها أسماء وصول صحيحة");
        PressOwnedKey(refresh, VirtualKeyShort.RETURN);
        Require(RequireElement(window, "summaryLabel", "حالة التحديث").Name.Contains(
            english ? "Preview only" : "المعاينة للعرض فقط"),
            "تحديث المعاينة لا يقرأ قاعدة البيانات ويظهر الحالة باللغة المختارة");
        PressOwnedKey(close, VirtualKeyShort.RETURN);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة المراقبة دون اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report,
            english ? "system-monitor-english-report.json" : "system-monitor-report.json");
        Console.WriteLine("FlaUI monitor preview passed: owned native Enter/Space, three pages, two window sizes, screenshots, refresh and close; no SQL or authenticated Login.");
        return 0;
    }
    private static int RunInventoryAlertsScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english)
    {
        Require(window.Title == (english ? "Inventory alerts" : "تنبيهات المخزون"),
            "فُتحت معاينة تنبيهات المخزون بالاتجاه المطلوب");
        AutomationElement stock = RequireElement(window, "inventoryAlertsLowStock", "حد المخزون المنخفض");
        AutomationElement days = RequireElement(window, "inventoryAlertsExpiringDays", "أيام الصلاحية");
        AutomationElement refresh = RequireElement(window, "inventoryAlertsRefresh", "تحديث التنبيهات");
        AutomationElement print = RequireElement(window, "inventoryAlertsPrint", "طباعة التنبيهات");
        AutomationElement export = RequireElement(window, "inventoryAlertsExport", "تصدير التنبيهات");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة تنبيهات المخزون");
        Require(stock.Patterns.Value.IsSupported && days.Patterns.Value.IsSupported &&
            refresh.IsEnabled && !print.IsEnabled && !export.IsEnabled,
            "حقول التنبيهات قابلة للوصول والطباعة والتصدير معطلتان عند غياب النتائج");
        stock.Patterns.Value.Pattern.SetValue("10");
        days.Patterns.Value.Pattern.SetValue("abc");
        ActivateButton(refresh);
        Thread.Sleep(250);
        Require(status.Name.Contains(english ? "Enter a valid value" : "أدخل قيمة صحيحة"),
            "رُفض الإدخال غير الرقمي برسالة تتبع لغة الشاشة");
        days.Patterns.Value.Pattern.SetValue("3651");
        ActivateButton(refresh);
        Thread.Sleep(250);
        Require(status.Name.Contains(english ? "between 0 and 3650" : "بين 0 و3650"),
            "رُفض حد الصلاحية غير المسموح قبل قراءة SQL");
        days.Patterns.Value.Pattern.SetValue("30");
        ActivateButton(refresh);
        Thread.Sleep(250);
        Require(status.Name.Contains(english ? "no database data was read" : "لم تُقرأ قاعدة البيانات"),
            "التحديث الصحيح في المعاينة بقي معزولًا عن SQL");
        window.Close();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة تنبيهات المخزون دون SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report,
            english ? "inventory-alerts-english-report.json" : "inventory-alerts-report.json");
        Console.WriteLine("FlaUI inventory alerts thresholds and preview guard passed.");
        return 0;
    }

    private static int RunSettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english)
    {
        string expectedTitle = english ? "Settings" : "الإعدادات";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal), "فُتحت معاينة الإعدادات المعزولة باللغة المطلوبة");
        AutomationElement screenTitle = RequireElement(window, "lblTitle", "عنوان الإعدادات");
        AutomationElement screenSubtitle = RequireElement(window, "lblSubtitle", "وصف الإعدادات");
        Require(string.Equals(screenTitle.Name, expectedTitle, StringComparison.Ordinal), "عنوان الإعدادات مترجم");
        Require(string.Equals(screenSubtitle.Name, english ? "General system settings" : "إعدادات النظام العامة", StringComparison.Ordinal),
            "وصف الإعدادات مترجم");
        string[] cardActions =
        {
            "antdOpen_basic",
            "antdOpen_tax",
            "antdOpen_currency",
            "antdOpen_language",
            "antdOpen_devices",
            "antdOpen_compliance",
            "antdOpen_excel",
            "antdOpen_excel-import"
        };

        foreach (string automationId in cardActions)
        {
            AutomationElement action = RequireElement(window, automationId, "إجراء بطاقة الإعدادات " + automationId);
            Require(action.IsEnabled && action.BoundingRectangle.Height >= 44,
                "إجراء الإعدادات مفعّل وبمساحة تفاعل مناسبة: " + automationId);
            ActivateWithKeyboard(window, action);
            Thread.Sleep(100);
        }

        AutomationElement importAction = RequireElement(window, "antdOpen_excel-import", "بطاقة استيراد الأصناف");
        ActivateButton(importAction);
        string expectedHint = (english ? "Preview status: " : "حالة المعاينة: ") + importAction.Name;
        AutomationElement hint = WaitForAccessibleName(window, "lblHint", expectedHint,
            TimeSpan.FromSeconds(3), "رسالة معاينة بطاقة استيراد الأصناف");
        string suffix = english ? "settings-english" : "settings";
        string screenshotPath = Path.Combine(artifactsDirectory, suffix + "-window.png");
        CaptureDashboardWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لشاشة الإعدادات");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        Thread.Sleep(400);
        Require(application.HasExited, "أُغلقت معاينة الإعدادات دون حفظ بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, suffix + "-report.json");
        Console.WriteLine("FlaUI settings bilingual interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }
    private static int RunCurrencySettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Currency settings" : "إعدادات العملات", StringComparison.Ordinal),
            "فُتحت معاينة إعدادات العملات المعزولة");
        AutomationElement title = RequireElement(window, "lblTitle", "عنوان شاشة العملات");
        AutomationElement editorTitle = RequireElement(window, "currencyEditorTitle", "عنوان بيانات العملة");
        AutomationElement country = RequireElement(window, "txtCountry", "حقل الدولة");
        AutomationElement code = RequireElement(window, "txtCurrencyCode", "حقل رمز العملة");
        AutomationElement grid = RequireElement(window, "gridCurrencies", "جدول العملات");
        AutomationElement systemStatus = RequireElement(window, "lblSystemCurrency", "حالة عملة النظام");
        AutomationElement save = RequireElement(window, "btnSave", "حفظ العملة");
        AutomationElement delete = RequireElement(window, "btnDelete", "حذف العملة");
        AutomationElement setSystem = RequireElement(window, "btnSetSystem", "تعيين عملة النظام");
        AutomationElement clear = RequireElement(window, "btnClear", "إضافة عملة جديدة");

        Require(string.Equals(title.Name, english ? "Currencies and countries" : "العملات والدول", StringComparison.Ordinal) &&
            string.Equals(editorTitle.Name, english ? "Currency details" : "بيانات العملة", StringComparison.Ordinal),
            "العناوين المرئية تتبع لغة عرض العملات");
        Require(country.Patterns.Value.IsSupported && code.Patterns.Value.IsSupported && grid.IsEnabled,
            "بيانات العملة التجريبية متاحة للقراءة والتحرير المحلي");
        Require(!save.IsEnabled && !delete.IsEnabled && !setSystem.IsEnabled && clear.IsEnabled,
            "المعاينة تعطل كل عمليات الحفظ والحذف وتبقي إجراء الجديد محليًا");
        Require(systemStatus.Name.Contains(english ? "Preview only" : "معاينة فقط", StringComparison.Ordinal),
            "حالة المعاينة مكشوفة لقارئ الشاشة");
        Require(country.Patterns.Value.Pattern.Value == "اليمن" && code.Patterns.Value.Pattern.Value == "YER",
            "صف عملة النظام يملأ المحرر من بيانات وهمية فقط");

        window.Focus();
        Thread.Sleep(250);
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "currency-settings" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لإعدادات العملات");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        Thread.Sleep(400);
        Require(application.HasExited, "أُغلقت معاينة العملات دون اتصال أو حفظ");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "currency-settings" + suffix + "-report.json");
        Console.WriteLine("FlaUI currency settings preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunLanguageSettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool englishPresentation, bool dark)
    {
        Require(string.Equals(window.Title, englishPresentation ? "Language center" : "مركز اللغة", StringComparison.Ordinal), "فُتحت معاينة مركز اللغة المعزولة");
        AutomationElement arabic = RequireElement(window, "languageArabicOption", "خيار اللغة العربية");
        AutomationElement englishChoice = RequireElement(window, "languageEnglishOption", "خيار اللغة الإنجليزية");
        AutomationElement title = RequireElement(window, "languageTitle", "عنوان مركز اللغة");
        AutomationElement caption = RequireElement(window, "languageEnglishCaption", "حالة خيار الإنجليزية");
        AutomationElement status = RequireElement(window, "languageStatus", "حالة توفر اللغة");
        AutomationElement save = RequireElement(window, "languageSaveButton", "زر حفظ اللغة");
        AutomationElement close = RequireElement(window, "languageCloseButton", "زر إغلاق مركز اللغة");

        Require(arabic.IsEnabled && string.Equals(arabic.Name, englishPresentation ? "Select Arabic" : "اختيار اللغة العربية", StringComparison.Ordinal), "العربية متاحة ومترجمة في معاينة مركز اللغة");
        Require(!englishChoice.IsEnabled, "الإنجليزية لا تزال محجوبة حتى تكتمل الترجمة");
        Require(!save.IsEnabled, "الحفظ معطّل صراحةً في معاينة مركز اللغة");
        Require(string.Equals(title.Name, englishPresentation ? "Interface language" : "لغة الواجهة", StringComparison.Ordinal) &&
                string.Equals(caption.Name, englishPresentation ? "English is in progress" : "الإنجليزية قيد الإعداد", StringComparison.Ordinal),
            "عنوان وخيار الإنجليزية يتبعان لغة العرض");
        Require(status.Name.StartsWith(englishPresentation ? "Language availability:" : "حالة توفر اللغة:", StringComparison.Ordinal), "حالة اللغة مكشوفة لقارئ الشاشة");
        Require(string.Equals(close.Name, englishPresentation ? "Close language center" : "إغلاق مركز اللغة", StringComparison.Ordinal), "زر إغلاق مركز اللغة له اسم وصول واضح");

        window.Focus();
        Thread.Sleep(250);
        string suffix = (englishPresentation ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "language-settings" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لمركز اللغة");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        Thread.Sleep(400);
        Require(application.HasExited, "أُغلقت معاينة مركز اللغة دون حفظ تفضيلات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "language-settings" + suffix + "-report.json");
        Console.WriteLine("FlaUI language settings preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunDeviceSettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Device center" : "مركز الأجهزة", StringComparison.Ordinal), "فُتحت معاينة مركز الأجهزة المعزولة");
        AutomationElement preferredPrinter = RequireElement(window, "preferredPrinter", "قائمة الطابعة المفضلة");
        AutomationElement receiptPaper = RequireElement(window, "receiptPaperFormat", "قائمة مقاس الإيصال");
        AutomationElement receiptDropdown = RequireElement(window, "receiptPaperFormatDropdown", "سهم مقاس الإيصال AntdUI");
        AutomationElement invoicePaper = RequireElement(window, "invoicePrintFormat", "قائمة مقاس الفاتورة");
        AutomationElement status = RequireElement(window, "printerStatus", "حالة الطابعة");
        AutomationElement save = RequireElement(window, "saveDeviceSettings", "زر حفظ إعدادات الأجهزة");
        AutomationElement check = RequireElement(window, "checkPreferredPrinter", "زر فحص الطابعة");
        AutomationElement close = RequireElement(window, "closeDeviceSettings", "زر إغلاق مركز الأجهزة");

        Require(preferredPrinter.IsEnabled && receiptPaper.IsEnabled && invoicePaper.IsEnabled, "خيارات الطابعة ومقاسَي الإيصال والفاتورة متاحة في المعاينة");
        Require(!save.IsEnabled, "الحفظ معطّل صراحةً في معاينة مركز الأجهزة");
        Require(check.IsEnabled, "فحص الطابعة متاح من دون إرسال مهمة طباعة");
        Require(string.Equals(close.Name, english ? "Close device center" : "إغلاق مركز الأجهزة", StringComparison.Ordinal), "زر إغلاق مركز الأجهزة له اسم وصول واضح");
        Require(status.Name.StartsWith(english ? "Printer status:" : "حالة الطابعة:", StringComparison.Ordinal), "حالة الطابعة مكشوفة لقارئ الشاشة");

        window.Focus();
        ActivateButton(receiptDropdown);
        Thread.Sleep(150);
        Keyboard.Press(VirtualKeyShort.DOWN);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(150);
        Require((receiptPaper.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains("58mm", StringComparison.Ordinal),
            "سهم AntdUI يختار مقاس 58mm في القائمة الأصلية دون تغيير تفضيلات المحطة");

        Thread.Sleep(250);
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "device-settings" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لمركز الأجهزة");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        Thread.Sleep(400);
        Require(application.HasExited, "أُغلقت معاينة مركز الأجهزة دون حفظ تفضيلات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "device-settings" + suffix + "-report.json");
        Console.WriteLine("FlaUI device settings preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunTaxReadOnlyCapture(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english, bool dark)
    {
        Require(window.Title == (english ? "Accounting and tax settings" : "إعدادات الحسابات والضرائب"),
            "فُتحت معاينة الضريبة باللغة المطلوبة");
        AutomationElement country = RequireElement(window, "cmbTaxCountry", "بلد الضريبة");
        for (int attempt = 0; attempt < 30; attempt++)
        {
            country = RequireElement(window, "cmbTaxCountry", "بلد الضريبة");
            if (country.IsEnabled && country.BoundingRectangle.Width > 50 &&
                window.BoundingRectangle.Contains(country.BoundingRectangle)) break;
            Thread.Sleep(100);
        }
        AutomationElement dropdown = RequireElement(window, "taxCountryDropdown", "سهم البلد");
        AutomationElement save = RequireElement(window, "btnSaveTaxSettings", "الحفظ");
        AutomationElement cancel = RequireElement(window, "btnCancelTaxSettings", "الإلغاء");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        report.ScreenshotPath = Path.Combine(artifactsDirectory, "tax-settings-readonly" + suffix + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, report.ScreenshotPath);
        Require(country.IsEnabled && country.BoundingRectangle.Width > 50 &&
            window.BoundingRectangle.Contains(country.BoundingRectangle),
            "أبعاد البلد داخل النافذة وهو متاح؛ Offscreen مسجل منفصلًا: enabled=" + country.IsEnabled +
            "; offscreen=" + country.IsOffscreen + "; bounds=" + country.BoundingRectangle);
        Require((country.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains("YE", StringComparison.Ordinal),
            "تُقرأ قيمة اليمن المختارة دون فتح القائمة أو تغييرها");
        Require(dropdown.IsEnabled && dropdown.BoundingRectangle.Width > 0 &&
            window.BoundingRectangle.Contains(dropdown.BoundingRectangle), "أبعاد سهم البلد داخل النافذة وهو متاح");
        Require(!save.IsEnabled && cancel.IsEnabled, "الحفظ معطل والإلغاء متاح في المعاينة");
        Require(save.Name == (english ? "Save" : "حفظ") && cancel.Name == (english ? "Cancel" : "إلغاء"),
            "أسماء الإجراءات تتبع لغة الشاشة");
        var displayEvidence = new
        {
            Country = country.AsComboBox().SelectedItem?.Name,
            CountryOffscreenReported = country.IsOffscreen,
            DropdownOffscreenReported = dropdown.IsOffscreen,
            WindowDpi = GetDpiForWindow(application.MainWindowHandle),
            ControllerSystemDpi = GetDpiForSystem(),
            WindowDpiAwareness = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(application.MainWindowHandle)),
            CountryBounds = country.BoundingRectangle.ToString(),
            SaveEnabled = save.IsEnabled,
            KeyboardInjected = false,
            MouseInjected = false,
            ChangedSelection = false,
            SqlWrites = false
        };
        File.WriteAllText(Path.Combine(artifactsDirectory, "tax-settings-readonly" + suffix + "-display.json"),
            JsonSerializer.Serialize(displayEvidence, new JsonSerializerOptions { WriteIndented = true }));
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "tax-settings-readonly" + suffix + "-report.json");
        Console.WriteLine("PASS tax readonly values, bounds and screen capture; Offscreen is diagnostic, interactive accessibility remains open. No selection, focus, mouse or keyboard injection.");
        return 0;
    }

    private static int RunTaxSettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Accounting and tax settings" : "إعدادات الحسابات والضرائب", StringComparison.Ordinal), "فُتحت معاينة إعدادات الضريبة المعزولة باللغة المطلوبة");
        AutomationElement country = RequireElement(window, "cmbTaxCountry", "قائمة بلد الضريبة");
        AutomationElement countryDropdown = RequireElement(window, "taxCountryDropdown", "سهم اختيار بلد الضريبة AntdUI");
        AutomationElement save = RequireElement(window, "btnSaveTaxSettings", "زر حفظ الضريبة");
        AutomationElement cancel = RequireElement(window, "btnCancelTaxSettings", "زر إلغاء الضريبة");
        Require(country.IsEnabled, "قائمة بلد الضريبة مفعّلة في المعاينة");
        Require(!save.IsEnabled, "حفظ سياسة الضريبة معطّل في المعاينة المعزولة");
        Require(string.Equals(save.Name, english ? "Save" : "حفظ", StringComparison.Ordinal), "زر الحفظ له اسم وصول واضح");
        Require(string.Equals(cancel.Name, english ? "Cancel" : "إلغاء", StringComparison.Ordinal), "زر الإلغاء له اسم وصول واضح");

        window.Focus();
        country.AsComboBox().Select(1);
        Thread.Sleep(200);
        Require((country.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains("SA", StringComparison.Ordinal),
            "سهم AntdUI يغيّر البلد إلى السعودية في المعاينة دون حفظ سياسة الضريبة");
        cancel.Focus();
        Thread.Sleep(100);

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "tax-settings" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لإعدادات الضريبة");
        report.ScreenshotPath = screenshotPath;

        ActivateButton(cancel);
        Thread.Sleep(400);
        Require(application.HasExited, "يعمل إلغاء إعدادات الضريبة من زر الوصول");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "tax-settings" + suffix + "-report.json");
        Console.WriteLine("FlaUI tax settings interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunDatabaseSettingsScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool dark, bool english, bool readOnly = false)
    {
        string expectedWindowTitle = english ? "Database connection settings" : "إعدادات اتصال قاعدة البيانات";
        Require(string.Equals(window.Title, expectedWindowTitle, StringComparison.Ordinal),
            "فُتحت معاينة إعداد الاتصال المعزولة");
        AutomationElement save = RequireElement(window, "databaseSaveConnection", "حفظ اتصال قاعدة البيانات");
        AutomationElement test = RequireElement(window, "databaseTestConnection", "اختبار الاتصال");
        AutomationElement appearance = RequireElement(window, "databaseSaveAppearance", "حفظ المظهر");
        AutomationElement close = RequireElement(window, "databaseClose", "إغلاق إعداد الاتصال");
        AutomationElement theme = RequireElement(window, "databaseTheme", "اختيار المظهر");
        AutomationElement themeDropdown = RequireElement(window, "databaseThemeDropdown", "سهم المظهر AntdUI");
        AutomationElement title = RequireElement(window, "databaseSettingsTitle", "عنوان إعداد الاتصال");
        AutomationElement subtitle = RequireElement(window, "databaseSettingsSubtitle", "وصف إعداد الاتصال");
        Require(string.Equals(title.Name, english ? "Connection settings" : "إعدادات الاتصال", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(subtitle.Name), "عنوان إعداد الاتصال ووصفه مترجمان");
        Require(string.Equals(save.Name, english ? "Save database connection" : "حفظ اتصال قاعدة البيانات", StringComparison.Ordinal) &&
                string.Equals(close.Name, english ? "Close database settings" : "إغلاق إعدادات قاعدة البيانات", StringComparison.Ordinal),
            "إجراءات إعداد الاتصال تتبع لغة النافذة");
        Require(!save.IsEnabled && !test.IsEnabled && !appearance.IsEnabled,
            "المعاينة تمنع الاتصال والحفظ وتترك الإغلاق متاحًا");
        Require(close.IsEnabled && !string.IsNullOrWhiteSpace(close.Name), "زر الإغلاق متاح باسمه لقارئ الشاشة");
        string suffix = (readOnly ? "database-settings-readonly" : "database-settings") + (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي لإعداد الاتصال");
        report.ScreenshotPath = screenshotPath;
        if (readOnly)
        {
            static string ReadValue(AutomationElement field)
            {
                var editor = field.Patterns.Value.IsSupported ? field : field.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                Require(editor != null && editor.Patterns.Value.IsSupported, "حقل الاتصال يدعم قراءة القيمة دون إدخال");
                return editor!.Patterns.Value.Pattern.Value.Value;
            }
            string serverValue = ReadValue(RequireElement(window, "databaseServer", "الخادم"));
            string databaseValue = ReadValue(RequireElement(window, "databaseCatalog", "قاعدة المعاينة"));
            Require(serverValue == "." && databaseValue == "PreviewDb", "قيم الاتصال الوهمية مقروءة حرفيًا في النافذة الحية");
            Require(close.Patterns.Invoke.IsSupported, "زر AntdUI للإغلاق يعلن إجراء الوصول دون تنفيذه");
            var evidence = new { Server = serverValue, Database = databaseValue,
                WindowDpi = GetDpiForWindow(application.MainWindowHandle), ControllerSystemDpi = GetDpiForSystem(),
                WindowDpiAwareness = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(application.MainWindowHandle)),
                KeyboardInjected = false, MouseInjected = false, ChangedSelection = false, SqlWrites = false, CloseInvoked = false };
            File.WriteAllText(Path.Combine(artifactsDirectory, suffix + "-display.json"),
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            report.Passed = true; report.CompletedAtUtc = DateTimeOffset.UtcNow;
            WriteReport(artifactsDirectory, report, suffix + "-report.json");
            Console.WriteLine("PASS database readonly values, accessible action state and live capture; no input, selection or SQL writes. Keyboard and DPI acceptance remain open.");
            return 0;
        }
        ActivateButton(themeDropdown);
        Thread.Sleep(150);
        Keyboard.Press(VirtualKeyShort.DOWN);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Thread.Sleep(150);
        Require((theme.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains(english ? "Dark" : "داكن", StringComparison.Ordinal),
            "سهم AntdUI يتيح اختيار المظهر بلوحة المفاتيح دون حفظه");
        ActivateButton(close);
        Thread.Sleep(400);
        Require(application.HasExited, "يعمل إغلاق إعداد الاتصال من زر الوصول");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, suffix + "-report.json");
        Console.WriteLine("FlaUI database settings preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunAccountingPeriodScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Accounting periods" : "إدارة الفترات المحاسبية", StringComparison.Ordinal),
            "فُتحت معاينة الفترات المحاسبية المعزولة باللغة المطلوبة");
        AutomationElement from = RequireElement(window, "periodFromDate", "تاريخ بداية الفترة AntdUI");
        AutomationElement to = RequireElement(window, "periodToDate", "تاريخ نهاية الفترة AntdUI");
        AutomationElement notes = RequireElement(window, "periodNotes", "ملاحظات الفترة AntdUI");
        AutomationElement create = RequireElement(window, "createAccountingPeriod", "إنشاء الفترة");
        AutomationElement closePeriod = RequireElement(window, "closeAccountingPeriod", "إقفال الفترة");
        AutomationElement status = RequireElement(window, "accountingPeriodsStatus", "حالة الفترات");
        AutomationElement exit = RequireElement(window, "closeAccountingPeriodWindow", "إغلاق النافذة");
        AutomationElement title = RequireElement(window, "accountingPeriodsTitle", "عنوان الفترات المحاسبية");
        Require(string.Equals(title.Name, english ? "Accounting periods" : "إدارة الفترات المحاسبية", StringComparison.Ordinal) &&
                string.Equals(create.Name, english ? "Create an open accounting period" : "إنشاء فترة محاسبية مفتوحة", StringComparison.Ordinal),
            "عنوان وإجراء الفترات مترجمان بحسب لغة العرض");
        Require(from.IsEnabled && to.IsEnabled && notes.IsEnabled && create.IsEnabled && !closePeriod.IsEnabled,
            "التواريخ متاحة والمعاينة تمنع إقفال الفترة");
        Require(from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported && notes.Patterns.Value.IsSupported,
            "حقول الفترة تدعم القراءة والتعيين عبر واجهة الوصول");
        const string selectedDate = "2026/09/23";
        from.Patterns.Value.Pattern.SetValue(selectedDate);
        Thread.Sleep(150);
        Require(string.Equals(from.Patterns.Value.Pattern.Value.Value, selectedDate, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(to.Patterns.Value.Pattern.Value.Value),
            "احتفظ منتقي تاريخ الفترة بالقيمة المختارة وتاريخ النهاية");
        Require(notes.BoundingRectangle.Width >= from.BoundingRectangle.Width * 0.9 &&
                notes.BoundingRectangle.Width >= to.BoundingRectangle.Width * 0.9 &&
                notes.BoundingRectangle.Width >= window.BoundingRectangle.Width * 0.65,
            "حقل الملاحظات يستخدم عرض الصف نفسه ولا ينكمش بسبب عكس RTL");
        ActivateButton(create);
        Require(status.Name.Contains(english ? "was created" : "لم تُنشأ", StringComparison.Ordinal),
            "إنشاء الفترة في المعاينة لم ينفذ حفظًا");
        Require(english
                ? create.BoundingRectangle.Left < exit.BoundingRectangle.Left
                : create.BoundingRectangle.Right > exit.BoundingRectangle.Right,
            "تبدأ إجراءات الفترات من حافة القراءة الصحيحة");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "accounting-period" + suffix + "-window.png");
        exit.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة الفترات المحاسبية");
        report.ScreenshotPath = screenshotPath;
        exit = RequireElement(window, "closeAccountingPeriodWindow", "إغلاق الفترات بعد التصوير");
        ActivateButton(exit);
        for (int attempt = 0; attempt < 40 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة الفترات المحاسبية");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "accounting-period" + suffix + "-report.json");
        Console.WriteLine("FlaUI accounting period preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunPeriodReadinessScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Period-close readiness" : "جاهزية إقفال الفترة", StringComparison.Ordinal),
            "فُتحت معاينة جاهزية الإقفال المعزولة باللغة المطلوبة");
        AutomationElement title = RequireElement(window, "periodReadinessTitle", "عنوان جاهزية الإقفال");
        AutomationElement from = RequireElement(window, "periodReadinessFromDate", "بداية فترة الفحص");
        AutomationElement to = RequireElement(window, "periodReadinessToDate", "نهاية فترة الفحص");
        AutomationElement grid = RequireElement(window, "periodReadinessGrid", "نتائج فحوص الجاهزية");
        AutomationElement refresh = RequireElement(window, "refreshPeriodReadiness", "إعادة فحص الجاهزية");
        AutomationElement close = RequireElement(window, "closePeriodReadiness", "إغلاق فحص الجاهزية");
        AutomationElement? status = null;
        for (int attempt = 0; attempt < 15 && status == null; attempt++)
        {
            status = window.FindFirstDescendant(factory => factory.ByAutomationId("periodReadinessStatus"));
            if (status == null) Thread.Sleep(100);
        }
        Require(status != null, "ظهرت حالة فحص الجاهزية بعد استقرار تخطيط النافذة");
        Require(string.Equals(title.Name, english ? "Period-close readiness" : "جاهزية إقفال الفترة", StringComparison.Ordinal) &&
                string.Equals(refresh.Name, english ? "Check period close readiness" : "فحص جاهزية إقفال الفترة", StringComparison.Ordinal),
            "عنوان وإجراء الجاهزية مترجمان بحسب لغة العرض");
        Require(from.IsEnabled && to.IsEnabled && grid.IsEnabled && refresh.IsEnabled && close.IsEnabled,
            "حقول ونتائج وإجراءات الجاهزية متاحة في المعاينة");
        Require(from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported,
            "تاريخا فحص الجاهزية قابلان للقراءة والتحرير");
        Require(status!.Name.Contains(english ? "Read-only preview" : "معاينة واجهة القراءة فقط", StringComparison.Ordinal),
            "المعاينة تعلن بوضوح أنها لم تقرأ قاعدة البيانات");
        AutomationElement[] gridContent = grid.FindAllDescendants();
        Require(gridContent.Length >= 4,
            "جدول الجاهزية يكشف صفوف الفحوص الأربعة لقارئ الشاشة");
        const string selectedDate = "2026/09/23";
        from.Patterns.Value.Pattern.SetValue(selectedDate);
        Thread.Sleep(150);
        Require(string.Equals(from.Patterns.Value.Pattern.Value.Value, selectedDate, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(to.Patterns.Value.Pattern.Value.Value),
            "احتفظ فحص الجاهزية بالتاريخ المحدد وتاريخ النهاية");
        ActivateButton(refresh);
        Require(status.Name.Contains(english ? "Preview only" : "المعاينة للعرض فقط", StringComparison.Ordinal),
            "إعادة الفحص في المعاينة لم تنفذ قراءة SQL");
        Require(english
                ? refresh.BoundingRectangle.Left < close.BoundingRectangle.Left
                : refresh.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات الجاهزية من حافة القراءة الصحيحة");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "period-readiness" + suffix + "-window.png");
        close.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة جاهزية إقفال الفترة");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, "closePeriodReadiness", "إغلاق الجاهزية بعد التصوير");
        ActivateButton(close);
        for (int attempt = 0; attempt < 40 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة جاهزية الإقفال");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "period-readiness" + suffix + "-report.json");
        Console.WriteLine("FlaUI period readiness preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunIntegrityScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool accounting, bool english, bool dark)
    {
        string prefix = accounting ? "accountingIntegrity" : "inventoryIntegrity";
        string titleText = accounting
            ? (english ? "Journal integrity check" : "فحص سلامة القيود")
            : (english ? "Inventory integrity check" : "فحص مطابقة المخزون");
        string refreshId = accounting ? "refreshAccountingIntegrity" : "refreshInventoryIntegrity";
        string closeId = accounting ? "closeAccountingIntegrity" : "closeInventoryIntegrity";
        string refreshName = accounting
            ? (english ? "Check journal integrity again" : "إعادة فحص سلامة القيود")
            : (english ? "Check inventory integrity again" : "إعادة فحص مطابقة المخزون");

        Require(string.Equals(window.Title, titleText, StringComparison.Ordinal),
            "فُتحت معاينة السلامة المعزولة باللغة المطلوبة");
        AutomationElement title = RequireElement(window, prefix + "Title", "عنوان شاشة السلامة");
        AutomationElement grid = RequireElement(window, prefix + "Grid", "نتائج فحص السلامة");
        AutomationElement refresh = RequireElement(window, refreshId, "إعادة فحص السلامة");
        AutomationElement close = RequireElement(window, closeId, "إغلاق فحص السلامة");
        AutomationElement? status = null;
        for (int attempt = 0; attempt < 15 && status == null; attempt++)
        {
            status = window.FindFirstDescendant(factory => factory.ByAutomationId(prefix + "Status"));
            if (status == null) Thread.Sleep(100);
        }
        Require(status != null, "ظهرت حالة فحص السلامة بعد استقرار تخطيط النافذة");
        Require(string.Equals(title.Name, titleText, StringComparison.Ordinal) &&
                string.Equals(refresh.Name, refreshName, StringComparison.Ordinal),
            "عنوان وإجراء فحص السلامة مترجمان بحسب لغة العرض");
        Require(grid.IsEnabled && refresh.IsEnabled && close.IsEnabled,
            "نتائج وإجراءات فحص السلامة متاحة في المعاينة");
        Require(status!.Name.Contains(english ? "Read-only preview" : "معاينة واجهة القراءة فقط", StringComparison.Ordinal),
            "المعاينة تعلن بوضوح أنها لم تقرأ قاعدة البيانات");
        Require(grid.FindAllDescendants().Length >= 2,
            "جدول السلامة يكشف حالتي المعاينة لقارئ الشاشة");

        if (accounting)
        {
            AutomationElement from = RequireElement(window, "integrityFromDate", "بداية فترة سلامة القيود");
            AutomationElement to = RequireElement(window, "integrityToDate", "نهاية فترة سلامة القيود");
            Require(from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported,
                "تاريخا سلامة القيود قابلان للقراءة والتحرير");
            const string selectedDate = "2026/09/23";
            from.Patterns.Value.Pattern.SetValue(selectedDate);
            Thread.Sleep(150);
            Require(string.Equals(from.Patterns.Value.Pattern.Value.Value, selectedDate, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(to.Patterns.Value.Pattern.Value.Value),
                "احتفظ فحص سلامة القيود بالتاريخ المحدد وتاريخ النهاية");
        }

        ActivateButton(refresh);
        Require(status.Name.Contains(english ? "Preview only" : "المعاينة للعرض فقط", StringComparison.Ordinal),
            "إعادة فحص السلامة في المعاينة لم تنفذ قراءة SQL");
        Require(english
                ? refresh.BoundingRectangle.Left < close.BoundingRectangle.Left
                : refresh.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات فحص السلامة من حافة القراءة الصحيحة");

        string family = accounting ? "accounting-integrity" : "inventory-integrity";
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, family + suffix + "-window.png");
        close.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة فحص السلامة");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, closeId, "إغلاق فحص السلامة بعد التصوير");
        ActivateButton(close);
        for (int attempt = 0; attempt < 40 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة فحص السلامة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, family + suffix + "-report.json");
        Console.WriteLine("FlaUI " + family + " preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunSaudiSettingsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Saudi compliance settings" : "إعدادات المعايير السعودية", StringComparison.Ordinal),
            "فُتحت معاينة المعايير السعودية المعزولة باللغة المطلوبة");
        AutomationElement grid = RequireElement(window, "saudiFeaturesGrid", "جدول المزايا السعودية");
        AutomationElement reason = RequireElement(window, "saudiCommercialReasonText", "سبب التغيير");
        AutomationElement save = RequireElement(window, "saveSaudiCommercialSettings", "حفظ التغييرات");
        AutomationElement close = RequireElement(window, "closeSaudiCommercialSettings", "إغلاق الإعدادات");
        Require(grid.IsEnabled && reason.IsEnabled && close.IsEnabled && !save.IsEnabled,
            "المعاينة تسمح بالتحرير والإغلاق وتمنع حفظ SQL");
        Require(reason.Patterns.Value.IsSupported, "سبب التغيير قابل للقراءة والتحرير");
        const string sample = "اختبار معايير السعودية — Batch 500 mg";
        window.Focus();
        string actualReason = string.Empty;
        for (int attempt = 0; attempt < 3 && !string.Equals(actualReason, sample, StringComparison.Ordinal); attempt++)
        {
            reason = RequireElement(window, "saudiCommercialReasonText", "سبب التغيير");
            reason.Patterns.Value.Pattern.SetValue(sample);
            Thread.Sleep(150);
            actualReason = reason.Patterns.Value.Pattern.Value.Value;
        }
        Require(string.Equals(actualReason, sample, StringComparison.Ordinal),
            "احتفظ حقل سبب التغيير بالنص العربي والإنجليزي حرفيًا");
        Require(close.Patterns.Invoke.IsSupported, "زر الإغلاق يعلن دعم إجراء الوصول");
        Require(string.Equals(close.Name, english ? "Close Saudi compliance settings" : "إغلاق إعدادات المعايير السعودية", StringComparison.Ordinal),
            "زر إغلاق المعايير السعودية يحتفظ باسمه المتاح");

        close.Focus();
        Thread.Sleep(100);
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "saudi-settings" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة نافذة المعايير السعودية");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, "closeSaudiCommercialSettings", "إغلاق الإعدادات بعد التصوير");
        if (close.IsEnabled) ActivateButton(close);
        else window.Close();
        Thread.Sleep(350);
        Require(application.HasExited, "أغلقت معاينة المعايير السعودية دون اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "saudi-settings" + suffix + "-report.json");
        Console.WriteLine("FlaUI Saudi settings preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunPharmacyProfileScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Pharmacy profile" : "بيانات الصيدلية", StringComparison.Ordinal),
            "فُتحت معاينة بيانات الصيدلية المعزولة باللغة المطلوبة");
        AutomationElement arabicName = RequireElement(window, "txtNameAr", "اسم الصيدلية العربي");
        AutomationElement englishName = RequireElement(window, "txtNameEn", "اسم الصيدلية الإنجليزي");
        AutomationElement invoiceNotes = RequireElement(window, "txtInvoiceNotes", "ملاحظات الفاتورة");
        AutomationElement save = RequireElement(window, "btnToolSave", "حفظ بيانات الصيدلية");
        AutomationElement add = RequireElement(window, "btnToolAdd", "ملف صيدلية جديد");
        AutomationElement print = RequireElement(window, "btnToolPrint", "طباعة بيانات الصيدلية");
        AutomationElement logo = RequireElement(window, "btnPickLogo", "اختيار شعار الصيدلية");
        AutomationElement close = RequireElement(window, "btnToolExit", "إغلاق بيانات الصيدلية");
        Require(arabicName.IsEnabled && englishName.IsEnabled && invoiceNotes.IsEnabled && close.IsEnabled &&
                !save.IsEnabled && !add.IsEnabled && !print.IsEnabled && !logo.IsEnabled,
            "المعاينة تسمح بتحرير النص واختباره وتمنع الحفظ والطباعة واختيار الملفات");
        Require(arabicName.Patterns.Value.IsSupported && englishName.Patterns.Value.IsSupported &&
                (invoiceNotes.Patterns.Value.IsSupported || invoiceNotes.Patterns.Text.IsSupported),
            "حقول بيانات الصيدلية قابلة للقراءة والتحرير عبر الوصول");
        const string arabicSample = "صيدلية المهندس — Pharma 500 mg";
        const string englishSample = "Engineer Pharmacy — Aden 2026";
        const string notesSample = "سطر أول — Batch 500 mg\r\nSecond line";
        arabicName.Patterns.Value.Pattern.SetValue(arabicSample);
        englishName.Patterns.Value.Pattern.SetValue(englishSample);
        invoiceNotes.AsTextBox().Text = notesSample;
        Thread.Sleep(180);
        string actualNotes = invoiceNotes.Patterns.Value.IsSupported
            ? invoiceNotes.Patterns.Value.Pattern.Value.Value
            : invoiceNotes.AsTextBox().Text;
        Require(string.Equals(arabicName.Patterns.Value.Pattern.Value.Value, arabicSample, StringComparison.Ordinal) &&
                string.Equals(englishName.Patterns.Value.Pattern.Value.Value, englishSample, StringComparison.Ordinal) &&
                string.Equals(actualNotes.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"), notesSample, StringComparison.Ordinal),
            "احتفظت حقول بيانات الصيدلية بالنص العربي والإنجليزي والمختلط حرفيًا");
        Require(close.Patterns.Invoke.IsSupported &&
                string.Equals(close.Name, english ? "Close pharmacy profile" : "إغلاق بيانات الصيدلية", StringComparison.Ordinal),
            "زر إغلاق بيانات الصيدلية يعلن إجراء وصول واسمًا محليًا واضحًا");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "pharmacy-profile" + suffix + "-window.png");
        close.Focus();
        Thread.Sleep(100);
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة نافذة بيانات الصيدلية");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, "btnToolExit", "إغلاق بيانات الصيدلية بعد التصوير");
        if (close.IsEnabled) close.Click();
        else window.Close();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة بيانات الصيدلية دون اتصال SQL أو طباعة أو اختيار ملف");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "pharmacy-profile" + suffix + "-report.json");
        Console.WriteLine("FlaUI pharmacy profile preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunBootstrapManagerScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Initial manager setup" : "إعداد المدير الأول", StringComparison.Ordinal),
            "فُتحت معاينة تهيئة المدير الأول المعزولة باللغة المطلوبة");
        AutomationElement employeeId = RequireElement(window, "bootstrapEmployeeId", "رقم الموظف");
        AutomationElement employeeName = RequireElement(window, "bootstrapEmployeeName", "اسم المدير");
        AutomationElement phone = RequireElement(window, "bootstrapEmployeePhone", "هاتف المدير");
        AutomationElement save = RequireElement(window, "createBootstrapManager", "إنشاء المدير الأول");
        AutomationElement cancel = RequireElement(window, "cancelBootstrapManager", "إلغاء تهيئة المدير الأول");
        AutomationElement employeeIdLabel = RequireElement(window, "bootstrapFieldLabel0", "تسمية رقم الموظف");
        AutomationElement employeeNameLabel = RequireElement(window, "bootstrapFieldLabel1", "تسمية اسم المستخدم");
        AutomationElement phoneLabel = RequireElement(window, "bootstrapFieldLabel4", "تسمية رقم الهاتف");
        Require(string.Equals(employeeIdLabel.Name, english ? "Employee ID" : "رقم الموظف", StringComparison.Ordinal) &&
                string.Equals(employeeNameLabel.Name, english ? "Username" : "اسم المستخدم", StringComparison.Ordinal) &&
                string.Equals(phoneLabel.Name, english ? "Phone number" : "رقم الهاتف", StringComparison.Ordinal),
            "تسميات المدير الأول مترجمة بالكامل بحسب لغة العرض");
        Require(employeeId.IsEnabled && employeeName.IsEnabled && phone.IsEnabled && cancel.IsEnabled && !save.IsEnabled,
            "المعاينة تسمح باختبار الإدخال والإغلاق وتمنع إنشاء مستخدم");
        Require(employeeId.Patterns.Value.IsSupported && employeeName.Patterns.Value.IsSupported && phone.Patterns.Value.IsSupported,
            "حقول المدير الأول تدعم التحرير عبر الوصول");
        const string mixedName = "مدير النظام — System Manager 2026";
        employeeName.Patterns.Value.Pattern.SetValue(mixedName);
        employeeId.Patterns.Value.Pattern.SetValue("1002");
        phone.Patterns.Value.Pattern.SetValue("+967 711 222 333");
        Thread.Sleep(150);
        // Switching a native TextBox between LTR and RTL can recreate its Win32 handle.
        // Reacquire the automation elements so the mixed Arabic/English verification
        // observes the current controls rather than stale UIA handles.
        employeeName = RequireElement(window, "bootstrapEmployeeName", "اسم المدير بعد تطبيق اتجاه النص");
        employeeId = RequireElement(window, "bootstrapEmployeeId", "رقم الموظف بعد التحرير");
        phone = RequireElement(window, "bootstrapEmployeePhone", "هاتف المدير بعد التحرير");
        Require(string.Equals(employeeName.Patterns.Value.Pattern.Value.Value, mixedName, StringComparison.Ordinal) &&
                string.Equals(employeeId.Patterns.Value.Pattern.Value.Value, "1002", StringComparison.Ordinal) &&
                string.Equals(phone.Patterns.Value.Pattern.Value.Value, "+967 711 222 333", StringComparison.Ordinal),
            "احتفظت حقول المدير بالنص المختلط والقيم الرقمية حرفيًا");
        Require(string.Equals(cancel.Name, english ? "Cancel initial manager setup" : "إلغاء إعداد المدير الأول", StringComparison.Ordinal),
            "زر الإلغاء يملك اسم وصول محليًا واضحًا");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "bootstrap-manager" + suffix + "-window.png");
        cancel.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة تهيئة المدير الأول");
        report.ScreenshotPath = screenshotPath;
        cancel = RequireElement(window, "cancelBootstrapManager", "إلغاء تهيئة المدير بعد التصوير");
        cancel.Click();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة المدير الأول دون إنشاء مستخدم أو فتح SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "bootstrap-manager" + suffix + "-report.json");
        Console.WriteLine("FlaUI bootstrap manager preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunCommercialReturnScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Commercial sales return" : "إشعار مرتجع تجاري", StringComparison.Ordinal),
            "فُتحت معاينة المرتجع التجاري المعزولة باللغة المطلوبة");
        AutomationElement reason = RequireElement(window, "commercialReturnReason", "سبب المرتجع");
        AutomationElement refund = RequireElement(window, "commercialReturnRefund", "طريقة الاسترداد");
        AutomationElement lots = RequireElement(window, "commercialReturnLotsGrid", "دفعات الفاتورة المتاحة للإرجاع");
        AutomationElement issue = RequireElement(window, "issueCommercialReturn", "اعتماد إشعار المرتجع");
        AutomationElement cancel = RequireElement(window, "cancelCommercialReturn", "إلغاء إشعار المرتجع");
        AutomationElement reasonLabel = RequireElement(window, "commercialReturnReasonLabel", "تسمية سبب المرتجع");
        AutomationElement refundLabel = RequireElement(window, "commercialReturnRefundLabel", "تسمية طريقة الاسترداد");
        Require(string.Equals(reasonLabel.Name, english ? "Return reason" : "سبب المرتجع", StringComparison.Ordinal) &&
                string.Equals(refundLabel.Name, english ? "Refund method" : "طريقة الاسترداد", StringComparison.Ordinal),
            "تسميات المرتجع مترجمة بالكامل بحسب لغة العرض");
        Require(reason.Patterns.Value.IsSupported && reason.IsEnabled && refund.IsEnabled && lots.IsEnabled,
            "حقول المرتجع والجدول متاحة للفحص عبر الوصول");
        const string mixedReason = "عبوة سليمة — Batch 500 mg";
        reason.Patterns.Value.Pattern.SetValue(mixedReason);
        Require(string.Equals(reason.Patterns.Value.Pattern.Value.Value, mixedReason, StringComparison.Ordinal),
            "احتفظ سبب المرتجع بالنص العربي واللاتيني حرفيًا");
        Require(!issue.IsEnabled && cancel.IsEnabled,
            "المعاينة تمنع اعتماد إشعار مالي وتسمح بالإغلاق");
        Require(english
                ? issue.BoundingRectangle.Left < cancel.BoundingRectangle.Left
                : issue.BoundingRectangle.Right > cancel.BoundingRectangle.Right,
            "تبدأ إجراءات المرتجع من حافة القراءة الصحيحة");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "commercial-return" + suffix + "-window.png");
        cancel.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة المرتجع التجاري");
        report.ScreenshotPath = screenshotPath;
        cancel = RequireElement(window, "cancelCommercialReturn", "إغلاق المرتجع بعد التصوير");
        cancel.Click();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة المرتجع دون قراءة SQL أو اعتماد مستند مالي");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "commercial-return" + suffix + "-report.json");
        Console.WriteLine("FlaUI commercial return preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunReturnHistoryScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Sales return credit notes" : "سجل إشعارات المرتجع", StringComparison.Ordinal),
            "فُتحت معاينة سجل المرتجعات المعزولة باللغة المطلوبة");
        AutomationElement grid = RequireElement(window, "commercialReturnHistoryGrid", "جدول سجل المرتجعات");
        AutomationElement heading = RequireElement(window, "returnHistorySectionHeading", "عنوان سجل المرتجعات");
        AutomationElement preview = RequireElement(window, "previewCommercialReturn", "معاينة إشعار المرتجع");
        AutomationElement close = RequireElement(window, "closeCommercialReturnHistory", "إغلاق سجل المرتجعات");
        Require(string.Equals(heading.Name, english ? "Approved credit notes" : "الإشعارات الدائنة المعتمدة", StringComparison.Ordinal),
            "عنوان سجل المرتجعات مترجم بحسب لغة العرض");
        Require(grid.IsEnabled && !preview.IsEnabled && close.IsEnabled,
            "المعاينة تعرض السجل وتمنع الطباعة وتسمح بالإغلاق");
        Require(english
                ? preview.BoundingRectangle.Left < close.BoundingRectangle.Left
                : preview.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات سجل المرتجعات من حافة القراءة الصحيحة");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "return-history" + suffix + "-window.png");
        close.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة سجل المرتجعات");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, "closeCommercialReturnHistory", "إغلاق سجل المرتجعات بعد التصوير");
        close.Click();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة سجل المرتجعات دون SQL أو طباعة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "return-history" + suffix + "-report.json");
        Console.WriteLine("FlaUI return history preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunQuarantineResolutionScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Quarantined stock" : "إدارة مخزون الحجر", StringComparison.Ordinal),
            "فُتحت معاينة قرار الحجر المعزولة باللغة المطلوبة");
        AutomationElement grid = RequireElement(window, "quarantineSourceGrid", "دفعات الحجر المعلقة");
        AutomationElement heading = RequireElement(window, "quarantineResolutionSourceHeading", "عنوان دفعات الحجر");
        AutomationElement choice = RequireElement(window, "quarantineResolutionChoice", "قرار الحجر");
        AutomationElement reason = RequireElement(window, "quarantineResolutionReason", "سبب قرار الحجر");
        AutomationElement approve = RequireElement(window, "approveQuarantineResolution", "اعتماد قرار الحجر");
        AutomationElement close = RequireElement(window, "closeQuarantineResolution", "إغلاق قرار الحجر");
        Require(string.Equals(heading.Name, english ? "Pending quarantined lots" : "الدفعات المحجورة المعلقة", StringComparison.Ordinal),
            "عنوان دفعات الحجر مترجم بحسب لغة العرض");
        Require(grid.IsEnabled && choice.IsEnabled && reason.IsEnabled && !approve.IsEnabled && close.IsEnabled,
            "المعاينة تعرض الدفعات والمدخلات وتمنع اعتماد حركة مخزون");
        Require(reason.Patterns.Value.IsSupported, "سبب قرار الحجر قابل للتحرير عبر الوصول");
        const string mixedReason = "فحص العبوة — Batch 500 mg";
        reason.Patterns.Value.Pattern.SetValue(mixedReason);
        Require(string.Equals(reason.Patterns.Value.Pattern.Value.Value, mixedReason, StringComparison.Ordinal),
            "احتفظ سبب قرار الحجر بالنص العربي واللاتيني حرفيًا");
        Require(english
                ? approve.BoundingRectangle.Left < close.BoundingRectangle.Left
                : approve.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات قرار الحجر من حافة القراءة الصحيحة");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "quarantine-resolution" + suffix + "-window.png");
        close.Focus();
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة قرار الحجر");
        report.ScreenshotPath = screenshotPath;
        close = RequireElement(window, "closeQuarantineResolution", "إغلاق قرار الحجر بعد التصوير");
        ActivateButton(close);
        for (int attempt = 0; attempt < 40 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أغلقت معاينة قرار الحجر دون SQL أو حركة مخزون أو قيد");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "quarantine-resolution" + suffix + "-report.json");
        Console.WriteLine("FlaUI quarantine resolution preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunPermissionsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report)
    {
        Require(string.Equals(window.Title, "صلاحيات الشاشات والعمليات", StringComparison.Ordinal),
            "فُتحت معاينة صلاحيات الشاشات والعمليات المعزولة");
        AutomationElement grid = RequireElement(window, "employeeOperationPermissionsGrid", "جدول الصلاحيات");
        AutomationElement save = RequireElement(window, "saveEmployeeOperationPermissions", "حفظ الصلاحيات");
        AutomationElement close = RequireElement(window, "closeEmployeeOperationPermissions", "إغلاق الصلاحيات");
        Require(grid.IsEnabled && !save.IsEnabled && close.IsEnabled,
            "جدول المعاينة ظاهر والحفظ معطل والإغلاق متاح");
        Require(close.Patterns.Invoke.IsSupported &&
            string.Equals(close.Name, "إغلاق صلاحيات الشاشات والعمليات", StringComparison.Ordinal),
            "زر الإغلاق يوفر إجراء وصول واسمًا واضحًا");
        // FlaUI's screen capture can include another desktop window, while PrintWindow
        // reverses text in this RTL WinForms dialog. Layout fixtures provide visual evidence.
        report.ScreenshotPath = null;
        ActivateButton(close);
        Thread.Sleep(350);
        Require(application.HasExited, "أغلقت معاينة الصلاحيات عبر إجراء الوصول دون SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "permissions-report.json");
        Console.WriteLine("FlaUI permission settings accessibility passed. Visual evidence uses layout fixtures.");
        return 0;
    }

    private static int RunManufacturerScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Manufacturers" : "الشركات المصنعة", StringComparison.Ordinal),
            "فُتحت شاشة الشركات المصنعة بعنوان واحد ومترجم");
        AutomationElement name = RequireElement(window, "tb2", "حقل اسم الشركة");
        AutomationElement add = RequireElement(window, "button4", "إضافة شركة");
        AutomationElement edit = RequireElement(window, "button3", "تعديل شركة");
        AutomationElement delete = RequireElement(window, "button1", "حذف شركة");
        AutomationElement clear = RequireElement(window, "button5", "مسح حقول الشركة");
        AutomationElement close = RequireElement(window, "button2", "إغلاق الشركات");
        AutomationElement grid = RequireElement(window, "manufacturerTable", "جدول الشركات المصنعة");
        AutomationElement footer = RequireElement(window, "manufacturerActionBar", "شريط الإجراءات");
        AutomationElement statusStrip = RequireElement(window, "__UiStatusStrip", "شريط الحالة");
        Require(footer.BoundingRectangle.Bottom <= statusStrip.BoundingRectangle.Top,
            "أزرار الشركات لا تتداخل مع شريط الحالة؛ footer=" + footer.BoundingRectangle +
            ", status=" + statusStrip.BoundingRectangle);
        Require(name.Patterns.Value.IsSupported && !add.IsEnabled && !edit.IsEnabled && !delete.IsEnabled &&
            clear.IsEnabled && close.IsEnabled,
            "المعاينة تمنع عمليات الشركات الدائمة وتبقي المسح والإغلاق متاحين");
        AutomationElement[] companyCells = grid.FindAllDescendants();
        foreach (string expected in new[] { "COMP-001", "الشفاء — Al Shifa", "+967 1 234 567",
            "صنعاء — شارع الزبيري", "COMP-014", "Pharma 500 mg", "+967 777 555 444", "عدن — المنصورة" })
            Require(companyCells.Any(cell => cell.Name.EndsWith(": " + expected, StringComparison.Ordinal) &&
                cell.BoundingRectangle.Width > 0 && cell.BoundingRectangle.Height > 0),
                "قيمة شركة فعلية وحدودها متاحة لأدوات الوصول: " + expected);
        Require(english ? add.BoundingRectangle.Left < close.BoundingRectangle.Left
                        : add.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات الشركات من حافة القراءة الصحيحة");
        Require(string.Equals(add.Name, english ? "Add manufacturer" : "إضافة شركة مصنعة", StringComparison.Ordinal) &&
            string.Equals(close.Name, english ? "Close manufacturers screen" : "إغلاق شاشة الشركات المصنعة", StringComparison.Ordinal),
            "إجراءات الشركات مترجمة وتملك أسماء وصول واضحة");
        const string sample = "شركة المستلزمات الطبية — Pharma 500 mg";
        Require(PromoteForScreenCapture(application.MainWindowHandle), "معاينة الشركات ظاهرة قبل إدخال الاسم");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            window.Focus(); name = RequireElement(window, "tb2", "اسم الشركة قبل الإدخال"); name.Click(); name.Focus(); Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && name.Properties.HasKeyboardFocus.Value) break;
        }
        Require(GetForegroundWindow() == application.MainWindowHandle && name.Properties.HasKeyboardFocus.Value,
            "نافذة الشركات وحقل الاسم يملكان التركيز قبل Unicode");
        TypeInvariantUnicodeText(sample);
        Thread.Sleep(200);
        name = RequireElement(window, "tb2", "اسم الشركة بعد الإدخال");
        Require(string.Equals(name.Patterns.Value.Pattern.Value.Value, sample, StringComparison.Ordinal),
            "حافظ حقل الشركة على النص العربي والإنجليزي حرفيًا");
        Require(close.Patterns.Invoke.IsSupported &&
            string.Equals(close.Name, english ? "Close manufacturers screen" : "إغلاق شاشة الشركات المصنعة", StringComparison.Ordinal),
            "زر الشركات المصنعة يوفر إجراء الوصول واسمه الواضح");
        string screenshotPath = Path.Combine(artifactsDirectory,
            "manufacturer-" + (english ? "english" : "arabic") + "-" + (dark ? "dark" : "light") + ".png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة شاشة الشركات المصنعة");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        Thread.Sleep(350);
        Require(application.HasExited, "أغلقت معاينة الشركات عبر إجراء الوصول دون SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report,
            "manufacturer-" + (english ? "english" : "arabic") + "-" + (dark ? "dark" : "light") + "-report.json");
        Console.WriteLine("FlaUI manufacturer accessibility passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunQuickJournalScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report)
    {
        Require(string.Equals(window.Title, "القيود اليومية السريعة", StringComparison.Ordinal), "فُتحت معاينة القيود اليومية السريعة دون SQL");
        AutomationElement editor = RequireElement(window, "accountingDataEditor", "محرر القيد السريع");
        AutomationElement grid = RequireElement(window, "accountingDataGrid", "سجل القيود");
        AutomationElement fromLabel = RequireElement(window, "accountingFieldLabel_quickJournalFromDate", "تسمية تاريخ البداية");
        AutomationElement fromDate = RequireElement(window, "quickJournalFromDate", "تاريخ البداية");
        AutomationElement toLabel = RequireElement(window, "accountingFieldLabel_quickJournalToDate", "تسمية تاريخ النهاية");
        AutomationElement toDate = RequireElement(window, "quickJournalToDate", "تاريخ النهاية");
        AutomationElement description = RequireElement(window, "quickJournalDescription", "بيان القيد");
        AutomationElement save = RequireElement(window, "quickJournalSave", "زر حفظ القيد");
        AutomationElement refresh = RequireElement(window, "quickJournalRefresh", "زر تحديث القيود");
        AutomationElement footer = RequireElement(window, "quickJournalActions", "تذييل إجراءات القيود");
        AutomationElement header = RequireElement(window, "accountingDataHeader", "رأس القيود اليومية");
        AutomationElement headerClose = RequireElement(window, "accountingDataCloseButton", "إغلاق شاشة القيود");
        Require(editor.BoundingRectangle.Left >= grid.BoundingRectangle.Right - 2,
            "محرر القيود العربية على يمين الجدول");
        Require(headerClose.BoundingRectangle.Left < header.BoundingRectangle.Left + header.BoundingRectangle.Width / 3,
            "زر إغلاق الرأس مقابل العنوان العربي عند تكبير Windows");
        Require(fromLabel.BoundingRectangle.Bottom <= fromDate.BoundingRectangle.Top &&
            toLabel.BoundingRectangle.Bottom <= toDate.BoundingRectangle.Top,
            "تسميات التاريخين أعلى مدخليهما دون تداخل");
        Require(description.Patterns.Value.IsSupported && !save.IsEnabled,
            "بيان القيد قابل للتحرير والحفظ معطل في المعاينة");
        Require(fromDate.Patterns.Value.IsSupported && toDate.Patterns.Value.IsSupported,
            "تاريخا AntdUI يدعمان قراءة القيمة");
        Require(save.BoundingRectangle.Bottom <= footer.BoundingRectangle.Bottom &&
            refresh.BoundingRectangle.Bottom <= footer.BoundingRectangle.Bottom &&
            footer.BoundingRectangle.Width >= window.BoundingRectangle.Width * 0.95,
            "إجراءا التذييل ظاهران بعرض الشاشة؛ النافذة=" + window.BoundingRectangle +
            ", التذييل=" + footer.BoundingRectangle + ", الحفظ=" + save.BoundingRectangle +
            ", التحديث=" + refresh.BoundingRectangle);
        fromDate.Patterns.Value.Pattern.SetValue("2026/09/23");
        Require(string.Equals(fromDate.Patterns.Value.Pattern.Value.Value, "2026/09/23", StringComparison.Ordinal),
            "احتفظ تاريخ بداية القيود بالنص المدخل");
        const string sample = "قيد توضيحي — Batch 500 mg";
        description.Patterns.Value.Pattern.SetValue(sample);
        Require(string.Equals(description.Patterns.Value.Pattern.Value.Value, sample, StringComparison.Ordinal),
            "احتفظ بيان القيد بالنص العربي والإنجليزي حرفيًا");
        ActivateButton(refresh);
        Require(grid.IsEnabled && !application.HasExited,
            "تحديث المعاينة لا يتصل بقاعدة البيانات ولا يغلق الشاشة");

        string screenshotPath = Path.Combine(artifactsDirectory, "quick-journal-window.png");
        window.CaptureToFile(screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة تذييل القيود السريعة");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        Thread.Sleep(350);
        Require(application.HasExited, "أُغلقت معاينة القيود دون اعتماد قيد أو اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "quick-journal-report.json");
        Console.WriteLine("FlaUI quick journal layout and input passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunJournalEntryScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool dark)
    {
        Require(string.Equals(window.Title, "القيود اليومية", StringComparison.Ordinal),
            "فُتحت معاينة القيد اليومي دون SQL");
        AutomationElement title = RequireElement(window, "journalScreenTitle", "عنوان القيد اليومي");
        AutomationElement date = RequireElement(window, "journalEntryDate", "تاريخ القيد AntdUI");
        AutomationElement number = RequireElement(window, "journalEntryNumber", "رقم القيد المعروض");
        AutomationElement toolbar = RequireElement(window, "journalToolbar", "تذييل الإجراءات");
        AutomationElement save = RequireElement(window, "journalAction2", "زر حفظ القيد");
        AutomationElement status = RequireElement(window, "journalStatus", "حالة القيد");
        Require(title.BoundingRectangle.Bottom < date.BoundingRectangle.Top &&
            toolbar.BoundingRectangle.Top > date.BoundingRectangle.Bottom &&
            date.BoundingRectangle.Height >= 36 && save.BoundingRectangle.Height >= 44,
            "العنوان والتاريخ والتذييل مرتبة وبأهداف تفاعل مناسبة");
        Require(date.Patterns.Value.IsSupported &&
            !string.IsNullOrWhiteSpace(date.Patterns.Value.Pattern.Value.Value) &&
            number.Patterns.Value.IsSupported &&
            number.Patterns.Value.Pattern.Value.Value == "تلقائي عند الحفظ",
            "تظهر قيمة التاريخ ورقم القيد العربي دون فقدان النص");
        string screenshotPath = Path.Combine(artifactsDirectory, dark ? "journal-entry-dark-window.png" : "journal-entry-window.png");
        CaptureWindowContent(application.MainWindowHandle, screenshotPath);
        report.ScreenshotPath = screenshotPath;
        date.Patterns.Value.Pattern.SetValue("2026/09/23");
        Thread.Sleep(200);
        Require(date.Patterns.Value.Pattern.Value.Value == "2026/09/23",
            "احتفظ تاريخ القيد بقيمة واجهة الوصول المنسقة");
        ActivateButton(save);
        Thread.Sleep(150);
        Require(status.Name.Contains("المعاينة للعرض فقط", StringComparison.Ordinal),
            "زر الحفظ في المعاينة لا ينشئ قيدًا ولا يتصل بقاعدة البيانات");
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة القيد اليومي");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, dark ? "journal-entry-dark-report.json" : "journal-entry-report.json");
        Console.WriteLine("FlaUI journal date ValuePattern, layout, and preview save guard passed. Keyboard typing remains unverified.");
        return 0;
    }

    private static int RunVoucherActionsScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool dark)
    {
        Require(string.Equals(window.Title, "سند قبض خارجي", StringComparison.Ordinal),
            "فُتحت معاينة سند القبض المعزولة دون SQL");
        window.Focus();
        AutomationElement title = RequireElement(window, "receiptVoucherScreenTitle", "عنوان السند");
        AutomationElement date = RequireElement(window, "receiptVoucherDate", "تاريخ السند");
        AutomationElement side = RequireElement(window, "receiptVoucherSide", "ملخص السند الجانبي");
        AutomationElement toolbar = RequireElement(window, "receiptVoucherToolbar", "شريط إجراءات السند");
        AutomationElement save = RequireElement(window, "receiptVoucherAction_Save_حفظ", "زر حفظ السند");
        AutomationElement status = RequireElement(window, "receiptVoucherStatus", "حالة السند");
        Require(title.BoundingRectangle.Bottom < date.BoundingRectangle.Top &&
            toolbar.BoundingRectangle.Top > date.BoundingRectangle.Bottom &&
            save.BoundingRectangle.Height >= 44 && date.IsEnabled,
            "عنوان السند والتاريخان وإجراء الحفظ ظاهرون بترتيب صحيح");
        Require(date.Patterns.Value.IsSupported, "تاريخ السند يدعم إدخال AntdUI عبر واجهة الوصول");
        date.Patterns.Value.Pattern.SetValue(string.Empty);
        date.Focus();
        Keyboard.Type("2026/09/23");
        Thread.Sleep(200);
        Require(date.Patterns.Value.Pattern.Value.Value == "2026/09/23",
            "احتفظ تاريخ السند بالمدخل الفعلي من لوحة المفاتيح");
        double cashSideWidth = side.BoundingRectangle.Width;
        AutomationElement cheque = RequireElement(window, "receiptVoucherMethod_Cheque", "طريقة الدفع بالشيك");
        ActivateButton(cheque);
        Thread.Sleep(100);
        AutomationElement chequeDate = RequireElement(window, "receiptVoucherChequeDate", "تاريخ الشيك");
        Require(chequeDate.IsEnabled && side.BoundingRectangle.Width >= cashSideWidth + 50,
            "ظهر تاريخ الشيك واتسع جانبه بعد اختيار طريقة الشيك");
        Require(chequeDate.Patterns.Value.IsSupported, "تاريخ استحقاق الشيك يدعم إدخال AntdUI");
        chequeDate.Patterns.Value.Pattern.SetValue("2026/10/15");
        Require(chequeDate.Patterns.Value.Pattern.Value.Value == "2026/10/15",
            "احتفظ تاريخ استحقاق الشيك بالقيمة المختارة");
        string screenshotPath = Path.Combine(artifactsDirectory,
            dark ? "voucher-actions-dark-window.png" : "voucher-actions-window.png");
        CaptureWindowContent(application.MainWindowHandle, screenshotPath);
        report.ScreenshotPath = screenshotPath;
        ActivateButton(RequireElement(window, "receiptVoucherMethod_Cash", "طريقة الدفع النقدي"));
        Thread.Sleep(100);
        Require(side.BoundingRectangle.Width <= cashSideWidth + 3,
            "عاد ملخص السند إلى العرض المضغوط عند الدفع النقدي");
        Require(!save.IsEnabled &&
            !RequireElement(window, "receiptVoucherAction_Print_حفظ وطباعة", "حفظ وطباعة السند").IsEnabled &&
            !RequireElement(window, "receiptVoucherAction_Print_طباعة كاشير", "طباعة السند").IsEnabled,
            "تعطل المعاينة الحفظ والطباعة قبل أي معاملة أو اتصال بطابعة");
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة السند دون SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, dark ? "voucher-actions-dark-report.json" : "voucher-actions-report.json");
        Console.WriteLine("FlaUI voucher actions, AntdUI date input, and preview save guard passed.");
        return 0;
    }

    private static int RunHomeScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english)
    {
        string expectedTitle = english ? "Home" : "الرئيسية";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتحت الرئيسية باللغة المطلوبة وبمعاينة معزولة دون SQL");
        window.Focus();
        // Wait for the first resize/paint cycle before capturing visual evidence.
        Thread.Sleep(600);
        AutomationElement title = RequireElement(window, "label1", "عنوان الرئيسية");
        AutomationElement subtitle = RequireElement(window, "label2", "وصف مساحة العمل");
        AutomationElement monitor = RequireElement(window, "monitorButton", "زر مراقبة النظام");
        AutomationElement screenMode = RequireElement(window, "screenModeButton", "زر وضع الشاشة");
        AutomationElement layoutMode = RequireElement(window, "layoutModeButton", "زر تخطيط الرئيسية");
        AutomationElement workspaceToggle = RequireElement(window, "workspaceToggleButton", "زر اختصار وتوسيع الأقسام");
        AutomationElement workspace = RequireElement(window, "workspaceButton", "زر تخصيص مساحة العمل");
        AutomationElement sales = RequireElement(window, "label6", "بطاقة المبيعات");
        AutomationElement purchases = RequireElement(window, "label9", "بطاقة المشتريات");
        AutomationElement dashboard = RequireElement(window, "label7", "بطاقة لوحة التحكم");

        Require(string.Equals(title.Name, expectedTitle, StringComparison.Ordinal), "عنوان الرئيسية يطابق لغة النافذة");
        Require(string.Equals(subtitle.Name, english ? "Daily workspace" : "مساحة العمل اليومية", StringComparison.Ordinal),
            "وصف مساحة العمل مترجم");
        Require(string.Equals(sales.Name, english ? "Sales" : "المبيعات", StringComparison.Ordinal) &&
                string.Equals(purchases.Name, english ? "Purchases" : "المشتريات", StringComparison.Ordinal) &&
                string.Equals(dashboard.Name, english ? "Dashboard" : "لوحة التحكم", StringComparison.Ordinal),
            "بطاقات العمليات الأساسية مترجمة");
        Require(monitor.BoundingRectangle.Height >= 44 && screenMode.BoundingRectangle.Height >= 44 &&
                layoutMode.BoundingRectangle.Height >= 44 && workspaceToggle.BoundingRectangle.Height >= 44 &&
                workspace.BoundingRectangle.Height >= 44,
            "إجراءات الرأس تحقق مساحة تفاعل لا تقل عن 44 بكسل");
        Require(!monitor.BoundingRectangle.IntersectsWith(screenMode.BoundingRectangle) &&
                !screenMode.BoundingRectangle.IntersectsWith(layoutMode.BoundingRectangle) &&
                !layoutMode.BoundingRectangle.IntersectsWith(workspaceToggle.BoundingRectangle) &&
                !workspaceToggle.BoundingRectangle.IntersectsWith(workspace.BoundingRectangle),
            "إجراءات الرأس لا تتداخل");
        Require(string.Equals(workspaceToggle.Name, english ? "Collapse modules" : "اختصار الأقسام", StringComparison.Ordinal),
            "زر كثافة مساحة العمل يوضح الإجراء التالي باللغة المختارة");
        ActivateButton(workspaceToggle);
        Thread.Sleep(200);
        Require(window.FindFirstDescendant(factory => factory.ByAutomationId("label8")) == null &&
                window.FindFirstDescendant(factory => factory.ByAutomationId("label6")) != null,
            "اختصار مساحة العمل يخفي الإدارة ويبقي المبيعات");
        workspaceToggle = RequireElement(window, "workspaceToggleButton", "زر توسيع الأقسام بعد الاختصار");
        Require(string.Equals(workspaceToggle.Name, english ? "Expand modules" : "توسيع الأقسام", StringComparison.Ordinal),
            "زر كثافة مساحة العمل يعلن التوسيع بعد الاختصار");
        ActivateButton(workspaceToggle);
        Thread.Sleep(200);
        Require(window.FindFirstDescendant(factory => factory.ByAutomationId("label8")) != null,
            "توسيع مساحة العمل يعيد الأقسام الإدارية السابقة");

        string suffix = english ? "home-english" : "home";
        string screenshotPath = Path.Combine(artifactsDirectory, suffix + "-window.png");
        CaptureDashboardWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي للشاشة الرئيسية");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        DateTime exitDeadline = DateTime.UtcNow.AddSeconds(2);
        while (!application.HasExited && DateTime.UtcNow < exitDeadline) Thread.Sleep(100);
        if (!application.HasExited) application.Close();
        exitDeadline = DateTime.UtcNow.AddSeconds(3);
        while (!application.HasExited && DateTime.UtcNow < exitDeadline) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة الرئيسية دون اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, suffix + "-report.json");
        Console.WriteLine("FlaUI home bilingual preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunHomeWorkspaceScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english)
    {
        string expectedTitle = english ? "Customize workspace" : "تخصيص مساحة العمل";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتح محرر مساحة العمل باللغة المطلوبة دون SQL أو حفظ");
        window.Focus();
        Thread.Sleep(500);

        AutomationElement title = RequireElement(window, "homeWorkspaceTitle", "عنوان تخصيص مساحة العمل");
        AutomationElement list = RequireElement(window, "homeWorkspaceModuleList", "قائمة أقسام مساحة العمل");
        AutomationElement basic = RequireElement(window, "homeWorkspaceBasicOption", "وضع أساسي");
        AutomationElement full = RequireElement(window, "homeWorkspaceFullOption", "وضع كامل");
        AutomationElement custom = RequireElement(window, "homeWorkspaceCustomOption", "وضع مخصص");
        AutomationElement moveUp = RequireElement(window, "homeWorkspaceMoveUp", "تحريك لأعلى");
        AutomationElement moveDown = RequireElement(window, "homeWorkspaceMoveDown", "تحريك لأسفل");
        AutomationElement save = RequireElement(window, "homeWorkspaceSave", "حفظ التخصيص");
        AutomationElement reset = RequireElement(window, "homeWorkspaceReset", "استعادة الافتراضي");
        AutomationElement close = RequireElement(window, "homeWorkspaceClose", "إغلاق التخصيص");

        Require(string.Equals(title.Name, expectedTitle, StringComparison.Ordinal), "عنوان المحرر مترجم");
        Require(string.Equals(save.Name, english ? "Save" : "حفظ", StringComparison.Ordinal) &&
                string.Equals(reset.Name, english ? "Reset" : "استعادة الافتراضي", StringComparison.Ordinal) &&
                string.Equals(close.Name, english ? "Close" : "إغلاق", StringComparison.Ordinal),
            "إجراءات محرر مساحة العمل مترجمة بالكامل");
        Require(list.IsEnabled && custom.IsEnabled && basic.IsEnabled && full.IsEnabled,
            "أوضاع العرض وقائمة الترتيب متاحة");
        Require(moveUp.BoundingRectangle.Height >= 44 && moveDown.BoundingRectangle.Height >= 44 &&
                save.BoundingRectangle.Height >= 44 && reset.BoundingRectangle.Height >= 44 && close.BoundingRectangle.Height >= 44,
            "أزرار التخصيص تحقق مساحة تفاعل لا تقل عن 44 بكسل");
        Require(!save.BoundingRectangle.IntersectsWith(reset.BoundingRectangle) &&
                !reset.BoundingRectangle.IntersectsWith(close.BoundingRectangle),
            "إجراءات محرر مساحة العمل لا تتداخل");

        foreach (AutomationElement preset in new[] { full, basic })
        {
            preset.Click();
            Thread.Sleep(150);
            Require(!list.IsEnabled && !moveUp.IsEnabled && !moveDown.IsEnabled,
                "الوضع الجاهز يمنع تعديل ترتيب الأقسام");
            custom.Click();
            Thread.Sleep(150);
            Require(list.IsEnabled && moveUp.IsEnabled && moveDown.IsEnabled,
                "العودة إلى الوضع المخصص تعيد أدوات الترتيب");
        }

        string suffix = english ? "home-workspace-english" : "home-workspace";
        string screenshotPath = Path.Combine(artifactsDirectory, suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل مرئي لمحرر مساحة العمل");
        report.ScreenshotPath = screenshotPath;

        window.Close();
        DateTime exitDeadline = DateTime.UtcNow.AddSeconds(3);
        while (!application.HasExited && DateTime.UtcNow < exitDeadline) Thread.Sleep(100);
        Require(application.HasExited, "أُغلق محرر مساحة العمل دون حفظ أو اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, suffix + "-report.json");
        Console.WriteLine("FlaUI home workspace preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunDashboardScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english)
    {
        string expectedTitle = english ? "Dashboard" : "لوحة التحكم";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتحت لوحة التحكم باللغة المطلوبة وببيانات وهمية دون SQL");
        window.Focus();
        AutomationElement title = RequireElement(window, "label1", "عنوان لوحة التحكم");
        AutomationElement filter = RequireElement(window, "cmbCashierFilter", "مرشح الكاشير AntdUI");
        AutomationElement caption = RequireElement(window, "lblCashierFilter", "تسمية مرشح الكاشير");
        AutomationElement back = RequireElement(window, "button2", "زر الرجوع");
        Require(string.Equals(title.Name, expectedTitle, StringComparison.Ordinal),
            "عنوان المحتوى يطابق لغة النافذة");
        Require(string.Equals(caption.Name, english ? "Cashier" : "الكاشير", StringComparison.Ordinal),
            "تسمية مرشح الكاشير مترجمة");
        Require(string.Equals(back.Name, english ? "Back" : "رجوع", StringComparison.Ordinal),
            "إجراء الرجوع مترجم");
        Require(filter.IsEnabled && filter.BoundingRectangle.Height >= 44,
            "مرشح الكاشير متاح وبمساحة تفاعل مناسبة");
        Require(english
                ? caption.BoundingRectangle.Right < filter.BoundingRectangle.Left
                : filter.BoundingRectangle.Right < caption.BoundingRectangle.Left,
            "اتجاه تسمية مرشح الكاشير متسق دون تداخل");
        filter.Click();
        Thread.Sleep(250);
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Thread.Sleep(150);
        string suffix = english ? "dashboard-english" : "dashboard";
        string screenshotPath = Path.Combine(artifactsDirectory, suffix + "-cashier-window.png");
        CaptureDashboardWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظت صورة نافذة لوحة التحكم نفسها بعد تفاعل المرشح");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(350);
        Require(application.HasExited, "أُغلقت معاينة لوحة التحكم دون اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, suffix + "-report.json");
        Console.WriteLine("FlaUI dashboard bilingual cashier preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(IntPtr windowHandle, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr windowHandle);
    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")]
    private static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr windowHandle, ref NativePoint point);


    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static void CaptureDashboardWindow(IntPtr windowHandle, string path)
    {
        Require(windowHandle != IntPtr.Zero, "مقبض نافذة لوحة التحكم صالح للتصوير");
        if (!GetWindowRect(windowHandle, out NativeRect rectangle))
            throw new InvalidOperationException("تعذر قراءة أبعاد نافذة لوحة التحكم من Windows.");
        Size size = new Size(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        Require(size.Width > 100 && size.Height > 100,
            "مقبض نافذة لوحة التحكم وأبعادها صالحان للتصوير");
        using var bitmap = new Bitmap(size.Width, size.Height);
        Require(PromoteForScreenCapture(windowHandle), "ظهرت نافذة لوحة التحكم مؤقتًا فوق النوافذ الأخرى للتصوير");
        try
        {
            Thread.Sleep(250);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, size);
        }
        finally { RestoreAfterScreenCapture(windowHandle); }
        var colorBuckets = new HashSet<int>();
        int detailedPixels = 0;
        for (int y = 24; y < bitmap.Height - 24; y += 5)
            for (int x = 24; x < bitmap.Width - 24; x += 5)
            {
                Color color = bitmap.GetPixel(x, y);
                colorBuckets.Add(((color.R / 32) << 6) | ((color.G / 32) << 3) | (color.B / 32));
                if (Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) >= 18 ||
                    Math.Min(color.R, Math.Min(color.G, color.B)) < 210)
                    detailedPixels++;
            }
        Require(colorBuckets.Count >= 6 && detailedPixels > 300,
            "الصورة تعرض محتوى لوحة التحكم وليست لقطة فارغة أو موحدة اللون");
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void CaptureWindowContent(IntPtr windowHandle, string path)
    {
        Require(windowHandle != IntPtr.Zero, "مقبض نافذة المعاينة صالح للتصوير");
        Require(GetWindowRect(windowHandle, out NativeRect rectangle), "أمكن قراءة أبعاد نافذة المعاينة");
        Size size = new Size(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        Require(size.Width > 100 && size.Height > 100, "أبعاد نافذة المعاينة صالحة للتصوير");
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr context = graphics.GetHdc();
            try { Require(PrintWindow(windowHandle, context, 2), "التقط Windows نافذة المعاينة مباشرة"); }
            finally { graphics.ReleaseHdc(context); }
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void CaptureVisibleWindow(IntPtr windowHandle, string path)
    {
        Require(windowHandle != IntPtr.Zero, "مقبض النافذة الظاهرة صالح للتصوير");
        Require(GetWindowRect(windowHandle, out NativeRect rectangle), "أمكن قراءة أبعاد النافذة الظاهرة");
        Size size = new Size(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
        Require(size.Width > 100 && size.Height > 100, "أبعاد النافذة الظاهرة صالحة للتصوير");
        using var bitmap = new Bitmap(size.Width, size.Height);
        Require(PromoteForScreenCapture(windowHandle), "ظهرت نافذة المعاينة مؤقتًا فوق النوافذ الأخرى للتصوير");
        try
        {
            Thread.Sleep(250);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(rectangle.Left, rectangle.Top, 0, 0, size);
        }
        finally { RestoreAfterScreenCapture(windowHandle); }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static bool PromoteForScreenCapture(IntPtr windowHandle)
    {
        IntPtr topMost = new IntPtr(-1);
        const uint NoMove = 0x0002;
        const uint NoSize = 0x0001;
        const uint NoActivate = 0x0010;
        const uint ShowWindow = 0x0040;
        return SetWindowPos(windowHandle, topMost, 0, 0, 0, 0, NoMove | NoSize | NoActivate | ShowWindow);
    }

    private static void RestoreAfterScreenCapture(IntPtr windowHandle)
    {
        IntPtr notTopMost = new IntPtr(-2);
        const uint NoMove = 0x0002;
        const uint NoSize = 0x0001;
        const uint NoActivate = 0x0010;
        SetWindowPos(windowHandle, notTopMost, 0, 0, 0, 0, NoMove | NoSize | NoActivate);
    }

    private static int RunAntdArabicScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report)
    {
        Require(window.Title.Contains("AntdUI", StringComparison.Ordinal), "فُتح دليل AntdUI المستقل");
        window.Focus();
        Thread.Sleep(150);
        AutomationElement dateElement = RequireElement(window, "antdArabicDate", "منتقي AntdUI للتاريخ");
        Require(dateElement.IsEnabled && dateElement.Patterns.Value.IsSupported, "منتقي AntdUI للتاريخ قابل للتحرير والوصول");
        dateElement.Patterns.Value.Pattern.SetValue(string.Empty);
        dateElement.Focus();
        Keyboard.Type("2026/09/23");
        Thread.Sleep(250);
        string typedDate = dateElement.Patterns.Value.Pattern.Value.Value;
        Require(string.Equals(typedDate, "2026/09/23", StringComparison.Ordinal),
            "احتفظ منتقي AntdUI بتاريخ الإدخال عبر لوحة المفاتيح حرفيًا؛ المقروء=" + typedDate +
            "؛ التركيز=" + dateElement.Properties.HasKeyboardFocus.Value);
        dateElement.Patterns.Value.Pattern.SetValue("2026/09/23");
        Thread.Sleep(250);
        Require(string.Equals(dateElement.Patterns.Value.Pattern.Value.Value, "2026/09/23", StringComparison.Ordinal), "حافظ منتقي AntdUI على التاريخ المنسق عبر واجهة الوصول حرفيًا");        window.Focus();
        Thread.Sleep(150);
        AutomationElement editorElement = RequireElement(window, "antdArabicInput", "حقل AntdUI العربي");
        AutomationElement selectElement = RequireElement(window, "antdArabicSelect", "قائمة AntdUI العربية");
        AutomationElement quantityElement = RequireElement(window, "antdQuantity", "حقل كمية AntdUI");
        AutomationElement tableElement = RequireElement(window, "antdInvoiceTable", "جدول AntdUI");
        Require(editorElement.Patterns.Value.IsSupported, "حقل AntdUI يدعم ValuePattern للتحرير والوصول");
        string sample = "مُستَلزمات طِبّية — Paracetamol 500 mg";
        editorElement.Patterns.Value.Pattern.SetValue(string.Empty);
        editorElement.Focus();
        Keyboard.Type(sample);
        Thread.Sleep(300);
        string actual = editorElement.Patterns.Value.Pattern.Value.Value;
        Require(string.Equals(actual, sample, StringComparison.Ordinal), "احتفظ AntdUI بالنص العربي المشكول والمختلط حرفيًا بعد إدخال لوحة المفاتيح");
        Require(selectElement.IsEnabled, "قائمة AntdUI العربية مفعّلة");
        Require(quantityElement.IsEnabled, "حقل كمية AntdUI مفعّل");
        Require(tableElement.IsEnabled, "جدول AntdUI مفعّل");
        string screenshotPath = Path.Combine(artifactsDirectory, "antdui-arabic-window.png");
        window.CaptureToFile(screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل تفاعل AntdUI العربي");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(300);
        if (!application.HasExited) application.Close();
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "antdui-arabic-report.json");
        Console.WriteLine("FlaUI AntdUI Arabic interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static void TypeInvariantUnicodeText(string text)
    {
        // KEYEVENTF_UNICODE avoids mapping ASCII through a different thread's HKL.
        // Send a burst: no artificial character delay hides a slow or lossy editor.
        var events = new INPUT[text.Length * 2];
        for (int index = 0; index < text.Length; index++)
        {
            var pressed = new KEYBDINPUT { wScan = text[index], dwFlags = KeyEventFlags.KEYEVENTF_UNICODE };
            var released = pressed;
            released.dwFlags |= KeyEventFlags.KEYEVENTF_KEYUP;
            events[index * 2] = INPUT.KeyboardInput(pressed);
            events[index * 2 + 1] = INPUT.KeyboardInput(released);
        }
        if (User32.SendInput((uint)events.Length, events, INPUT.Size) != events.Length)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    private static int RunInventoryStockScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool lotMode, bool english, bool dark)
    {
        string expectedTitle = english
            ? (lotMode ? "Lot stock and expiry" : "Stock snapshot")
            : (lotMode ? "جرد التشغيلات والصلاحيات" : "كشف جرد المخزن");
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتحت معاينة الجرد المطلوبة دون SQL");
        window.Focus();
        AutomationElement search = RequireElement(window, "inventoryStockSearch", "بحث كشف الجرد");
        AutomationElement includeZero = RequireElement(window, "inventoryStockIncludeZero", "إظهار الرصيد الصفري");
        AutomationElement refresh = RequireElement(window, "inventoryStockRefresh", "تحديث كشف الجرد");
        AutomationElement print = RequireElement(window, "inventoryStockPrint", "طباعة كشف الجرد");
        AutomationElement export = RequireElement(window, "inventoryStockExport", "تصدير كشف الجرد");
        Require(search.Patterns.Value.IsSupported && includeZero.IsEnabled,
            "بحث الجرد وخيار الرصيد الصفري متاحان");
        if (lotMode)
            Require(includeZero.Name.Contains(english ? "lots" : "التشغيلات", StringComparison.OrdinalIgnoreCase),
                "خيار إظهار التشغيلات الصفرية يملك اسم وصول صحيحًا");
        Require(refresh.BoundingRectangle.Top > search.BoundingRectangle.Bottom &&
            print.BoundingRectangle.Top == refresh.BoundingRectangle.Top &&
            export.BoundingRectangle.Top == refresh.BoundingRectangle.Top,
            "إجراءات الجرد في صف ثابت أسفل الفلاتر");
        const string sample = "باراسيتامول 500 mg";
        Require(PromoteForScreenCapture(application.MainWindowHandle), "معاينة الجرد ظاهرة قبل تحرير البحث");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            window.Focus();
            search.Click();
            search.Focus();
            Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && search.Properties.HasKeyboardFocus.Value) break;
        }
        Require(search.Properties.HasKeyboardFocus.Value, "بحث الجرد يملك التركيز");
        TypeInvariantUnicodeText(sample);
        search = RequireElement(window, "inventoryStockSearch", "بحث كشف الجرد بعد الإدخال");
        for (int attempt = 0; attempt < 20 && search.AsTextBox().Text != sample; attempt++) Thread.Sleep(50);
        Require(search.AsTextBox().Text == sample, "حفظ بحث الجرد حرفيًا قبل اختبار الحذف: " + search.AsTextBox().Text);
        Console.WriteLine("Before delete: foreground=" + GetForegroundWindow() + "; main=" + application.MainWindowHandle +
            "; focused=" + search.Properties.HasKeyboardFocus.Value + "; editor=" + search.Properties.NativeWindowHandle.Value);
        Require(GetForegroundWindow() == application.MainWindowHandle && search.Properties.HasKeyboardFocus.Value,
            "بقي التركيز على الحقل بعد تغيير اتجاه المحتوى");
        Keyboard.Type(VirtualKeyShort.END);
        Keyboard.Type(VirtualKeyShort.BACK);
        Thread.Sleep(100);
        Console.WriteLine("Search after Backspace: expected="+sample.Substring(0, sample.Length-1)+"; actual="+search.AsTextBox().Text);
        Require(search.AsTextBox().Text == sample.Substring(0, sample.Length - 1), "حذف نهاية بحث الجرد يحافظ على ترتيب النص");
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Type(VirtualKeyShort.KEY_Z);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(100);
        string actualSearch = search.Patterns.Value.Pattern.Value.Value;
        Require(string.Equals(actualSearch, sample, StringComparison.Ordinal),
            "احتفظ بحث الجرد بالنص العربي والإنجليزي عبر واجهة الوصول حرفيًا؛ الفعلي=" + actualSearch);
        ActivateButton(refresh);
        Thread.Sleep(250);
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة معاينة الجرد");
        Require(status.Name.Contains(english ? "no database data was read" : "لم تُقرأ قاعدة البيانات", StringComparison.OrdinalIgnoreCase),
            "بقي تحديث معاينة الجرد معزولًا عن SQL");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : "-light");
        string screenshotPath = Path.Combine(artifactsDirectory,
            (lotMode ? "inventory-lots" : "inventory-stock") + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت معاينة كشف الجرد");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة الجرد دون تشغيل تقرير أو اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report,
            (lotMode ? "inventory-lots" : "inventory-stock") + suffix + "-report.json");
        Console.WriteLine("FlaUI inventory " + (lotMode ? "lots" : "stock") + " interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryStockLiveQaScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool lotMode)
    {
        string expectedTitle = lotMode ? "جرد التشغيلات والصلاحيات" : "كشف جرد المخزن";
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal),
            "فُتحت شاشة الجرد المتصلة بنسخة QA المؤقتة");
        AutomationElement search = RequireElement(window, "inventoryStockSearch", "بحث تقرير الجرد الحي");
        AutomationElement refresh = RequireElement(window, "inventoryStockRefresh", "تحديث تقرير الجرد الحي");
        AutomationElement print = RequireElement(window, "inventoryStockPrint", "طباعة تقرير الجرد الحي");
        AutomationElement export = RequireElement(window, "inventoryStockExport", "تصدير تقرير الجرد الحي");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة قراءة QA");

        bool loaded = false;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "حالة قراءة QA");
            string current = status.Name;
            if (current.Contains("تم تحميل", StringComparison.Ordinal) && print.IsEnabled && export.IsEnabled)
            {
                loaded = true;
                break;
            }
            if (current.Contains("تعذر تحميل", StringComparison.Ordinal))
                throw new InvalidOperationException("فشل الاستعلام الحي على نسخة QA: " + current);
            Thread.Sleep(100);
        }
        Require(loaded, "اكتمل استعلام QA وعُرضت نتيجة لها طباعة وتصدير متاحان");

        search.Patterns.Value.Pattern.SetValue("QA-LIVE-NO-MATCH-9C8E");
        search = RequireElement(window, "inventoryStockSearch", "بحث QA بعد تغيير المرشح");
        Require(search.AsTextBox().Text == "QA-LIVE-NO-MATCH-9C8E", "حُفظ مرشح البحث الحي حرفيًا");
        Require(!print.IsEnabled && !export.IsEnabled, "أُبطلت مخرجات نتيجة QA السابقة فور تغير المرشح");
        ActivateButton(refresh);
        bool noMatches = false;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "نتيجة مرشح البحث الحي");
            if (status.Name.Contains("لا توجد بيانات مطابقة", StringComparison.Ordinal))
            {
                noMatches = true;
                break;
            }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal))
                throw new InvalidOperationException("فشل استعلام المرشح الحي: " + status.Name);
            Thread.Sleep(100);
        }
        Require(noMatches && !print.IsEnabled && !export.IsEnabled,
            "أعاد استعلام QA الفارغ الحالة الصحيحة وأبقى الطباعة والتصدير معطلين");

        search = RequireElement(window, "inventoryStockSearch", "مسح مرشح بحث QA");
        search.Patterns.Value.Pattern.SetValue(string.Empty);
        ActivateButton(refresh);
        loaded = false;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "إعادة تحميل تقرير QA");
            if (status.Name.Contains("تم تحميل", StringComparison.Ordinal) && print.IsEnabled && export.IsEnabled)
            {
                loaded = true;
                break;
            }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal))
                throw new InvalidOperationException("فشل استعلام إعادة تحميل QA: " + status.Name);
            Thread.Sleep(100);
        }
        Require(loaded, "عادت نتيجة QA بعد إزالة مرشح البحث");

        string screenshotPath = Path.Combine(artifactsDirectory,
            (lotMode ? "inventory-lots" : "inventory-stock") + "-live-qa-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0,
            "حُفظ دليل شاشة تقرير QA الحية");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 50 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت شاشة التقرير الحية بعد اكتمال القراءة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, (lotMode ? "inventory-lots" : "inventory-stock") + "-live-qa-report.json");
        Console.WriteLine("FlaUI live QA inventory report passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryMovementCancelLoadingScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, string? cancellationSignalPath)
    {
        Require(string.Equals(window.Title, "حركة صنف محددة", StringComparison.Ordinal), "فُتحت شاشة حركة الصنف لاختبار الإلغاء");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة استعلام حركة الصنف الجاري");
        bool loading = false;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "انتظار استعلام SQL المحجوب");
            if (status.Name.Contains("جارٍ تحميل بيانات المخزون", StringComparison.Ordinal)) { loading = true; break; }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("ظهر فشل بدل انتظار SQL: " + status.Name);
            Thread.Sleep(100);
        }
        Require(loading, "بقيت الشاشة في حالة تحميل بينما قفل QA يحجب استعلام SQL");
        Require(!string.IsNullOrWhiteSpace(cancellationSignalPath), "وصل مسار إشارة الإغلاق المنسق من بوابة QA");
        DateTime signalDeadline = DateTime.UtcNow.AddSeconds(25);
        while (!File.Exists(cancellationSignalPath) && DateTime.UtcNow < signalDeadline) Thread.Sleep(50);
        Require(File.Exists(cancellationSignalPath), "رصدت بوابة QA استعلام SQL المحجوب قبل طلب الإغلاق");
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-movement-cancel-loading-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت لقطة الشاشة قبل الإغلاق أثناء انتظار SQL");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 100 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت الشاشة وخرج التطبيق بينما استعلام SQL محجوب");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-movement-cancel-loading-report.json");
        Console.WriteLine("FlaUI live SQL cancellation window-close scenario passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryMovementLiveQaScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report)
    {
        Require(string.Equals(window.Title, "حركة صنف محددة", StringComparison.Ordinal), "فُتح تقرير حركة الصنف الحي على نسخة QA");
        AutomationElement medicine = RequireElement(window, "inventoryMovementMedicine", "صنف تقرير الحركة");
        AutomationElement from = RequireElement(window, "inventoryMovementFromDate", "بداية فترة حركة الصنف");
        AutomationElement to = RequireElement(window, "inventoryMovementToDate", "نهاية فترة حركة الصنف");
        AutomationElement refresh = RequireElement(window, "inventoryMovementRefresh", "تحديث حركة الصنف الحية");
        AutomationElement print = RequireElement(window, "inventoryMovementPrint", "طباعة حركة الصنف");
        AutomationElement export = RequireElement(window, "inventoryMovementExport", "تصدير حركة الصنف");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة حركة الصنف الحية");
        bool loaded = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "حالة قراءة حركة الصنف");
            if (status.Name.Contains("تم تحميل حركة الصنف", StringComparison.Ordinal) && print.IsEnabled && export.IsEnabled) { loaded = true; break; }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("فشل استعلام حركة الصنف: " + status.Name);
            Thread.Sleep(100);
        }
        Require(loaded, "حُمّلت حركات الصنف من QA مع تفعيل المخرجات");
        Require(medicine.AsComboBox().SelectedItem != null && from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported,
            "محدد الصنف وفترة التقرير متاحان عبر واجهة الوصول");
        from.Patterns.Value.Pattern.SetValue("2000/01/01");
        to.Patterns.Value.Pattern.SetValue("2000/01/01");
        Require(!print.IsEnabled && !export.IsEnabled, "تغيير الفترة أبطل مخرجات حركة الصنف السابقة");
        ActivateButton(refresh);
        bool noMatches = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "حالة الفترة الخالية");
            if (status.Name.Contains("لا توجد بيانات مطابقة", StringComparison.Ordinal)) { noMatches = true; break; }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("فشل استعلام الفترة الخالية: " + status.Name);
            Thread.Sleep(100);
        }
        Require(noMatches && !print.IsEnabled && !export.IsEnabled, "أعادت فترة QA الخالية الحالة الصحيحة وعطلت المخرجات");
        from = RequireElement(window, "inventoryMovementFromDate", "إعادة فترة حركة الصنف");
        to = RequireElement(window, "inventoryMovementToDate", "إعادة نهاية فترة حركة الصنف");
        from.Patterns.Value.Pattern.SetValue("2026/09/02");
        to.Patterns.Value.Pattern.SetValue("2026/10/02");
        ActivateButton(refresh);
        bool restored = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            status = RequireElement(window, "inventoryDataStatus", "نتيجة إعادة حركة الصنف");
            if (status.Name.Contains("تم تحميل حركة الصنف", StringComparison.Ordinal) && print.IsEnabled && export.IsEnabled) { restored = true; break; }
            if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("فشل استعادة حركات QA: " + status.Name);
            Thread.Sleep(100);
        }
        Require(restored, "عادت بيانات حركة الصنف بعد إعادة الفترة");
        AutomationElement grid = RequireElement(window, "inventoryDataGrid", "جدول حركة الصنف لاختبار النص الكامل");
        var gridBounds = grid.BoundingRectangle;
        int tooltipX = (int)gridBounds.Left + 48;
        int tooltipY = (int)gridBounds.Top + Math.Max(70, Math.Min(120, (int)(gridBounds.Height * 0.20)));
        Mouse.MoveTo(new Point(tooltipX, tooltipY));
        Thread.Sleep(1200);
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-movement-live-qa-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل حركة الصنف الحي");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 50 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت شاشة حركة الصنف بعد اكتمال القراءة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-movement-live-qa-report.json");
        Console.WriteLine("FlaUI live QA item movement passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryIntegrityLiveQaScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report)
    {
        Require(string.Equals(window.Title, "فحص مطابقة المخزون", StringComparison.Ordinal), "فُتح فحص المطابقة الحي على نسخة QA");
        AutomationElement status = RequireElement(window, "inventoryIntegrityStatus", "حالة فحص المطابقة");
        AutomationElement refresh = RequireElement(window, "refreshInventoryIntegrity", "إعادة فحص المطابقة");
        AutomationElement close = RequireElement(window, "closeInventoryIntegrity", "إغلاق فحص المطابقة");
        bool loaded = false;
        for (int attempt = 0; attempt < 150; attempt++)
        {
            status = RequireElement(window, "inventoryIntegrityStatus", "نتيجة فحص المطابقة الحية");
            if (status.Name.Contains("فحص سليم", StringComparison.Ordinal) || status.Name.Contains("توجد", StringComparison.Ordinal) && status.Name.Contains("حركات تحتاج مراجعة", StringComparison.Ordinal)) { loaded = true; break; }
            if (status.Name.Contains("تعذر", StringComparison.Ordinal) || status.Name.Contains("تحقق من الاتصال", StringComparison.Ordinal)) throw new InvalidOperationException("تعذر فحص QA: " + status.Name);
            Thread.Sleep(100);
        }
        Require(loaded && refresh.IsEnabled && close.IsEnabled, "اكتمل فحص QA دون تعديل مخزون");
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-integrity-live-qa-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل فحص المطابقة الحي");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        for (int attempt = 0; attempt < 50 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت شاشة فحص المطابقة بعد اكتمال القراءة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-integrity-live-qa-report.json");
        Console.WriteLine("FlaUI live QA inventory integrity passed. Screenshot=" + screenshotPath);
        return 0;
    }
    private static int RunInventoryOperationalLiveQaScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, string reportKind)
    {
        string expectedTitle = reportKind switch
        {
            "summary" => "تقرير إجمالي حركة الصنف",
            "turnover" => "معدل الدوران والركود للأصناف",
            "alerts" => "تنبيهات المخزون",
            _ => throw new InvalidOperationException("نوع التقرير الحي غير معروف: " + reportKind)
        };
        Require(string.Equals(window.Title, expectedTitle, StringComparison.Ordinal), "فُتحت شاشة التقرير التشغيلي على نسخة QA المؤقتة");
        string prefix = "inventory" + char.ToUpperInvariant(reportKind[0]) + reportKind.Substring(1);
        AutomationElement refresh = RequireElement(window, prefix + "Refresh", "تحديث التقرير الحي");
        AutomationElement print = RequireElement(window, prefix + "Print", "طباعة التقرير الحي");
        AutomationElement export = RequireElement(window, prefix + "Export", "تصدير التقرير الحي");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة قراءة التقرير الحي");
        string finalStatus = WaitForLiveInventoryReportStatus(window, status, TimeSpan.FromSeconds(15));
        Require(finalStatus.Contains("تم تحميل", StringComparison.Ordinal), "عرض التقرير بيانات فعلية من QA: " + finalStatus);
        Require(print.IsEnabled && export.IsEnabled, "تفعيل المخرجات بعد تحميل نتيجة فعلية");

        AutomationElement changedFilter;
        if (reportKind == "summary")
        {
            changedFilter = RequireElement(window, "inventorySummarySearch", "بحث ملخص الحركة");
            changedFilter.Patterns.Value.Pattern.SetValue("QA-LIVE-NO-MATCH-9C8E");
        }
        else if (reportKind == "turnover")
        {
            changedFilter = RequireElement(window, "inventoryTurnoverIdleDays", "حد ركود التقرير");
            changedFilter.Patterns.Value.Pattern.SetValue("61");
        }
        else
        {
            changedFilter = RequireElement(window, "inventoryAlertsLowStock", "حد التنبيه الحي");
            changedFilter.Patterns.Value.Pattern.SetValue("11");
        }
        Require(changedFilter.Patterns.Value.Pattern.Value.Value != null, "احتفظ حقل المرشح بالقيمة الجديدة");
        print = RequireElement(window, prefix + "Print", "طباعة بعد تغيير المرشح");
        export = RequireElement(window, prefix + "Export", "تصدير بعد تغيير المرشح");
        Require(!print.IsEnabled && !export.IsEnabled, "أُبطلت المخرجات السابقة فور تغيير مرشح التقرير");
        ActivateButton(refresh);
        if (reportKind == "summary")
        {
            bool noMatches = false;
            for (int attempt = 0; attempt < 150; attempt++)
            {
                status = RequireElement(window, "inventoryDataStatus", "حالة البحث الحي");
                if (status.Name.Contains("لا توجد بيانات مطابقة", StringComparison.Ordinal)) { noMatches = true; break; }
                if (status.Name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("فشل بحث ملخص الحركة على QA: " + status.Name);
                Thread.Sleep(100);
            }
            Require(noMatches, "أعاد البحث الفريد حالة خلو صحيحة من قاعدة QA");
            changedFilter = RequireElement(window, "inventorySummarySearch", "مسح بحث ملخص الحركة");
            changedFilter.Patterns.Value.Pattern.SetValue(string.Empty);
            ActivateButton(refresh);
        }
        finalStatus = WaitForLiveInventoryReportStatus(window,
            RequireElement(window, "inventoryDataStatus", "نتيجة إعادة قراءة التقرير"), TimeSpan.FromSeconds(15));
        Require(finalStatus.Contains("تم تحميل", StringComparison.Ordinal) || finalStatus.Contains("لا توجد بيانات مطابقة", StringComparison.Ordinal),
            "انتهت إعادة القراءة الحية دون خطأ: " + finalStatus);
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-" + reportKind + "-live-qa-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل التقرير التشغيلي الحي");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 50 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت شاشة التقرير بعد اكتمال القراءة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-" + reportKind + "-live-qa-report.json");
        Console.WriteLine("FlaUI live QA " + reportKind + " report passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static string WaitForLiveInventoryReportStatus(Window window, AutomationElement status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            status = RequireElement(window, "inventoryDataStatus", "حالة التقرير الحي");
            string name = status.Name ?? string.Empty;
            if (name.Contains("تم تحميل", StringComparison.Ordinal) || name.Contains("لا توجد بيانات مطابقة", StringComparison.Ordinal)) return name;
            if (name.Contains("تعذر تحميل", StringComparison.Ordinal)) throw new InvalidOperationException("تعذر تحميل التقرير الحي من QA: " + name);
            Thread.Sleep(100);
        }
        throw new TimeoutException("لم يصل تقرير QA الحي إلى حالة مكتملة؛ آخر حالة=" + status.Name);
    }
    private static int RunInventorySummaryScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english)
    {
        Require(string.Equals(window.Title, english ? "Item movement summary" : "تقرير إجمالي حركة الصنف", StringComparison.Ordinal),
            "فُتحت معاينة تقرير إجمالي حركة الصنف دون SQL");
        AutomationElement search = RequireElement(window, "inventorySummarySearch", "بحث التقرير");
        AutomationElement from = RequireElement(window, "inventorySummaryFromDate", "تاريخ البداية");
        AutomationElement to = RequireElement(window, "inventorySummaryToDate", "تاريخ النهاية");
        AutomationElement hint = RequireElement(window, "inventoryDataEmptyHint", "إرشاد النتائج");
        AutomationElement refresh = RequireElement(window, "inventorySummaryRefresh", "تحديث التقرير");
        AutomationElement print = RequireElement(window, "inventorySummaryPrint", "طباعة التقرير");
        AutomationElement export = RequireElement(window, "inventorySummaryExport", "تصدير التقرير");
        Require(search.Patterns.Value.IsSupported && from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported &&
            !hint.IsOffscreen && refresh.IsEnabled && !print.IsEnabled && !export.IsEnabled,
            "بحث AntdUI والفترة والإرشاد متاحة، والإخراج معطل لعدم وجود نتائج");
        const string sample = "باراسيتامول 500 mg";
        search.Patterns.Value.Pattern.SetValue(sample);
        search = RequireElement(window, "inventorySummarySearch", "بحث التقرير بعد التحرير");
        Require(string.Equals(search.Patterns.Value.Pattern.Value.Value, sample, StringComparison.Ordinal),
            "حافظ بحث AntdUI على النص العربي والإنجليزي المختلط حرفيًا");
        Require((english
                ? search.BoundingRectangle.Left < from.BoundingRectangle.Left && from.BoundingRectangle.Left < to.BoundingRectangle.Left
                : search.BoundingRectangle.Left > from.BoundingRectangle.Left && from.BoundingRectangle.Left > to.BoundingRectangle.Left) &&
            refresh.BoundingRectangle.Top > search.BoundingRectangle.Bottom,
            "تتبع الفلاتر جهة العربية وتبقى الإجراءات في الأسفل");
        ActivateButton(refresh);
        Thread.Sleep(250);
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة ملخص الحركة");
        Require(status.Name.Contains(english ? "no database data was read" : "لم تُقرأ قاعدة البيانات", StringComparison.OrdinalIgnoreCase),
            "بقي تحديث معاينة ملخص الحركة معزولًا عن SQL");
        string suffix = english ? "-english" : string.Empty;
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-summary" + suffix + "-window.png");
        CaptureWindowContent(application.MainWindowHandle, screenshotPath);
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة التقرير دون قاعدة بيانات أو طباعة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-summary" + suffix + "-report.json");
        Console.WriteLine("FlaUI inventory summary preview passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryMovementScenario(Application application, Window window, string artifactsDirectory,
        UiSmokeReport report, bool english)
    {
        Require(string.Equals(window.Title, english ? "Item movements" : "حركة صنف محددة", StringComparison.Ordinal),
            "فُتح تقرير حركة صنف في معاينة معزولة");
        window.Focus();
        Thread.Sleep(200);
        AutomationElement medicine = RequireElement(window, "inventoryMovementMedicine", "محدد صنف التقرير");
        AutomationElement from = RequireElement(window, "inventoryMovementFromDate", "تاريخ البداية");
        AutomationElement to = RequireElement(window, "inventoryMovementToDate", "تاريخ النهاية");
        AutomationElement refresh = RequireElement(window, "inventoryMovementRefresh", "تحديث التقرير");
        AutomationElement print = RequireElement(window, "inventoryMovementPrint", "طباعة التقرير");
        AutomationElement export = RequireElement(window, "inventoryMovementExport", "تصدير التقرير");
        AutomationElement emptyHint = RequireElement(window, "inventoryDataEmptyHint", "إرشاد النتائج الفارغة");
        Require(!emptyHint.IsOffscreen && emptyHint.Name.Contains(english ? "Select a medicine" : "اختر الصنف", StringComparison.OrdinalIgnoreCase),
            "توضح شاشة الحركة الخطوة التالية عند غياب الصفوف");
        Require(from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported,
            "تاريخا التقرير يعرضان قيمهما عبر UI Automation");
        string initialMedicine = medicine.AsComboBox().SelectedItem?.Name ?? string.Empty;
        Require(initialMedicine.Contains("باراسيتامول", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(from.Patterns.Value.Pattern.Value.Value) &&
            !string.IsNullOrWhiteSpace(to.Patterns.Value.Pattern.Value.Value),
            "تظهر بيانات معاينة الصنف والتاريخين دون SQL");
        Require((english
                ? medicine.BoundingRectangle.Left < from.BoundingRectangle.Left && from.BoundingRectangle.Left < to.BoundingRectangle.Left
                : medicine.BoundingRectangle.Left > from.BoundingRectangle.Left && from.BoundingRectangle.Left > to.BoundingRectangle.Left) &&
            refresh.BoundingRectangle.Top > medicine.BoundingRectangle.Bottom &&
            print.BoundingRectangle.Top == refresh.BoundingRectangle.Top &&
            export.BoundingRectangle.Top == refresh.BoundingRectangle.Top,
            "الفلاتر من اليمين والأزرار في صف سفلي واحد");

        Require(PromoteForScreenCapture(application.MainWindowHandle),
            "نافذة حركة المخزون الأصلية ظاهرة قبل إدخال المفاتيح");
        for (int attempt = 0; attempt < 20; attempt++)
        {
            window.Focus();
            medicine = RequireElement(window, "inventoryMovementMedicine", "محدد الصنف قبل إدخال لوحة المفاتيح");
            medicine.Focus();
            Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && medicine.Properties.HasKeyboardFocus.Value) break;
        }
        bool movementInputOwned = GetForegroundWindow() == application.MainWindowHandle && medicine.Properties.HasKeyboardFocus.Value;
        var focusedChildren = window.FindAllDescendants()
            .Where(element => element.Properties.HasKeyboardFocus.ValueOrDefault)
            .Select(element => new { Id = element.AutomationId, Type = element.ControlType.ToString(), Handle = element.Properties.NativeWindowHandle.ValueOrDefault.ToInt64() });
        File.WriteAllText(Path.Combine(artifactsDirectory, "focus-diagnostic.json"), JsonSerializer.Serialize(new {
            Foreground = GetForegroundWindow().ToInt64(), MainWindow = application.MainWindowHandle.ToInt64(),
            MedicineHandle = medicine.Properties.NativeWindowHandle.ValueOrDefault.ToInt64(),
            MedicineFocus = medicine.Properties.HasKeyboardFocus.ValueOrDefault,
            MedicineEnabled = medicine.IsEnabled, OwnedFocusedChildren = focusedChildren.ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
        Require(movementInputOwned && GetForegroundWindow() == application.MainWindowHandle && medicine.Properties.HasKeyboardFocus.Value, "نافذة حركة المخزون ومحدد الصنف يملكان الإدخال قبل ضغط السهم");
        Keyboard.Type(VirtualKeyShort.DOWN);
        Keyboard.Type(VirtualKeyShort.RETURN);
        Thread.Sleep(200);
        string selectedMedicine = medicine.AsComboBox().SelectedItem?.Name ?? string.Empty;
        Console.WriteLine("Inventory movement keyboard-selected value=" + selectedMedicine);
        Require(selectedMedicine.Contains("أموكسيسيلين", StringComparison.Ordinal),
            "يختار المحدد الأصلي الصنف الثاني بلوحة المفاتيح دون تغيير بيانات التقرير");

        Require(GetForegroundWindow() == application.MainWindowHandle,
            "نافذة حركة المخزون تحتفظ بملكية الإدخال قبل فتح التقويم");
        var calendarBounds = from.BoundingRectangle;
        Mouse.Click(new System.Drawing.Point((int)(calendarBounds.Right - 18),
            (int)(calendarBounds.Top + calendarBounds.Height / 2)));
        Thread.Sleep(200);
        window.CaptureToFile(Path.Combine(artifactsDirectory, "inventory-movement-calendar-interaction.png"));

        ActivateButton(refresh);
        Thread.Sleep(250);
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة حركة الصنف");
        Require(status.Name.Contains(english ? "no database data was read" : "لم تُقرأ قاعدة البيانات", StringComparison.OrdinalIgnoreCase),
            "بقي تحديث معاينة حركة الصنف معزولًا عن SQL");

        Require(window.FindFirstDescendant(cf => cf.ByAutomationId("__UiStatusStrip")) == null,
            "معاينة حركة المخزون لا تعرض جاهزية قاعدة بيانات");
        AutomationElement sharedEmpty = RequireElement(window, "__GridEmptyState", "رسالة خلو النتائج المشتركة");
        emptyHint = RequireElement(window, "inventoryDataEmptyHint", "رسالة خلو النتائج المخصصة");
        Require(sharedEmpty.Name == emptyHint.Name &&
            sharedEmpty.Name.Contains(english ? "Select a medicine" : "اختر الصنف", StringComparison.OrdinalIgnoreCase),
            "رسالة خلو النتائج المشتركة تحفظ لغة الشاشة وإرشادها بعد التحديث");
        string suffix = english ? "-english" : string.Empty;
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-movement" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة تقرير حركة الصنف");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة التقرير دون اتصال SQL أو طباعة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-movement" + suffix + "-report.json");
        Console.WriteLine("FlaUI inventory movement interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryTurnoverScenario(Application application, Window window,
        string artifactsDirectory, UiSmokeReport report, bool english)
    {
        Require(string.Equals(window.Title,
            english ? "Item turnover and inactivity" : "معدل الدوران والركود للأصناف",
            StringComparison.Ordinal), "فُتحت معاينة معدل الدوران بالاتجاه المطلوب");
        AutomationElement from = RequireElement(window, "inventoryTurnoverFromDate", "تاريخ بداية الدوران");
        AutomationElement to = RequireElement(window, "inventoryTurnoverToDate", "تاريخ نهاية الدوران");
        AutomationElement days = RequireElement(window, "inventoryTurnoverIdleDays", "حد أيام الركود");
        AutomationElement refresh = RequireElement(window, "inventoryTurnoverRefresh", "تحديث معدل الدوران");
        AutomationElement print = RequireElement(window, "inventoryTurnoverPrint", "طباعة معدل الدوران");
        AutomationElement export = RequireElement(window, "inventoryTurnoverExport", "تصدير معدل الدوران");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة معدل الدوران");
        Require(from.Patterns.Value.IsSupported && to.Patterns.Value.IsSupported && days.Patterns.Value.IsSupported &&
            refresh.IsEnabled && !print.IsEnabled && !export.IsEnabled,
            "حقول معدل الدوران قابلة للوصول والإخراج معطل لعدم وجود نتائج");
        days.Patterns.Value.Pattern.SetValue("0");
        ActivateButton(refresh);
        Thread.Sleep(250);
        Require(status.Name.Contains(english ? "between 1 and 3650" : "بين 1 و3650", StringComparison.OrdinalIgnoreCase),
            "رُفض حد الركود غير المسموح قبل SQL");
        days.Patterns.Value.Pattern.SetValue("60");
        ActivateButton(refresh);
        Thread.Sleep(250);
        Require(status.Name.Contains(english ? "no database data was read" : "لم تُقرأ قاعدة البيانات", StringComparison.OrdinalIgnoreCase),
            "بقي تحديث معاينة الدوران معزولًا عن SQL");
        string suffix = english ? "-english" : string.Empty;
        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-turnover" + suffix + "-window.png");
        window.CaptureToFile(screenshotPath);
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة معدل الدوران دون SQL أو طباعة");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-turnover" + suffix + "-report.json");
        Console.WriteLine("FlaUI inventory turnover thresholds and preview guard passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunInventoryOperationScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english)
    {
        Require(string.Equals(window.Title, english ? "Stock receipt" : "التوريد المخزني", StringComparison.Ordinal), "فُتحت معاينة توريد مخزني معزولة");
        window.Focus();
        Thread.Sleep(200);
        AutomationElement medicine = RequireElement(window, "inventoryOperationMedicine", "محدد صنف المخزون");
        AutomationElement medicineDropdown = RequireElement(window, "inventoryOperationMedicineDropdown", "سهم أصناف المخزون");
        AutomationElement quantity = RequireElement(window, "inventoryOperationQuantity", "كمية التوريد");
        AutomationElement amount = RequireElement(window, "inventoryOperationUnitAmount", "تكلفة الوحدة");
        AutomationElement notes = RequireElement(window, "inventoryOperationNotes", "سبب الحركة");
        AutomationElement commit = RequireElement(window, "inventoryOperationCommit", "زر اعتماد الحركة");
        AutomationElement status = RequireElement(window, "inventoryDataStatus", "حالة عملية المخزون");
        Require(medicine.IsEnabled && quantity.Patterns.Value.IsSupported && amount.Patterns.Value.IsSupported,
            "حقول الصنف والكمية والتكلفة متاحة لقارئ الشاشة والإدخال");
        Require(medicineDropdown.IsEnabled && medicineDropdown.BoundingRectangle.IntersectsWith(medicine.BoundingRectangle),
            "سهم قائمة الصنف المنسق ظاهر داخل الحقل");
        Require((medicine.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains("باراسيتامول", StringComparison.Ordinal),
            "محدد الصنف يعرض الدواء الوهمي الأول دون SQL");
        Require(notes.IsEnabled && notes.BoundingRectangle.Width > 0 && notes.BoundingRectangle.Height > 0,
            "حقل سبب الحركة الأصلي ظاهر وقابل للإدخال");
        Require((english ? medicine.BoundingRectangle.Left < quantity.BoundingRectangle.Left : medicine.BoundingRectangle.Left > quantity.BoundingRectangle.Left) &&
            commit.BoundingRectangle.Top > notes.BoundingRectangle.Bottom,
            "أول حقل على يمين الشاشة العربية والاعتماد ثابت أسفل المدخلات");

        quantity.Patterns.Value.Pattern.SetValue(string.Empty);
        quantity.Focus();
        Keyboard.Type("12");
        Thread.Sleep(200);
        Require(string.Equals(quantity.Patterns.Value.Pattern.Value.Value, "12", StringComparison.Ordinal),
            "احتفظ حقل الكمية بقيمة لوحة المفاتيح حرفيًا");
        medicine.Focus();
        Keyboard.Press(VirtualKeyShort.DOWN);
        Thread.Sleep(150);
        Require((medicine.AsComboBox().SelectedItem?.Name ?? string.Empty).Contains("أموكسيسيلين", StringComparison.Ordinal),
            "اختير صنف المخزون الثاني بالسهم مع بقاء اسم الخدمة المنفصل");
        amount.Patterns.Value.Pattern.SetValue("1.25");
        const string reason = "توريد تجريبي — Batch 500 mg";
        notes.AsTextBox().Text = reason;
        Thread.Sleep(200);
        string reasonRead = notes.Patterns.Value.IsSupported
            ? notes.Patterns.Value.Pattern.Value.Value
            : notes.AsTextBox().Text;
        Require(string.Equals(amount.Patterns.Value.Pattern.Value.Value, "1.25", StringComparison.Ordinal) &&
            string.Equals(reasonRead, reason, StringComparison.Ordinal),
            "احتفظ حقلا المال والسبب بالقيم المختلطة حرفيًا؛ المقروء=" + reasonRead);
        ActivateButton(commit);
        Thread.Sleep(200);
        Require(status.Name.Contains(english ? "no data was saved" : "لم تُحفظ بيانات", StringComparison.Ordinal),
            "بقي اعتماد معاينة حركة المخزون معزولًا عن الحفظ وSQL");

        string screenshotPath = Path.Combine(artifactsDirectory, "inventory-operation-window.png");
        window.CaptureToFile(screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة شاشة عملية المخزون");
        report.ScreenshotPath = screenshotPath;
        window.Close();
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة المخزون دون اعتماد حركة أو اتصال SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "inventory-operation-report.json");
        Console.WriteLine("FlaUI inventory operation input interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunAgentPasswordScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        Require(string.Equals(window.Title, english ? "Employees" : "الموظفون", StringComparison.Ordinal),
            "فُتح محرر الموظفين في معاينة معزولة باللغة المطلوبة");
        AutomationElement title = RequireElement(window, "label2", "عنوان شاشة الموظفين");
        AutomationElement footer = RequireElement(window, "employeeActionBar", "تذييل إجراءات الموظفين");
        AutomationElement statusStrip = RequireElement(window, "__UiStatusStrip", "شريط الحالة");
        AutomationElement close = RequireElement(window, "button2", "زر رجوع الموظفين");
        Require(string.Equals(title.Name, english ? "Employees" : "الموظفون", StringComparison.Ordinal), "عنوان واحد مختصر ومترجم أعلى شاشة الموظفين");
        Require(footer.BoundingRectangle.Bottom <= statusStrip.BoundingRectangle.Top,
            "أزرار الموظفين ثابتة أعلى شريط الحالة دون تداخل");
        Require(string.Equals(close.Name, english ? "Close employee screen" : "إغلاق شاشة الموظفين", StringComparison.Ordinal),
            "اسم زر الرجوع يوضح فعله لقارئ الشاشة");
        AutomationElement add = RequireElement(window, "button4", "إضافة موظف");
        AutomationElement edit = RequireElement(window, "button3", "تعديل موظف");
        AutomationElement delete = RequireElement(window, "button1", "حذف موظف");
        AutomationElement clear = RequireElement(window, "button5", "مسح الحقول");
        AutomationElement grid = RequireElement(window, "EmployeeGV", "قائمة الموظفين");
        Require(!add.IsEnabled && !edit.IsEnabled && !delete.IsEnabled && clear.IsEnabled && close.IsEnabled,
            "المعاينة تمنع عمليات الموظفين الدائمة وتبقي المسح والإغلاق متاحين");
        Require(string.Equals(add.Name, english ? "Add employee" : "إضافة موظف", StringComparison.Ordinal) &&
                string.Equals(edit.Name, english ? "Edit employee" : "تعديل موظف", StringComparison.Ordinal) &&
                string.Equals(delete.Name, english ? "Delete employee" : "حذف موظف", StringComparison.Ordinal),
            "إجراءات الموظفين مترجمة وتملك أسماء وصول واضحة");
        Require(grid.BoundingRectangle.Height > 100, "قائمة الموظفين الوهمية ظاهرة في المعاينة");
        Require(english ? add.BoundingRectangle.Left < close.BoundingRectangle.Left
                        : add.BoundingRectangle.Right > close.BoundingRectangle.Right,
            "تبدأ إجراءات الموظفين من حافة القراءة الصحيحة");
        AutomationElement name = RequireElement(window, "tb2", "اسم الموظف");
        AutomationElement role = RequireElement(window, "roleCombo", "صلاحية الموظف AntdUI");
        AutomationElement password = RequireElement(window, "tb6", "كلمة مرور الموظف");
        Require(name.Patterns.Value.IsSupported, "اسم الموظف يقبل قيمة عبر UI Automation");
        const string nameSample = "محمد أحمد — Admin";
        name.Patterns.Value.Pattern.SetValue(nameSample);
        Require(string.Equals(name.Patterns.Value.Pattern.Value.Value, nameSample, StringComparison.Ordinal),
            "احتفظ اسم الموظف بالنص العربي المختلط حرفيًا");
        Require(role.IsEnabled && role.BoundingRectangle.Width > 0, "محدد الدور ظاهر وقابل للوصول");
        Require(password.Properties.IsPassword.Value, "حقل كلمة المرور يعلن أنه حقل محمي في UI Automation");
        string? exposedPassword = null;
        try
        {
            if (password.Patterns.Value.IsSupported)
                exposedPassword = password.Patterns.Value.Pattern.Value.Value;
        }
        catch (Exception)
        {
            // يمنع مزود UI Automation القياسي قراءة قيمة كلمة المرور.
        }
        Require(!string.Equals(exposedPassword, "Password-Check-123", StringComparison.Ordinal),
            "لا يكشف UI Automation نص كلمة المرور الوهمية");
        password.Focus();
        Thread.Sleep(200);
        Require(password.BoundingRectangle.Top >= title.BoundingRectangle.Bottom &&
                password.BoundingRectangle.Bottom <= footer.BoundingRectangle.Top,
            "التركيز على كلمة المرور يمرر المحرر لإظهار الحقل دون تغطيته بالتذييل");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "agent" + suffix + "-window.png");
        window.CaptureToFile(screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة شاشة الموظفين");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلق محرر الموظفين دون SQL أو حفظ مستخدم");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "agent" + suffix + "-report.json");
        Console.WriteLine("FlaUI employee password accessibility passed.");
        return 0;
    }

    private static int RunQuickSearchScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        string expectedTitle = english ? "Quick medicine search" : "البحث السريع عن الأصناف";
        Require(window.Title.Contains(expectedTitle, StringComparison.Ordinal), "فُتحت معاينة البحث السريع باللغة المطلوبة");
        window.Focus();
        Thread.Sleep(200);
        AutomationElement title = RequireElement(window, "quickSearchTitle", "عنوان البحث السريع");
        AutomationElement search = RequireElement(window, "quickSearchText", "حقل البحث السريع");
        AutomationElement grid = RequireElement(window, "quickSearchResults", "نتائج البحث السريع");
        AutomationElement status = RequireElement(window, "quickSearchStatus", "حالة البحث السريع");
        AutomationElement close = RequireElement(window, "quickSearchClose", "إغلاق البحث السريع");
        Require(string.Equals(title.Name, expectedTitle, StringComparison.Ordinal), "عنوان البحث مترجم بلا عنوان مكرر");
        Require(search.Patterns.Value.IsSupported &&
                string.Equals(search.Patterns.Value.Pattern.Value.Value, "باراسيتامول 500 mg", StringComparison.Ordinal),
            "احتفظ البحث بالنص العربي واللاتيني المختلط حرفيًا");
        Console.WriteLine("Quick search bounds: search=" + search.BoundingRectangle + ", grid=" + grid.BoundingRectangle +
            ", status=" + status.BoundingRectangle);
        Require(grid.BoundingRectangle.Top >= search.BoundingRectangle.Bottom &&
                status.BoundingRectangle.Top + 2 >= grid.BoundingRectangle.Bottom,
            "ترتيب البحث والنتائج والحالة واضح دون تداخل");
        Require(string.Equals(status.Name, english ? "Quick search status" : "حالة البحث السريع", StringComparison.Ordinal),
            "حالة البحث تحمل اسم وصول مترجمًا");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "quick-search" + suffix + "-window.png");
        window.CaptureToFile(screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة البحث السريع");
        report.ScreenshotPath = screenshotPath;
        ActivateButton(close);
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلقت معاينة البحث السريع دون استعلام أو اختيار صنف");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "quick-search" + suffix + "-report.json");
        Console.WriteLine("FlaUI quick search preview passed.");
        return 0;
    }

    private static int RunMedicineEditorScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        string expectedTitle = english ? "Medicines" : "الأدوية";
        Require(window.Title.Contains(expectedTitle, StringComparison.Ordinal), "فُتح محرر الأدوية في معاينة معزولة");
        window.Focus();
        Thread.Sleep(200);
        AutomationElement title = RequireElement(window, "label2", "عنوان شاشة الأدوية");
        AutomationElement grid = RequireElement(window, "MedicineGV", "قائمة الأدوية");
        AutomationElement footer = RequireElement(window, "medicineActionBar", "تذييل إجراءات الأدوية");
        Require(string.Equals(title.Name, expectedTitle, StringComparison.Ordinal), "عنوان واحد مختصر أعلى شاشة الأدوية");
        Require(grid.BoundingRectangle.Top >= title.BoundingRectangle.Bottom &&
                grid.BoundingRectangle.Bottom <= footer.BoundingRectangle.Top,
            "قائمة الأدوية تظهر كاملة عند فتح الشاشة المضغوطة");
        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string initialScreenshotPath = Path.Combine(artifactsDirectory, "medicine" + suffix + "-initial.png");
        CaptureVisibleWindow(application.MainWindowHandle, initialScreenshotPath);
        Require(File.Exists(initialScreenshotPath), "حُفظت صورة بداية شاشة الأدوية");

        AutomationElement company = RequireElement(window, "SelectCompany", "محدد الشركة القابل للكتابة");
        AutomationElement medicineName = RequireElement(window, "MedicineTb", "اسم الدواء");
        AutomationElement production = RequireElement(window, "ProdDate", "تاريخ الإنتاج AntdUI");
        AutomationElement expiry = RequireElement(window, "ExpDate", "تاريخ الانتهاء AntdUI");
        AutomationElement close = RequireElement(window, "button2", "إغلاق شاشة الأدوية");
        Require(string.Equals(close.Name, english ? "Close medicines screen" : "إغلاق شاشة الأدوية", StringComparison.Ordinal),
            "إجراءات الأدوية مترجمة وفق لغة العرض");
        Require(company.Patterns.Value.IsSupported && production.Patterns.Value.IsSupported && expiry.Patterns.Value.IsSupported,
            "الشركة وتاريخا الدواء تدعم قراءة القيمة عبر UI Automation");
        const string medicineSample = "باراسيتامول 500 mg";
        Require(medicineName.IsEnabled && company.IsEnabled &&
            medicineName.Patterns.Value.IsSupported && !medicineName.Patterns.Value.Pattern.IsReadOnly.Value,
            "مسار التحرير مفوض ومتاح؛ المعاينة المقفلة لا تُعتبر محررًا مصادقًا");
        for (int attempt = 0; attempt < 10; attempt++)
        {
            window.Focus();
            medicineName = RequireElement(window, "MedicineTb", "اسم الدواء قبل الإدخال");
            medicineName.Click(); medicineName.Focus(); Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && medicineName.Properties.HasKeyboardFocus.Value) break;
        }
        Require(GetForegroundWindow() == application.MainWindowHandle && medicineName.Properties.HasKeyboardFocus.Value,
            "نافذة الأدوية وحقل الاسم يملكان التركيز قبل Unicode");
        using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_A);
        Require(GetForegroundWindow() == application.MainWindowHandle && medicineName.Properties.HasKeyboardFocus.Value,
            "حقل الدواء يملك التركيز عند إدخال النص المختلط");
        TypeInvariantUnicodeText(medicineSample);
        Thread.Sleep(200);
        medicineName = RequireElement(window, "MedicineTb", "اسم الدواء بعد الإدخال");
        Require(string.Equals(medicineName.Patterns.Value.Pattern.Value.Value, medicineSample, StringComparison.Ordinal),
            "احتفظ حقل الدواء بالنص العربي المختلط حرفيًا");
        const string companySample = "شركة تجريبية — Pharma 500";
        company.Patterns.Value.Pattern.SetValue(companySample);
        Require(string.Equals(company.Patterns.Value.Pattern.Value.Value, companySample, StringComparison.Ordinal),
            "احتفظ محدد الشركة بالنص العربي المختلط حرفيًا");
        string productionValue = production.Patterns.Value.Pattern.Value.Value;
        string expiryValue = expiry.Patterns.Value.Pattern.Value.Value;
        Console.WriteLine("Medicine UI bounds: window=" + window.BoundingRectangle + " company=" + company.BoundingRectangle +
            " production=" + production.BoundingRectangle + " expiry=" + expiry.BoundingRectangle);
        Require(!string.IsNullOrWhiteSpace(productionValue) && !string.IsNullOrWhiteSpace(expiryValue),
            "يعرض تاريخا الإنتاج والانتهاء قيمًا أولية مقروءة");
        Require(company.BoundingRectangle.Bottom <= production.BoundingRectangle.Top &&
            production.BoundingRectangle.Bottom <= expiry.BoundingRectangle.Top,
            "ترتيب الشركة والتاريخين واضح بلا تداخل");

        medicineName.Focus();
        Thread.Sleep(200);
        string screenshotPath = Path.Combine(artifactsDirectory, "medicine" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظت صورة محرر الأدوية");
        report.ScreenshotPath = screenshotPath;
        AutomationElement dropdown = RequireElement(window, "medicineCompanyDropdown", "فتح قائمة الشركات");
        Require(string.Equals(dropdown.Name, english ? "Open manufacturer list" : "فتح قائمة الشركات المصنعة", StringComparison.Ordinal) &&
                dropdown.Patterns.Invoke.IsSupported, "سهم قائمة الشركة ذو اسم وإجراء وصول واضحين");
        ActivateButton(dropdown);
        Require(string.Equals(company.Patterns.Value.Pattern.Value.Value, companySample, StringComparison.Ordinal),
            "فتح قائمة الشركة لا يغير الاسم الحر المدخل");
        File.WriteAllText(Path.Combine(artifactsDirectory, "company-focus-diagnostic.json"), JsonSerializer.Serialize(new {
            Foreground = GetForegroundWindow().ToInt64(), MainWindow = application.MainWindowHandle.ToInt64(),
            CompanyFocus = company.Properties.HasKeyboardFocus.ValueOrDefault,
            CompanyHandle = company.Properties.NativeWindowHandle.ValueOrDefault.ToInt64(),
            CompanyChildren = company.FindAllDescendants().Select(element => new {
                Id = element.Properties.AutomationId.ValueOrDefault, Type = element.Properties.ControlType.ValueOrDefault.ToString(),
                Focus = element.Properties.HasKeyboardFocus.ValueOrDefault,
                Handle = element.Properties.NativeWindowHandle.ValueOrDefault.ToInt64()
            }).ToArray(),
            OwnedFocusedChildren = window.FindAllDescendants().Where(element => element.Properties.HasKeyboardFocus.ValueOrDefault).Select(element => new {
                Id = element.Properties.AutomationId.ValueOrDefault, Type = element.Properties.ControlType.ValueOrDefault.ToString(),
                Handle = element.Properties.NativeWindowHandle.ValueOrDefault.ToInt64()
            }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
        Func<bool> companyEditorHasFocus = () => company.Properties.HasKeyboardFocus.ValueOrDefault ||
            company.FindAllDescendants().Any(element =>
                element.Properties.ControlType.ValueOrDefault == ControlType.Edit &&
                element.Properties.HasKeyboardFocus.ValueOrDefault);
        Require(GetForegroundWindow() == application.MainWindowHandle && companyEditorHasFocus(),
            "نافذة الأدوية ومحرر النص داخل محدد الشركة يملكان الإدخال قبل اختيار المحفوظ");
        Keyboard.Type(VirtualKeyShort.DOWN);
        Keyboard.Type(VirtualKeyShort.RETURN);
        Thread.Sleep(100);
        Require(string.Equals(company.Patterns.Value.Pattern.Value.Value, "شركة محفوظة — Pharma 500", StringComparison.Ordinal),
            "اختيار شركة محفوظة من السهم المخصص يحدّث القيمة المعروضة");
        company.Patterns.Value.Pattern.SetValue(companySample);
        Require(string.Equals(company.Patterns.Value.Pattern.Value.Value, companySample, StringComparison.Ordinal),
            "إدخال اسم شركة حر بعد اختيار المحفوظ لا يعيد الاختيار القديم");
        Thread.Sleep(100);
        ActivateButton(close);
        Thread.Sleep(300);
        Require(application.HasExited, "أُغلق محرر الأدوية دون تحميل SQL أو حفظ سجل");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "medicine-editor" + suffix + "-report.json");
        Console.WriteLine("FlaUI medicine editor interaction passed. Dates=" + productionValue + "/" + expiryValue);
        return 0;
    }

    private static int RunPurchasesInputsScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        Require(window.FindFirstDescendant(cf => cf.ByAutomationId("__UiStatusStrip")) == null,
            "المعاينة المعزولة لا تعرض شريط جاهزية قاعدة البيانات");
        Require(string.Equals(window.Title, english ? "Purchases" : "المشتريات", StringComparison.Ordinal), "فُتحت شاشة المشتريات في وضع UIA المعزول");
        Require(PromoteForScreenCapture(application.MainWindowHandle),
            "نافذة المشتريات ظاهرة قبل فحص الحقول الأصلية");
        window.Focus();
        Thread.Sleep(200);

        AutomationElement supplier = RequireElement(window, "cmbSupplier", "حقل المورد AntdUI في المشتريات");
        AutomationElement barcode = RequireElement(window, "txtBarcode", "حقل الباركود AntdUI في المشتريات");
        AutomationElement quantity = RequireElement(window, "txtQuantity", "حقل الكمية AntdUI في المشتريات");
        AutomationElement medicine = RequireElement(window, "MediComboCb", "اختيار الصنف في المشتريات");
        AutomationElement addToPurchase = RequireElement(window, "btnAddToPurchase", "إضافة الصنف في المشتريات");
        AutomationElement productionDate = RequireElement(window, "dtpProdDate", "تاريخ الإنتاج في المشتريات");
        AutomationElement expiryDate = RequireElement(window, "dtpExpDate", "تاريخ الانتهاء في المشتريات");
        AutomationElement cashier = RequireElement(window, "SelectEmployee", "الموظف المسؤول في المشتريات");
        AutomationElement notes = RequireElement(window, "txtPurchaseNotesInput", "ملاحظات فاتورة الشراء");
        AutomationElement headerTitle = RequireElement(window, "lblPurchasesTitle", "عنوان شاشة المشتريات");
        AutomationElement cartTab = RequireElement(window, "lblTabCart", "تبويب الفاتورة الحالية");
        Require(english
                ? headerTitle.BoundingRectangle.Right < cartTab.BoundingRectangle.Left
                : headerTitle.BoundingRectangle.Left > cartTab.BoundingRectangle.Right,
            "اتجاه عنوان المشتريات والتبويبات يطابق لغة العرض");
        AutomationElement actions = RequireElement(window, "pnlPurchasesActions", "شريط إجراءات المشتريات");
        var actionsBounds = actions.BoundingRectangle;
        double actionTop = -1;
        foreach (string actionName in new[] { "btnSaveOnly", "btnPreviewPrint", "btnPrintA4", "btnSavePdf", "btnClearPurchase", "btnBack" })
        {
            AutomationElement action = RequireElement(window, actionName, "إجراء المشتريات " + actionName);
            var bounds = action.BoundingRectangle;
            Require(bounds.Top >= actionsBounds.Top && bounds.Bottom <= actionsBounds.Bottom &&
                bounds.Left >= actionsBounds.Left && bounds.Right <= actionsBounds.Right && !action.IsOffscreen,
                "زر المشتريات داخل شريط الإجراءات المرئي: " + actionName);
            if (actionTop < 0) actionTop = bounds.Top;
            Require(Math.Abs(bounds.Top - actionTop) <= 2, "إجراءات المشتريات في صف واحد: " + actionName);
        }
        Require(supplier.IsEnabled && barcode.IsEnabled && quantity.IsEnabled, "المورد والباركود والكمية متاحة للتحرير");
        Require(supplier.Patterns.Value.IsSupported, "مورد AntdUI يقبل قيمة حرة عبر UI Automation");
        const string supplierSample = "مورد تجريبي جديد";
        supplier.Patterns.Value.Pattern.SetValue(supplierSample);
        Require(string.Equals(supplier.Patterns.Value.Pattern.Value.Value, supplierSample, StringComparison.Ordinal),
            "احتفظ حقل المورد AntdUI باسم المورد الحر حرفيًا");
        const string notesSample = "ملاحظة شراء — Paracetamol 500 mg";
        // Native multiline text boxes may expose TextPattern rather than ValuePattern.
        // Send literal Unicode only after the owned window and target editor own input.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            window.Focus(); notes.Click(); notes.Focus(); Thread.Sleep(100);
            if (GetForegroundWindow() == application.MainWindowHandle && notes.Properties.HasKeyboardFocus.Value) break;
        }
        Require(GetForegroundWindow() == application.MainWindowHandle && notes.Properties.HasKeyboardFocus.Value,
            "نافذة المشتريات والحقل يملكان التركيز قبل إدخال نص الملاحظات");
        using (Keyboard.Pressing(VirtualKeyShort.CONTROL)) Keyboard.Type(VirtualKeyShort.KEY_A);
        Require(GetForegroundWindow() == application.MainWindowHandle && notes.Properties.HasKeyboardFocus.Value,
            "ما زال التركيز في ملاحظات الشراء قبل إرسال Unicode");
        TypeInvariantUnicodeText(notesSample);
        Thread.Sleep(200);
        AutomationElement currentNotes = RequireElement(window, "txtPurchaseNotesInput", "ملاحظات الشراء بعد التحرير");
        string notesRead = currentNotes.Patterns.Value.IsSupported
            ? currentNotes.Patterns.Value.Pattern.Value.Value : currentNotes.AsTextBox().Text;
        Require(string.Equals(notesRead, notesSample, StringComparison.Ordinal),
            "احتفظت ملاحظة الشراء بالنص العربي والإنجليزي عبر إدخال Unicode حرفيًا (read=" + notesRead + ")");
        Require(barcode.Patterns.Value.IsSupported && quantity.Patterns.Value.IsSupported, "حقلا AntdUI يدعمان ValuePattern");
        Require(string.Equals(barcode.Name, english ? "Barcode" : "الباركود", StringComparison.Ordinal), "اسم وصول حقل الباركود واضح");
        Require(string.Equals(quantity.Name, english ? "Quantity" : "الكمية", StringComparison.Ordinal), "اسم وصول حقل الكمية واضح");
        var barcodeBounds = barcode.BoundingRectangle;
        var medicineBounds = medicine.BoundingRectangle;
        var quantityBounds = quantity.BoundingRectangle;
        var addBounds = addToPurchase.BoundingRectangle;
        var productionBounds = productionDate.BoundingRectangle;
        var expiryBounds = expiryDate.BoundingRectangle;
        Require(barcodeBounds.Bottom <= medicineBounds.Top && medicineBounds.Bottom <= quantityBounds.Top,
            "حقول الباركود والصنف والكمية مرتبة دون تداخل رأسي");
        Require(Math.Abs(quantityBounds.Top - addBounds.Top) <= 10 && !quantityBounds.IntersectsWith(addBounds),
            "حقل الكمية وزر إضافة الصنف في صف واحد دون تداخل");
        Require(quantityBounds.Bottom <= productionBounds.Top &&
            Math.Abs(productionBounds.Top - expiryBounds.Top) <= 10 && !productionBounds.IntersectsWith(expiryBounds),
            "حقل الكمية يسبق حقلي الدفعة والتاريخان متجاوران دون تداخل");

        const string barcodeSample = "1234567890123";
        barcode.Patterns.Value.Pattern.SetValue(string.Empty);
        barcode.Focus();
        Keyboard.Type(barcodeSample);
        Thread.Sleep(200);
        Require(string.Equals(barcode.Patterns.Value.Pattern.Value.Value, barcodeSample, StringComparison.Ordinal),
            "احتفظ حقل الباركود AntdUI بالقيمة المدخلة عبر لوحة المفاتيح حرفيًا");

        const string quantitySample = "12";
        quantity.Patterns.Value.Pattern.SetValue(string.Empty);
        quantity.Focus();
        Keyboard.Type(quantitySample);
        Thread.Sleep(200);
        Require(string.Equals(quantity.Patterns.Value.Pattern.Value.Value, quantitySample, StringComparison.Ordinal),
            "احتفظ حقل الكمية AntdUI بالقيمة المدخلة عبر لوحة المفاتيح حرفيًا");

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "purchases-inputs" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "حُفظ دليل مرئي لشاشة المشتريات المعزولة");
        report.ScreenshotPath = screenshotPath;

        AutomationElement back = RequireElement(window, "btnBack", "إغلاق شاشة المشتريات");
        ActivateButton(back);
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت شاشة المشتريات دون اعتماد فاتورة أو فتح اتصال قاعدة بيانات");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "purchases-inputs" + suffix + "-report.json");
        Console.WriteLine("FlaUI purchase input interaction passed. Screenshot=" + screenshotPath);
        return 0;
    }

    private static int RunBillingCustomerTypeScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report,
        bool english, bool dark)
    {
        Require(window.FindFirstDescendant(cf => cf.ByAutomationId("__UiStatusStrip")) == null,
            "المعاينة المعزولة لا تعرض شريط جاهزية قاعدة البيانات");
        Require(string.Equals(window.Title, english ? "Point of sale" : "نقطة البيع", StringComparison.Ordinal), "فُتحت نقطة البيع في معاينة معزولة");
        window.Focus();
        Thread.Sleep(200);

        AutomationElement customerType = RequireElement(window, "btnBillingCustomerType", "عنصر نوع العميل");
        AutomationElement summary = RequireElement(window, "lblCheckoutTitle", "عنوان ملخص الفاتورة");
        AutomationElement medicine = RequireElement(window, "MediComboCb", "محدد الصنف AntdUI");
        Require((medicine.AsComboBox().SelectedItem?.Name ?? string.Empty) == "باراسيتامول 500 mg",
            "احتفظ محدد الدواء باسم عربي ولاتيني مختلط حرفيًا");
        medicine.AsComboBox().Select(0);
        Require((medicine.AsComboBox().SelectedItem?.Name ?? string.Empty) == "باراسيتامول 500 mg",
            "اختيار الصنف من القائمة يحافظ على الاسم المختلط في المعاينة");
        AutomationElement cashier = RequireElement(window, "SelectEmployee", "محدد الكاشير AntdUI");
        AutomationElement barcode = RequireElement(window, "txtBarcode", "باركود البيع AntdUI");
        AutomationElement quantity = RequireElement(window, "txtQuantity", "كمية البيع AntdUI");
        AutomationElement discount = RequireElement(window, "txtDiscountPercent", "نسبة الخصم AntdUI");
        AutomationElement notes = RequireElement(window, "txtInvoiceNotes", "ملاحظة الفاتورة AntdUI");
        Console.WriteLine("Billing UI bounds: window=" + window.BoundingRectangle + " type=" + customerType.BoundingRectangle + " summary=" + summary.BoundingRectangle);
        Console.WriteLine("Billing customer type: name='" + customerType.Name + "' enabled=" + customerType.IsEnabled);
        Require(customerType.IsEnabled && customerType.Name.Contains(english ? "Customer type" : "نوع العميل", StringComparison.Ordinal),
            "يعرض عنصر واحد نوع العميل ويمكن الوصول إليه");
        Require(window.FindFirstDescendant(factory => factory.ByAutomationId("openCustomerManagementFromBilling")) == null,
            "لا يظهر إجراء مستقل لاختيار العميل");
        Require(customerType.BoundingRectangle.Width > 0 && customerType.BoundingRectangle.Height > 0 &&
            customerType.BoundingRectangle.Bottom <= summary.BoundingRectangle.Top,
            "نوع العميل ظاهر قبل ملخص الفاتورة دون تداخل");
        Require(medicine.IsEnabled && medicine.BoundingRectangle.Width > 0 && cashier.BoundingRectangle.Width > 0,
            "محددا الصنف والكاشير متاحان في ترتيب الشاشة");
        Require(barcode.Patterns.Value.IsSupported && quantity.Patterns.Value.IsSupported,
            "حقلا الباركود والكمية يدعمان قيمة UI Automation");
        barcode.Patterns.Value.Pattern.SetValue(string.Empty);
        barcode.Focus();
        Thread.Sleep(100);
        Keyboard.Type("1234567890123");
        Thread.Sleep(200);
        Require(string.Equals(barcode.Patterns.Value.Pattern.Value.Value, "1234567890123", StringComparison.Ordinal),
            "حفظ حقل باركود البيع القيمة المكتوبة حرفيًا؛ المقروء=" + barcode.Patterns.Value.Pattern.Value.Value);
        quantity.Patterns.Value.Pattern.SetValue(string.Empty);
        quantity.Focus();
        Thread.Sleep(100);
        Keyboard.Type("12");
        Thread.Sleep(200);
        Require(string.Equals(quantity.Patterns.Value.Pattern.Value.Value, "12", StringComparison.Ordinal),
            "حفظ حقل كمية البيع القيمة المكتوبة حرفيًا؛ المقروء=" + quantity.Patterns.Value.Pattern.Value.Value);
        Require(discount.Patterns.Value.IsSupported && (notes.Patterns.Value.IsSupported || notes.Patterns.Text.IsSupported),
            "حقل الخصم ومحرر الملاحظة يدعمان تحرير UI Automation");
        Console.WriteLine("Billing invoice input bounds: discount=" + discount.BoundingRectangle + " notes=" + notes.BoundingRectangle);
        discount.Patterns.Value.Pattern.SetValue("7.5");
        Require(string.Equals(discount.Patterns.Value.Pattern.Value.Value, "7.5", StringComparison.Ordinal),
            "حفظ حقل الخصم القيمة المعينة عبر UI Automation؛ المقروء=" + discount.Patterns.Value.Pattern.Value.Value);
        const string notesSample = "ملاحظة داخلية — Paracetamol 500 mg";
        if (notes.Patterns.Value.IsSupported) notes.Patterns.Value.Pattern.SetValue(notesSample);
        else notes.AsTextBox().Text = notesSample;
        string notesValue = string.Empty;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            notesValue = notes.Patterns.Value.IsSupported ? notes.Patterns.Value.Pattern.Value.Value : notes.AsTextBox().Text;
            if (string.Equals(notesValue, notesSample, StringComparison.Ordinal)) break;
            Thread.Sleep(100);
        }
        Require(string.Equals(notesValue, notesSample, StringComparison.Ordinal),
            "حفظ حقل الملاحظة النص العربي المختلط المعين عبر UI Automation؛ المقروء=" + notesValue);

        foreach (string personalControl in new[] { "cmbCustomerName", "cmbCustomerPhone", "txtCustomerAddress" })
            Require(window.FindFirstDescendant(factory => factory.ByAutomationId(personalControl)) == null,
                "بيانات العميل الشخصية مخفية: " + personalControl);

        string suffix = (english ? "-english" : string.Empty) + (dark ? "-dark" : string.Empty);
        string screenshotPath = Path.Combine(artifactsDirectory, "billing-customer-type" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة نقطة البيع دون اعتماد فاتورة أو فتح SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "billing-customer-type" + suffix + "-report.json");
        Console.WriteLine("FlaUI billing customer type interaction passed.");
        return 0;
    }

    private static int RunQaLivePurchaseCancellationScenario(Application application, UIA2Automation automation,
        Window loginWindow, string artifactsDirectory, UiSmokeReport report)
    {
        string? testUser = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_USER");
        string? testPassword = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_PASSWORD");
        string? purchaseId = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_PURCHASE_ID");
        Require(!string.IsNullOrWhiteSpace(testUser) && !string.IsNullOrWhiteSpace(testPassword), "مدير QA المؤقت متاح");
        Require(!string.IsNullOrWhiteSpace(purchaseId) && purchaseId.StartsWith("QA-CXL-UI-", StringComparison.Ordinal), "معرف فاتورة UI خاص بالاختبار");
        Require(loginWindow.Title == "تسجيل الدخول", "بدأت رحلة إلغاء الشراء الحية من شاشة الدخول");
        AutomationElement user = RequireElement(loginWindow, "username", "اسم مستخدم QA");
        AutomationElement password = RequireElement(loginWindow, "password", "كلمة مرور QA");
        AutomationElement? userEditor = user.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Edit && item.Patterns.Value.IsSupported);
        Require(userEditor != null, "محرر اسم المستخدم متاح");
        userEditor!.Patterns.Value.Pattern.SetValue(testUser!);
        IntPtr passwordHandle = new IntPtr(password.Properties.NativeWindowHandle.Value);
        Require(passwordHandle != IntPtr.Zero, "حقل كلمة المرور يملك مقبضًا صالحًا");
        SendMessage(passwordHandle, 0x000C, IntPtr.Zero, testPassword!);
        Require(SendMessage(passwordHandle, 0x000E, IntPtr.Zero, IntPtr.Zero).ToInt32() == testPassword!.Length, "أُدخلت كلمة مرور QA كاملة");
        ActivateButton(RequireElement(loginWindow, "button1", "زر الدخول"));
        Window? home = WaitForWindow(application, automation, "الرئيسية", TimeSpan.FromSeconds(20));
        Require(home != null, "دخل مدير QA إلى الرئيسية");
        RequireElement(home!, "label9", "بطاقة المشتريات").Click();
        Window? purchases = WaitForWindow(application, automation, "المشتريات", TimeSpan.FromSeconds(15))
            ?? WaitForWindow(application, automation, "Purchases", TimeSpan.FromSeconds(5));
        if (purchases == null)
        {
            string diagnostics = string.Join(Environment.NewLine, application.GetAllTopLevelWindows(automation).Select(item =>
            {
                string title; try { title = item.Title; } catch { title = "[title unavailable]"; }
                string text; try { text = string.Join(" | ", item.FindAllDescendants().Where(child => child.ControlType == ControlType.Text).Select(child => child.Name)); }
                catch { text = "[text unavailable]"; }
                return "WINDOW=" + title + " TEXT=" + text;
            }));
            File.WriteAllText(Path.Combine(artifactsDirectory, "purchase-cancel-live-qa-open-diagnostics.txt"), diagnostics, new System.Text.UTF8Encoding(false));
            throw new InvalidOperationException("لم تفتح شاشة المشتريات بعد اختيار البطاقة. " + diagnostics);
        }
        purchases!.Focus();
        AutomationElement historyTab = RequireElement(purchases, "lblTabHistory", "تبويب سجل المشتريات");
        historyTab.Click();
        Thread.Sleep(800);
        AutomationElement grid = RequireElement(purchases, "PurchaseHistoryGV", "جدول سجل المشتريات");
        CaptureVisibleWindow(new IntPtr(purchases.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "purchase-cancel-live-qa-history.png"));
        AutomationElement? newestRow = grid.FindAllDescendants().FirstOrDefault(item => string.Equals(item.Name, "صف 0", StringComparison.Ordinal));
        if (newestRow == null)
        {
            var names = new List<string>();
            foreach (AutomationElement item in grid.FindAllDescendants())
            {
                try { names.Add(item.Name); } catch { names.Add("[name unavailable]"); }
            }
            string diagnostics = "The QA fixture uses the current timestamp to sort first." + Environment.NewLine + string.Join(Environment.NewLine, names);
            File.WriteAllText(Path.Combine(artifactsDirectory, "purchase-cancel-live-qa-grid-diagnostics.txt"), diagnostics, new System.Text.UTF8Encoding(false));
            throw new InvalidOperationException("تعذر تحديد أحدث صف في سجل المشتريات. " + diagnostics);
        }
        AutomationElement invoiceCell = newestRow;
        invoiceCell.Click();
        Thread.Sleep(300);
        AutomationElement cancelButton = RequireElement(purchases, "btnCancelPurchaseHistory", "زر إلغاء فاتورة الشراء");
        Require(cancelButton.IsEnabled, "إلغاء الفاتورة المحددة متاح للمدير");
        string beforeScreenshot = Path.Combine(artifactsDirectory, "purchase-cancel-live-qa-selected.png");
        CaptureVisibleWindow(new IntPtr(purchases.Properties.NativeWindowHandle.Value), beforeScreenshot);
        ActivateButton(cancelButton);
        Window? confirmation = WaitForOwnedWindow(purchases, "تأكيد إلغاء فاتورة مشتريات", TimeSpan.FromSeconds(10));
        Require(confirmation != null, "إلغاء فاتورة الشراء يتطلب تأكيدًا صريحًا");
        AutomationElement? yes = confirmation!.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Button &&
            (item.Name == "نعم" || item.Name.Contains("Yes", StringComparison.OrdinalIgnoreCase)));
        Require(yes != null, "زر تأكيد الإلغاء موجود");
        ActivateButton(yes!);
        bool cancelled = false;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Thread.Sleep(250);
            AutomationElement currentCancel = RequireElement(purchases, "btnCancelPurchaseHistory", "زر إلغاء الفاتورة بعد التحديث");
            if (!currentCancel.IsEnabled) { cancelled = true; break; }
        }
        Require(cancelled, "تعطل زر الإلغاء بعد إعادة تحميل السجل؛ ستتحقق بوابة SQL من قيد العكس والحالة المحفوظة");        string afterScreenshot = Path.Combine(artifactsDirectory, "purchase-cancel-live-qa-cancelled.png");
        CaptureVisibleWindow(new IntPtr(purchases.Properties.NativeWindowHandle.Value), afterScreenshot);
        report.ScreenshotPath = afterScreenshot;
        purchases.Close(); home!.Close(); loginWindow.Close();
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "purchase-cancel-live-qa-report.json");
        Console.WriteLine("PASS: manager login, purchase history selection, explicit confirmation, cancellation status and duplicate-action prevention.");
        return 0;
    }
    private static int RunQaLiveBillingScenario(Application application, UIA2Automation automation,
        Window loginWindow, string artifactsDirectory, UiSmokeReport report)
    {
        string? testUser = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_USER");
        string? testPassword = Environment.GetEnvironmentVariable("PHARMACY_QA_UI_PASSWORD");
        Require(!string.IsNullOrWhiteSpace(testUser) && !string.IsNullOrWhiteSpace(testPassword), "حساب مدير QA المؤقت متاح");
        Require(loginWindow.Title == "تسجيل الدخول", "بدأت الرحلة الحية من شاشة الدخول");
        AutomationElement user = RequireElement(loginWindow, "username", "اسم مستخدم QA");
        AutomationElement password = RequireElement(loginWindow, "password", "كلمة مرور QA");
        AutomationElement? userEditor = user.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Edit && item.Patterns.Value.IsSupported);
        Require(userEditor != null, "محرر اسم المستخدم متاح");
        userEditor!.Patterns.Value.Pattern.SetValue(testUser!);
        IntPtr passwordHandle = new IntPtr(password.Properties.NativeWindowHandle.Value);
        Require(passwordHandle != IntPtr.Zero, "حقل كلمة المرور يملك مقبضًا صالحًا");
        SendMessage(passwordHandle, 0x000C, IntPtr.Zero, testPassword!);
        Require(SendMessage(passwordHandle, 0x000E, IntPtr.Zero, IntPtr.Zero).ToInt32() == testPassword!.Length, "أُدخلت كلمة مرور QA كاملة");
        ActivateButton(RequireElement(loginWindow, "button1", "زر الدخول"));
        Window? home = WaitForWindow(application, automation, "الرئيسية", TimeSpan.FromSeconds(20));
        if (home == null)
        {
            string diagnostics = string.Join(Environment.NewLine, application.GetAllTopLevelWindows(automation).Select(item => "WINDOW=" + item.Title + " TEXT=" + string.Join(" | ", item.FindAllDescendants().Where(child => child.ControlType == ControlType.Text).Select(child => child.Name))));
            Directory.CreateDirectory(artifactsDirectory);
            File.WriteAllText(Path.Combine(artifactsDirectory, "billing-live-qa-login-diagnostics.txt"), diagnostics, new System.Text.UTF8Encoding(false));
            throw new InvalidOperationException("تعذر دخول مستخدم QA إلى الرئيسية. " + diagnostics);
        }
        Require(home != null, "دخل مستخدم QA إلى الرئيسية");
        RequireElement(home!, "label6", "بطاقة المبيعات").Click();
        Window? billing = WaitForWindow(application, automation, "نقطة البيع", TimeSpan.FromSeconds(15)) ?? WaitForWindow(application, automation, "المبيعات", TimeSpan.FromSeconds(3));
        Require(billing != null, "فتحت نقطة البيع من مسار المستخدم الحقيقي؛ النوافذ: " + string.Join(" | ", application.GetAllTopLevelWindows(automation).Select(item => item.Title)));
        billing!.Focus();
        CaptureVisibleWindow(new IntPtr(billing.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "billing-live-qa-open.png"));
        AutomationElement medicine = RequireElement(billing, "MediComboCb", "قائمة الدواء الحية");

        Require(medicine.IsEnabled, "قائمة الأدوية مفعلة في البيع الحقيقي");
        var medicineCombo = medicine.AsComboBox();
        medicineCombo.Expand();
        Thread.Sleep(300);
        AutomationElement[] medicineOptions = medicineCombo.Items;
        Console.WriteLine("QA medicine options: count=" + medicineOptions.Length + " items=" + string.Join(" | ", medicineOptions.Select(item => item.Name + ":enabled=" + item.IsEnabled)));
        AutomationElement? selectableMedicine = medicineOptions.FirstOrDefault(item => item.IsEnabled);
        Require(selectableMedicine != null, "توجد أصناف مفعلة في قائمة أدوية QA");
        selectableMedicine!.Patterns.SelectionItem.Pattern.Select();
        Thread.Sleep(300);
        Require(medicineCombo.SelectedItem != null, "اختيار دواء QA من القائمة المنسدلة");        AutomationElement quantity = RequireElement(billing, "txtQuantity", "كمية البيع");
        Require(quantity.Patterns.Value.IsSupported, "كمية البيع قابلة للإدخال");
        quantity.Patterns.Value.Pattern.SetValue("1");
        ActivateButton(RequireElement(billing, "btnAddToCart", "إضافة الصنف للسلة"));
        Thread.Sleep(700);
        AutomationElement save = RequireElement(billing, "btnSaveOnly", "حفظ الفاتورة دون طباعة");
        Require(save.IsEnabled, "إضافة الدواء فعّلت الحفظ؛ لم يظهر خطأ في الصنف أو المخزون");
        AutomationElement totalBefore = RequireElement(billing, "PriceBDLabel", "إجمالي الفاتورة قبل الحفظ");
        string displayedTotalBefore = totalBefore.Name;
        Require(!string.IsNullOrWhiteSpace(displayedTotalBefore), "الإجمالي ظاهر قبل الحفظ");
        CaptureVisibleWindow(new IntPtr(billing.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "billing-live-qa-before-save.png"));
        ActivateButton(save);
        Window? saved = WaitForOwnedWindow(billing, "تم", TimeSpan.FromSeconds(15));
        if (saved == null)
        {
            Window[] activeWindows = application.GetAllTopLevelWindows(automation);
            File.WriteAllText(Path.Combine(artifactsDirectory, "billing-live-qa-after-save-windows.txt"), string.Join(Environment.NewLine, activeWindows.Select(item => item.Title)), new System.Text.UTF8Encoding(false));
            for (int index = 0; index < activeWindows.Length; index++)
                CaptureVisibleWindow(new IntPtr(activeWindows[index].Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "billing-live-qa-after-save-window-" + index + ".png"));
            throw new InvalidOperationException("لم تظهر نافذة نجاح الحفظ؛ النوافذ الحالية: " + string.Join(" | ", activeWindows.Select(item => item.Title)));
        }        Require(saved != null, "الحفظ الفعلي أظهر نتيجة النجاح");
        string saveMessage = string.Join(" ", saved!.FindAllDescendants().Where(item => item.ControlType == ControlType.Text).Select(item => item.Name));
        Require(saveMessage.Contains("الحفظ بنجاح") && saveMessage.Contains("مرجع البيع"), "رسالة النجاح تتضمن مرجع البيع");
        AutomationElement? acknowledgeSave = saved.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Button);
        Require(acknowledgeSave != null, "رسالة حفظ الفاتورة قابلة للإغلاق");
        ActivateButton(acknowledgeSave!);
        Thread.Sleep(600);
        AutomationElement cancel = RequireElement(billing, "btnCancelSavedInvoice", "إلغاء الفاتورة المحفوظة");
        Require(cancel.IsEnabled, "إجراء الإلغاء متاح بعد اعتماد الفاتورة");
        Require(!save.IsEnabled, "الحفظ يتعطل بعد اعتماد الفاتورة لمنع النقر المكرر");
        Require(string.Equals(displayedTotalBefore, totalBefore.Name, StringComparison.Ordinal), "الإجمالي المعروض بقي ثابتًا بعد الحفظ");
        CaptureVisibleWindow(new IntPtr(billing.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "billing-live-qa-after-save.png"));
        ActivateButton(cancel);
        Window? confirm = WaitForOwnedWindow(billing, "تأكيد إلغاء الفاتورة", TimeSpan.FromSeconds(10));
        Require(confirm != null, "إلغاء البيع يتطلب تأكيدًا صريحًا");
        AutomationElement? confirmYes = confirm!.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Button && (item.Name == "نعم" || item.Name.Contains("Yes", StringComparison.OrdinalIgnoreCase)));
        Require(confirmYes != null, "زر تأكيد الإلغاء موجود");
        ActivateButton(confirmYes!);
        Window? cancelled = WaitForOwnedWindow(billing, "تم الإلغاء", TimeSpan.FromSeconds(15));
        Require(cancelled != null, "عكس الفاتورة اكتمل بنجاح عبر واجهة البيع");
        AutomationElement? acknowledgeCancel = cancelled!.FindAllDescendants().FirstOrDefault(item => item.ControlType == ControlType.Button);
        Require(acknowledgeCancel != null, "رسالة الإلغاء قابلة للإغلاق");
        ActivateButton(acknowledgeCancel!);
        Thread.Sleep(500);
        Require(!cancel.IsEnabled, "تعطل الإلغاء بعد نجاح العكس لمنع التكرار");
        CaptureVisibleWindow(new IntPtr(billing.Properties.NativeWindowHandle.Value), Path.Combine(artifactsDirectory, "billing-live-qa-after-cancel.png"));
        billing.Close(); home!.Close(); loginWindow.Close();
        report.Passed = true; report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "billing-live-qa-report.json");
        Console.WriteLine("PASS: real QA UI login, sale save, duplicate-action prevention, invoice cancellation and recovery path.");
        return 0;
    }
    private static int RunCustomerManagementScenario(Application application, Window window, string artifactsDirectory, UiSmokeReport report, bool english, bool dark)
    {
        Require(window.FindFirstDescendant(cf => cf.ByAutomationId("__UiStatusStrip")) == null,
            "المعاينة المعزولة لا تعرض شريط جاهزية قاعدة البيانات");
        Require(window.Title == (english ? "Customer management" : "إدارة العملاء"), "فُتحت معاينة إدارة العملاء المعزولة");
        window.Focus();
        AutomationElement name = RequireElement(window, "customerName", "اسم العميل المختلط");
        AutomationElement address = RequireElement(window, "customerAddress", "عنوان العميل العربي");
        Require(name.Patterns.Value.IsSupported && address.Patterns.Value.IsSupported,
            "حقلا الاسم والعنوان متاحان للتحرير عبر واجهة الوصول");
        Require(window.FindFirstDescendant(factory => factory.ByAutomationId("chooseCustomer")) == null,
            "إجراء اختيار العميل محصور بنافذة اختيار عميل الفاتورة");
        const string mixedName = "أحمد Pharma 500 mg";
        AutomationElement search = RequireElement(window, "customerSearch", "بحث العملاء");
        search.Patterns.Value.Pattern.SetValue("ليلى");
        Require(name.Patterns.Value.Pattern.Value.Value == string.Empty, "تغيير نتائج البحث لا يبقي العميل السابق قيد التحرير");
        search.Patterns.Value.Pattern.SetValue("لا توجد نتيجة");
        Require(name.Patterns.Value.Pattern.Value.Value == string.Empty, "البحث الخالي يمسح سجل التحرير السابق");
        search.Patterns.Value.Pattern.SetValue(mixedName);
        Require(search.Patterns.Value.Pattern.Value.Value == mixedName, "البحث يحفظ النص المختلط حرفيًا");
        Require(name.Patterns.Value.Pattern.Value.Value == string.Empty, "نتائج البحث الجديدة تنتظر اختيار سجل صريح");
        name.Patterns.Value.Pattern.SetValue(mixedName);
        address.Patterns.Value.Pattern.SetValue("صنعاء");
        Require(name.Patterns.Value.Pattern.Value.Value == mixedName &&
            address.Patterns.Value.Pattern.Value.Value == "صنعاء",
            "احتفظ محرر العميل بالنص العربي والإنجليزي حرفيًا");
        Require(!RequireElement(window, "saveCustomer", "حفظ العميل").IsEnabled &&
            !RequireElement(window, "openSupplierManagement", "إدارة الموردين").IsEnabled,
            "المعاينة تمنع الحفظ وفتح شاشة الموردين");
        string suffix = (english ? "-english" : "") + (dark ? "-dark" : "");
        string screenshotPath = Path.Combine(artifactsDirectory, "customer-management" + suffix + "-window.png");
        CaptureVisibleWindow(application.MainWindowHandle, screenshotPath);
        report.ScreenshotPath = screenshotPath;
        window.Close();
        for (int attempt = 0; attempt < 20 && !application.HasExited; attempt++) Thread.Sleep(100);
        Require(application.HasExited, "أُغلقت معاينة العملاء دون قراءة أو كتابة SQL");
        report.Passed = true;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        WriteReport(artifactsDirectory, report, "customer-management" + suffix + "-report.json");
        Console.WriteLine("FlaUI customer mixed-text editing and preview save guard passed.");
        return 0;
    }

    private static AutomationElement RequireElement(AutomationElement root, string automationId, string description)
    {
        AutomationElement? element = null;
        // Shown/layout may still be publishing accessibility children after launch.
        var ready = Stopwatch.StartNew();
        do
        {
            element = root.FindFirstDescendant(factory => factory.ByAutomationId(automationId));
            if (element != null) break;
            Thread.Sleep(50);
        } while (ready.Elapsed < TimeSpan.FromSeconds(3));
        if (element == null)
        {
            string visibleButtons = string.Join(" | ", root.FindAllDescendants()
                .Where(item => item.ControlType == ControlType.Button)
                .Select(item => "id=" + item.AutomationId + ", name=" + item.Name + ", bounds=" + item.BoundingRectangle));
            throw new InvalidOperationException("لم يعثر UI Automation على " + description + " (" + automationId + "). " +
                "الأزرار المرئية: " + visibleButtons);
        }

        return element;
    }

    private static AutomationElement WaitForAccessibleName(AutomationElement root, string automationId, string expectedPrefix, TimeSpan timeout, string description)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        string lastName = string.Empty;
        do
        {
            AutomationElement element = RequireElement(root, automationId, description);
            lastName = element.Name ?? string.Empty;
            if (lastName.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                Require(true, "تتفاعل بطاقة الإعدادات في وضع المعاينة دون فتح خدمة. الاسم الحالي: " + lastName);
                return element;
            }

            Thread.Sleep(75);
        }
        while (DateTime.UtcNow < deadline);

        throw new InvalidOperationException("فشل التحقق: لم يُحدّث " + description + " خلال " + timeout.TotalSeconds + " ثوانٍ. الاسم الحالي: " + lastName);
    }

    private static void Require(bool condition, string check)
    {
        if (!condition)
        {
            throw new InvalidOperationException("فشل التحقق: " + check);
        }

        Console.WriteLine("PASS: " + check);
    }

    private static string? ReadOption(string[] args, string option)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        return null;
    }

    private static void ActivateWithKeyboard(Window window, AutomationElement element)
    {
        window.Focus();
        Thread.Sleep(100);
        element.Focus();
        Thread.Sleep(100);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);
    }
    private static void ActivateButton(AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
        {
            try
            {
                element.Patterns.Invoke.Pattern.Invoke();
                return;
            }
            catch (InvalidOperationException)
            {
                // Some AntdUI/WinForms buttons expose Invoke but reject it while
                // their window is transitioning; use the same visible keyboard path.
            }
        }

        element.Focus();
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);
    }

    private static string? ReadValueOption(string[] args, string option)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static void WriteReport(string directory, UiSmokeReport report, string fileName = "login-smoke-report.json")
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class UiSmokeReport
    {
        public bool Passed { get; set; }
        public string AutomationProvider { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string? ScreenshotPath { get; set; }
        public string? ErrorType { get; set; }
        public string? Error { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset CompletedAtUtc { get; set; }
    }
}
















