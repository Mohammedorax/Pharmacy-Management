using System;

namespace project_fo_3
{
    // Message remains Arabic for existing service callers and diagnostic contracts.
    // Only explicitly marked templates may be localized; raw values are never translated.
    internal sealed class UiValidationException : InvalidOperationException
    {
        private readonly string template;
        private readonly object[] values;

        internal UiValidationException(string template, params object[] values)
            : base(string.Format(System.Globalization.CultureInfo.CurrentCulture, template, values))
        {
            this.template = template;
            this.values = (object[])values.Clone();
        }

        internal string DisplayMessage(bool english) => UiLanguageCatalog.Format(english, template, values);
    }
}
