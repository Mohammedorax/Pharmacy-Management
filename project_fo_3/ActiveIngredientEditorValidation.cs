using System;
using PharmacyManagement.Application;

namespace project_fo_3
{
    internal static class ActiveIngredientEditorValidation
    {
        internal static string RequiredArabicName(string value)
        {
            return ActiveIngredientInputValidator.RequiredArabicName(value);
        }

        internal static string OptionalLatinName(string value)
        {
            return ActiveIngredientInputValidator.OptionalLatinName(value);
        }

        internal static decimal? OptionalStrength(string value, string unit)
        {
            return ActiveIngredientInputValidator.OptionalStrength(value, unit);
        }

        internal static string RequiredEvidenceTitle(string value)
        {
            return ActiveIngredientInputValidator.RequiredEvidenceTitle(value);
        }

        internal static string OptionalPublisher(string value)
        {
            return ActiveIngredientInputValidator.OptionalPublisher(value);
        }

        internal static string OptionalEvidenceUrl(string value)
        {
            return ActiveIngredientInputValidator.OptionalEvidenceUrl(value);
        }

        internal static DateTime? OptionalRetrievedOn(DateTime? value)
        {
            return ActiveIngredientInputValidator.OptionalRetrievedOn(value);
        }
    }
}
