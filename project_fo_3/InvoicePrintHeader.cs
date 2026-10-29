using System;

namespace project_fo_3
{
    // Desktop presentation boundary: remove one known caption, never alter business text.
    internal sealed class InvoicePrintHeader
    {
        internal string PartyId { get; private set; }
        internal string PartyName { get; private set; }
        internal string PartyPhone { get; private set; }
        internal string PartyAddress { get; private set; }
        internal string EmployeeId { get; private set; }
        internal string EmployeeName { get; private set; }
        internal string Timing { get; private set; }

        private InvoicePrintHeader(string id, string name, string phone, string address,
            string employeeId, string employeeName, string timing)
        {
            PartyId = id; PartyName = name; PartyPhone = phone; PartyAddress = address;
            EmployeeId = employeeId; EmployeeName = employeeName; Timing = timing;
        }

        internal static InvoicePrintHeader Sales(CommercialInvoicePrintSnapshot saved,
            string id, string name, string phone, string address, string employeeId, string employee, string timing)
        {
            string cashierId = ReadCaption(employeeId, "رقم الكاشير", "Employee Id", "Cashier Id");
            if (saved != null)
                return new InvoicePrintHeader(saved.CustomerId, saved.CustomerName, saved.CustomerPhone,
                    // The saved commercial contract stores IssuedBy, not an employee ID.
                    // Never attribute the current user's ID to an older invoice.
                    saved.CustomerAddress, string.Empty, saved.IssuedBy,
                    saved.IssuedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            return new InvoicePrintHeader(ReadCaption(id, "رقم العميل", "Customer Id"),
                ReadCaption(name, "اسم العميل", "Customer Name"),
                ReadCaption(phone, "هاتف العميل", "Customer Phone"),
                ReadCaption(address, "عنوان العميل", "Customer Address"), cashierId,
                ReadCaption(employee, "اسم الكاشير", "الكاشير", "الموظف", "Employee Name", "Employee", "Cashier"),
                ReadCaption(timing, "التاريخ", "التوقيت", "Time", "Timing", "Date"));
        }

        internal static InvoicePrintHeader Purchase(string supplier, string employee, string timing)
        {
            return new InvoicePrintHeader(string.Empty, ReadCaption(supplier, "المورد", "Supplier"),
                string.Empty, string.Empty, string.Empty,
                ReadCaption(employee, "الموظف", "اسم الكاشير", "الكاشير", "Employee Name", "Employee", "Cashier"),
                ReadCaption(timing, "التاريخ", "التوقيت", "Time", "Timing", "Date"));
        }

        private static string ReadCaption(string value, params string[] captions)
        {
            string text = (value ?? string.Empty).Trim();
            foreach (string caption in captions)
            {
                string prefix = caption + ":";
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return text.Substring(prefix.Length).Trim();
            }
            return text;
        }
    }
}